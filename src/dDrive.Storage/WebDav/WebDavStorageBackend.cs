using System.IO;
using System.Net;
using System.Net.Security;
using System.Security;
using dDrive.Core.Connections;
using dDrive.Core.Storage;
using WebDAVClient;
using WebDAVClient.Helpers;
using WebDAVClient.Model;

namespace dDrive.Storage.WebDav;

public sealed class WebDavStorageBackend : IStorageBackend
{
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private HttpClient? _httpClient;
    private IClient? _client;
    private Uri? _rootUri;
    private ConnectionProfile? _connection;

    public async Task ConnectAsync(ConnectionProfile connection, SecureString? secret, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(connection.Host);

        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            _client?.Dispose();
            _httpClient?.Dispose();
            _client = null;
            _httpClient = null;

            var rootUri = WebDavEndpoint.CreateRootUri(connection);
            var serverBuilder = new UriBuilder(rootUri.Scheme, rootUri.Host)
            {
                Port = rootUri.IsDefaultPort ? -1 : rootUri.Port,
                Path = "/"
            };
            NetworkCredential? credentials = secret is null
                ? null
                : new NetworkCredential(connection.Username, new NetworkCredential(string.Empty, secret).Password);
            var handler = new HttpClientHandler
            {
                Credentials = credentials,
                PreAuthenticate = credentials is not null,
                ServerCertificateCustomValidationCallback = (_, _, _, errors) =>
                    !connection.VerifyTlsCertificate || errors == SslPolicyErrors.None
            };
            var httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(Math.Clamp(connection.TimeoutSeconds, 1, 600))
            };
            var client = new WebDAVClient.Client(httpClient)
            {
                Server = serverBuilder.Uri.AbsoluteUri,
                BasePath = Uri.UnescapeDataString(rootUri.AbsolutePath)
            };

            _httpClient = httpClient;
            _client = client;
            _rootUri = rootUri;
            _connection = connection;

