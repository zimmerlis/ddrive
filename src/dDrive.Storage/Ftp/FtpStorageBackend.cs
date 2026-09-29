using System.IO;
using System.Net;
using System.Net.Security;
using System.Security;
using FluentFTP;
using dDrive.Core.Connections;
using dDrive.Core.Storage;

namespace dDrive.Storage.Ftp;

public sealed class FtpStorageBackend : IStorageBackend
{
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private FtpClient? _client;
    private ConnectionProfile? _connection;

    public async Task ConnectAsync(ConnectionProfile connection, SecureString? secret, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(connection.Host);
        ArgumentException.ThrowIfNullOrWhiteSpace(connection.Username);

        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            _client?.Dispose();
            var password = secret is null ? string.Empty : new NetworkCredential(string.Empty, secret).Password;
            var timeoutMilliseconds = Math.Clamp(connection.TimeoutSeconds, 1, 600) * 1000;
            var config = new FtpConfig
            {
                ConnectTimeout = timeoutMilliseconds,
                ReadTimeout = timeoutMilliseconds,
                DataConnectionConnectTimeout = timeoutMilliseconds
            };
            var client = new FtpClient(connection.Host, new NetworkCredential(connection.Username, password), connection.Port, config);

            client.Config.EncryptionMode = connection.UseTls ? FtpEncryptionMode.Explicit : FtpEncryptionMode.None;
            client.ValidateCertificate += (_, args) =>
            {
                args.Accept = !connection.UseTls || !connection.VerifyTlsCertificate || args.PolicyErrors == SslPolicyErrors.None;
            };

            try
            {
                await Task.Run(client.Connect, cancellationToken);
                _client = client;
                _connection = connection;
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public Task<IReadOnlyList<StorageEntry>> ListDirectoryAsync(string path, CancellationToken cancellationToken = default) => RunAsync<IReadOnlyList<StorageEntry>>(client =>
    {
        var virtualPath = Normalize(path);
        var listing = client.GetListing(ToRemotePath(virtualPath), FtpListOption.Auto);
        return listing
            .Where(item => item.Name is not "." and not "..")
            .Select(item => new StorageEntry(
                item.Name,
                Normalize($"{virtualPath.TrimEnd('/')}/{item.Name}"),
                item.Type == FtpObjectType.Directory,
                item.Type == FtpObjectType.Directory ? 0 : item.Size,
                new DateTimeOffset(DateTime.SpecifyKind(item.Modified, DateTimeKind.Utc)),
                false))
            .ToArray();
    }, cancellationToken);

    public Task<StorageEntry?> GetEntryAsync(string path, CancellationToken cancellationToken = default) => RunAsync<StorageEntry?>(client =>
    {
        var virtualPath = Normalize(path);
        var remotePath = ToRemotePath(virtualPath);
        var item = client.GetObjectInfo(remotePath, true);
        if (item is null)
        {
            return null;
        }

        return new StorageEntry(
            virtualPath == "/" ? "/" : virtualPath[(virtualPath.LastIndexOf('/') + 1)..],
            virtualPath,
            item.Type == FtpObjectType.Directory,
            item.Type == FtpObjectType.Directory ? 0 : item.Size,
            new DateTimeOffset(DateTime.SpecifyKind(item.Modified, DateTimeKind.Utc)),
            false);
    }, cancellationToken);

    public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default) => OpenAsync(path, FileMode.Open, FileAccess.Read, cancellationToken);

    public Task<Stream> OpenWriteAsync(string path, bool overwrite, CancellationToken cancellationToken = default) =>
        OpenAsync(path, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, cancellationToken);

    public async Task<Stream> OpenAsync(string path, FileMode mode, FileAccess access, CancellationToken cancellationToken = default)
    {
        var virtualPath = Normalize(path);
        var entry = await GetEntryAsync(virtualPath, cancellationToken);
        if (mode is FileMode.Open or FileMode.Truncate && entry is null)
        {
            throw new FileNotFoundException("Die FTP-Datei wurde nicht gefunden.", path);
        }

        if (mode == FileMode.CreateNew && entry is not null)
        {
            throw new IOException("Die FTP-Datei existiert bereits.");
        }

        if (entry?.IsDirectory == true)
        {
            throw new IOException("Ein Verzeichnis kann nicht als Datei geöffnet werden.");
        }

        if (access == FileAccess.Read && entry is null)
        {
            throw new FileNotFoundException("Die FTP-Datei wurde nicht gefunden.", path);
        }

        if (access != FileAccess.Read)
        {
            EnsureWritable();
        }

        var localPath = Path.GetTempFileName();
        try
        {
            if (entry is not null && (access == FileAccess.Read || mode is FileMode.Open or FileMode.OpenOrCreate or FileMode.Append))
            {
                await RunAsync(client =>
                {
                    client.DownloadFile(localPath, ToRemotePath(virtualPath), FtpLocalExists.Overwrite);
                }, cancellationToken);
            }

            var fileMode = mode switch
            {
                FileMode.Create or FileMode.CreateNew or FileMode.Truncate => FileMode.Create,
                FileMode.OpenOrCreate or FileMode.Append when entry is null => FileMode.Create,
                _ => FileMode.Open
            };
            var stream = new FileStream(localPath, fileMode, access, FileShare.None, 81920, useAsync: true);
            if (mode == FileMode.Append)
            {
                stream.Position = stream.Length;
            }
            return new FtpStagingStream(stream, localPath, access != FileAccess.Read, async token =>
            {
                await stream.FlushAsync(token);
                await stream.DisposeAsync();
                await RunAsync(client =>
                {
                    client.UploadFile(localPath, ToRemotePath(virtualPath), FtpRemoteExists.Overwrite);
                }, token);
            });
        }
        catch
        {
            if (File.Exists(localPath))
            {
                File.Delete(localPath);
            }

            throw;
        }
    }

    public Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default) => RunAsync(client =>
    {
        EnsureWritable();
        client.CreateDirectory(ToRemotePath(Normalize(path)), true);
    }, cancellationToken);

