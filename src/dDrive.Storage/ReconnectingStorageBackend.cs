using System.Net.Http;
using System.Net.Sockets;
using System.Security;
using dDrive.Core.Connections;
using dDrive.Core.Storage;
using FluentFTP;

namespace dDrive.Storage;

public sealed class ReconnectingStorageBackend : IStorageBackend
{
    private readonly IStorageBackend _inner;
    private readonly SemaphoreSlim _reconnectLock = new(1, 1);
    private ConnectionProfile? _connection;
    private SecureString? _secret;
    private long _connectionGeneration;

    public ReconnectingStorageBackend(IStorageBackend inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public async Task ConnectAsync(ConnectionProfile connection, SecureString? secret, CancellationToken cancellationToken = default)
    {
        _connection = connection;
        _secret?.Dispose();
        _secret = secret?.Copy();
        _secret?.MakeReadOnly();
        await _inner.ConnectAsync(connection, _secret, cancellationToken);
        Interlocked.Increment(ref _connectionGeneration);
    }

    public Task<IReadOnlyList<StorageEntry>> ListDirectoryAsync(string path, CancellationToken cancellationToken = default) =>
        ExecuteReadAsync(() => _inner.ListDirectoryAsync(path, cancellationToken), cancellationToken);

    public Task<StorageEntry?> GetEntryAsync(string path, CancellationToken cancellationToken = default) =>
        ExecuteReadAsync(() => _inner.GetEntryAsync(path, cancellationToken), cancellationToken);

    public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default) =>
        ExecuteReadAsync(() => _inner.OpenReadAsync(path, cancellationToken), cancellationToken);

    public Task<Stream> OpenWriteAsync(string path, bool overwrite, CancellationToken cancellationToken = default) =>
        _inner.OpenWriteAsync(path, overwrite, cancellationToken);

    public Task<Stream> OpenAsync(string path, FileMode mode, FileAccess access, CancellationToken cancellationToken = default) =>
        access == FileAccess.Read
            ? ExecuteReadAsync(() => _inner.OpenAsync(path, mode, access, cancellationToken), cancellationToken)
            : _inner.OpenAsync(path, mode, access, cancellationToken);

    public Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default) =>
        _inner.CreateDirectoryAsync(path, cancellationToken);

    public Task DeleteAsync(string path, bool isDirectory, CancellationToken cancellationToken = default) =>
        _inner.DeleteAsync(path, isDirectory, cancellationToken);

    public Task MoveAsync(string sourcePath, string destinationPath, bool overwrite, CancellationToken cancellationToken = default) =>
        _inner.MoveAsync(sourcePath, destinationPath, overwrite, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        _secret?.Dispose();
        _secret = null;
        _connection = null;
        await _inner.DisposeAsync();
    }

    private async Task<T> ExecuteReadAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        var generation = Volatile.Read(ref _connectionGeneration);
        try
        {
            return await operation();
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested && IsTransient(exception))
        {
            var connection = _connection ?? throw new InvalidOperationException("Es besteht keine Verbindung.", exception);
            await _reconnectLock.WaitAsync(cancellationToken);
            try
            {
                if (generation == Volatile.Read(ref _connectionGeneration))
                {
                    await _inner.ConnectAsync(connection, _secret, cancellationToken);
                    Interlocked.Increment(ref _connectionGeneration);
                }
            }
            finally
            {
                _reconnectLock.Release();
            }

            return await operation();
        }
    }

    private static bool IsTransient(Exception exception) => exception switch
    {
        HttpRequestException or SocketException or TimeoutException or TaskCanceledException => true,
        _ when exception.InnerException is not null => IsTransient(exception.InnerException),
        _ => false
    };
}