            try
            {
                await client.GetFolder("/", cancellationToken);
            }
            catch
            {
                client.Dispose();
                httpClient.Dispose();
                _client = null;
                _httpClient = null;
                _rootUri = null;
                _connection = null;
                throw;
            }
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task<IReadOnlyList<StorageEntry>> ListDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        var items = await RequireClient().List(Normalize(path), depth: 1, cancellationToken);
        return items.Select(ToStorageEntry).ToArray();
    }

    public async Task<StorageEntry?> GetEntryAsync(string path, CancellationToken cancellationToken = default)
    {
        var virtualPath = Normalize(path);
        Item item;
        try
        {
            item = await RequireClient().GetFile(virtualPath, cancellationToken);
        }
        catch (WebDAVException exception) when (exception.GetHttpCode() == (int)HttpStatusCode.NotFound)
        {
            try
            {
                item = await RequireClient().GetFolder(virtualPath, cancellationToken);
            }
            catch (WebDAVException folderException) when (folderException.GetHttpCode() == (int)HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        return ToStorageEntry(item);
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default) =>
        RequireClient().Download(Normalize(path), cancellationToken);

    public Task<Stream> OpenWriteAsync(string path, bool overwrite, CancellationToken cancellationToken = default) =>
        OpenAsync(path, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, cancellationToken);

    public async Task<Stream> OpenAsync(string path, FileMode mode, FileAccess access, CancellationToken cancellationToken = default)
    {
        EnsureWritable();
        var virtualPath = Normalize(path);
        var existing = await GetEntryAsync(virtualPath, cancellationToken);
        if ((mode is FileMode.Open or FileMode.Truncate) && existing is null)
        {
            throw new FileNotFoundException("Die WebDAV-Datei wurde nicht gefunden.", path);
        }

        if (mode == FileMode.CreateNew && existing is not null)
        {
            throw new IOException("Die WebDAV-Datei existiert bereits.");
        }

        if (existing?.IsDirectory == true)
        {
            throw new IOException("Ein Verzeichnis kann nicht als Datei geöffnet werden.");
        }

        if (access == FileAccess.Read)
        {
            return await OpenReadAsync(virtualPath, cancellationToken);
        }

        var temporaryPath = Path.GetTempFileName();
        var uploadStream = new FileStream(temporaryPath, FileMode.Truncate, FileAccess.ReadWrite, FileShare.None, 81920, useAsync: true);
        try
        {
            if (existing is not null && mode is FileMode.Open or FileMode.OpenOrCreate or FileMode.Append)
            {
                await using var remote = await OpenReadAsync(virtualPath, cancellationToken);
                await remote.CopyToAsync(uploadStream, cancellationToken);
                uploadStream.Position = mode == FileMode.Append ? uploadStream.Length : 0;
            }

            return new UploadStream(uploadStream, temporaryPath, async token =>
            {
                await uploadStream.FlushAsync(token);
                await uploadStream.DisposeAsync();
                await using var source = File.OpenRead(temporaryPath);
                var uploaded = await RequireClient().Upload(
                    GetParentPath(virtualPath),
                    source,
                    GetFileName(virtualPath),
                    cancellationToken: token);
                if (!uploaded)
                {
                    throw new IOException("Der WebDAV-Server hat den Upload nicht bestätigt.");
                }
            });
        }
        catch
        {
            await uploadStream.DisposeAsync();
            File.Delete(temporaryPath);
            throw;
        }
    }

    public async Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        EnsureWritable();
        var virtualPath = Normalize(path);
        EnsureNotRoot(virtualPath);
        var created = await RequireClient().CreateDir(
            GetParentPath(virtualPath),
            GetFileName(virtualPath),
            cancellationToken);
        if (!created)
        {
            throw new IOException("Der WebDAV-Server hat das Erstellen des Verzeichnisses nicht bestätigt.");
        }
    }

    public async Task DeleteAsync(string path, bool isDirectory, CancellationToken cancellationToken = default)
    {
        EnsureWritable();
        var virtualPath = Normalize(path);
        EnsureNotRoot(virtualPath);
        if (isDirectory)
        {
            await RequireClient().DeleteFolder(virtualPath, cancellationToken: cancellationToken);
        }
        else
        {
            await RequireClient().DeleteFile(virtualPath, cancellationToken: cancellationToken);
        }
    }

    public async Task MoveAsync(string sourcePath, string destinationPath, bool overwrite, CancellationToken cancellationToken = default)
    {
        EnsureWritable();
        var source = Normalize(sourcePath);
        var destination = Normalize(destinationPath);
        EnsureNotRoot(source);
        var entry = await GetEntryAsync(source, cancellationToken)
            ?? throw new FileNotFoundException("Die WebDAV-Quelle wurde nicht gefunden.", sourcePath);

        var moved = entry.IsDirectory
            ? await RequireClient().MoveFolder(source, destination, overwrite, cancellationToken: cancellationToken)
            : await RequireClient().MoveFile(source, destination, overwrite, cancellationToken: cancellationToken);
        if (!moved)
        {
            throw new IOException("Der WebDAV-Server hat das Verschieben nicht bestätigt.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _lifecycleLock.WaitAsync();
        try
        {
            _client?.Dispose();
            _httpClient?.Dispose();
            _client = null;
            _httpClient = null;
            _rootUri = null;
            _connection = null;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private IClient RequireClient() => _client ?? throw new InvalidOperationException("Es besteht keine WebDAV-Verbindung.");

    private StorageEntry ToStorageEntry(Item item)
    {
        var path = ToVirtualPath(item.Href);
        var name = path == "/" ? "/" : GetFileName(path);
        var modified = item.LastModified ?? item.CreationDate ?? DateTime.UnixEpoch;
        return new StorageEntry(
            name,
            path,
            item.IsCollection,
            item.ContentLength ?? 0,
            new DateTimeOffset(DateTime.SpecifyKind(modified, DateTimeKind.Utc)),
            false);
    }

    private string ToVirtualPath(string href)
    {
        var root = _rootUri ?? throw new InvalidOperationException("Es besteht keine WebDAV-Verbindung.");
        var itemUri = Uri.TryCreate(href, UriKind.Absolute, out var absoluteUri)
            ? absoluteUri
            : new Uri(root, href);
        if (!string.Equals(itemUri.Scheme, root.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(itemUri.Host, root.Host, StringComparison.OrdinalIgnoreCase) ||
            itemUri.Port != root.Port)
        {
            throw new InvalidDataException("Der WebDAV-Server lieferte einen Eintrag außerhalb des konfigurierten Endpunkts.");
        }

        var rootPath = Uri.UnescapeDataString(root.AbsolutePath).TrimEnd('/');
        var itemPath = Uri.UnescapeDataString(itemUri.AbsolutePath).TrimEnd('/');
        if (string.Equals(itemPath, rootPath, StringComparison.OrdinalIgnoreCase))
        {
            return "/";
        }

        var rootPrefix = rootPath + "/";
        if (!itemPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Der WebDAV-Server lieferte einen Eintrag außerhalb des konfigurierten Stammverzeichnisses.");
        }

        return StoragePath.Resolve("/", itemPath[rootPath.Length..]);
    }

    private static string Normalize(string path) => StoragePath.Resolve("/", path);

    private static string GetParentPath(string path)
    {
        var index = path.LastIndexOf('/');
        return index <= 0 ? "/" : path[..index];
    }

    private static string GetFileName(string path) => path[(path.TrimEnd('/').LastIndexOf('/') + 1)..];

    private void EnsureWritable()
    {
        if (_connection?.ReadOnly == true)
        {
            throw new UnauthorizedAccessException("Diese Verbindung ist schreibgeschützt.");
        }
    }

    private static void EnsureNotRoot(string path)
    {
        if (path == "/")
        {
            throw new UnauthorizedAccessException("Das Stammverzeichnis der Verbindung kann nicht entfernt oder verschoben werden.");
        }
    }

    private sealed class UploadStream(Stream inner, string temporaryPath, Func<CancellationToken, Task> commit) : Stream
    {
        private bool _disposed;
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => inner.Read(buffer);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        public override void Write(ReadOnlySpan<byte> buffer) => inner.Write(buffer);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => inner.WriteAsync(buffer, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                try { commit(CancellationToken.None).GetAwaiter().GetResult(); }
                finally { File.Delete(temporaryPath); }
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                try { await commit(CancellationToken.None); }
                finally { File.Delete(temporaryPath); }
            }

            GC.SuppressFinalize(this);
        }
    }
}