    public Task DeleteAsync(string path, bool isDirectory, CancellationToken cancellationToken = default) => RunAsync(client =>
    {
        EnsureWritable();
        var virtualPath = Normalize(path);
        EnsureNotRoot(virtualPath);
        var remotePath = ToRemotePath(virtualPath);
        if (isDirectory)
        {
            client.DeleteDirectory(remotePath);
        }
        else
        {
            client.DeleteFile(remotePath);
        }
    }, cancellationToken);

    public Task MoveAsync(string sourcePath, string destinationPath, bool overwrite, CancellationToken cancellationToken = default) => RunAsync(client =>
    {
        EnsureWritable();
        var source = Normalize(sourcePath);
        var destination = Normalize(destinationPath);
        EnsureNotRoot(source);
        var remoteDestination = ToRemotePath(destination);
        if (client.FileExists(remoteDestination) || client.DirectoryExists(remoteDestination))
        {
            if (!overwrite)
            {
                throw new IOException("Das FTP-Ziel existiert bereits.");
            }

            if (client.DirectoryExists(remoteDestination))
            {
                client.DeleteDirectory(remoteDestination);
            }
            else
            {
                client.DeleteFile(remoteDestination);
            }
        }

        client.MoveFile(ToRemotePath(source), remoteDestination);
    }, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await _operationLock.WaitAsync();
        try
        {
            _client?.Dispose();
            _client = null;
            _connection = null;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task RunAsync(Action<FtpClient> operation, CancellationToken cancellationToken) =>
        await RunAsync(client => { operation(client); return true; }, cancellationToken);

    private async Task<T> RunAsync<T>(Func<FtpClient, T> operation, CancellationToken cancellationToken)
    {
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            var client = _client;
            if (client is not { IsConnected: true })
            {
                throw new InvalidOperationException("Es besteht keine FTP-Verbindung.");
            }

            return await Task.Run(() => operation(client), cancellationToken);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private string ToRemotePath(string virtualPath) => StoragePath.Resolve(_connection?.RemotePath ?? "/", virtualPath);

    private static string Normalize(string path) => StoragePath.Resolve("/", path);

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

    private sealed class FtpStagingStream(Stream inner, string path, bool upload, Func<CancellationToken, Task> commit) : Stream
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
                try { if (upload) commit(CancellationToken.None).GetAwaiter().GetResult(); else inner.Dispose(); }
                finally { File.Delete(path); }
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                try { if (upload) await commit(CancellationToken.None); else await inner.DisposeAsync(); }
                finally { File.Delete(path); }
            }

            GC.SuppressFinalize(this);
        }
    }
}