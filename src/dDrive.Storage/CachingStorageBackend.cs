using System.Collections.Concurrent;
using System.Security;
using dDrive.Core.Connections;
using dDrive.Core.Storage;

namespace dDrive.Storage;

public sealed class CachingStorageBackend : IStorageBackend
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(2);
    private readonly IStorageBackend _inner;
    private readonly ConcurrentDictionary<string, CacheItem<IReadOnlyList<StorageEntry>>> _directories = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CacheItem<StorageEntry>> _entries = new(StringComparer.Ordinal);

    public CachingStorageBackend(IStorageBackend inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public async Task ConnectAsync(ConnectionProfile connection, SecureString? secret, CancellationToken cancellationToken = default)
    {
        Invalidate();
        await _inner.ConnectAsync(connection, secret, cancellationToken);
    }

    public async Task<IReadOnlyList<StorageEntry>> ListDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        if (TryGet(_directories, path, out var cached))
        {
            return cached;
        }

        var entries = await _inner.ListDirectoryAsync(path, cancellationToken);
        _directories[path] = new CacheItem<IReadOnlyList<StorageEntry>>(entries, DateTimeOffset.UtcNow + CacheLifetime);
        return entries;
    }

    public async Task<StorageEntry?> GetEntryAsync(string path, CancellationToken cancellationToken = default)
    {
        if (TryGet(_entries, path, out var cached))
        {
            return cached;
        }

        var entry = await _inner.GetEntryAsync(path, cancellationToken);
        if (entry is not null)
        {
            _entries[path] = new CacheItem<StorageEntry>(entry, DateTimeOffset.UtcNow + CacheLifetime);
        }

        return entry;
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default) =>
        _inner.OpenReadAsync(path, cancellationToken);

    public Task<Stream> OpenWriteAsync(string path, bool overwrite, CancellationToken cancellationToken = default)
    {
        Invalidate();
        return _inner.OpenWriteAsync(path, overwrite, cancellationToken);
    }

    public Task<Stream> OpenAsync(string path, FileMode mode, FileAccess access, CancellationToken cancellationToken = default)
    {
        if (access != FileAccess.Read)
        {
            Invalidate();
        }

        return _inner.OpenAsync(path, mode, access, cancellationToken);
    }

    public Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        Invalidate();
        return _inner.CreateDirectoryAsync(path, cancellationToken);
    }

    public Task DeleteAsync(string path, bool isDirectory, CancellationToken cancellationToken = default)
    {
        Invalidate();
        return _inner.DeleteAsync(path, isDirectory, cancellationToken);
    }

    public Task MoveAsync(string sourcePath, string destinationPath, bool overwrite, CancellationToken cancellationToken = default)
    {
        Invalidate();
        return _inner.MoveAsync(sourcePath, destinationPath, overwrite, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        Invalidate();
        return _inner.DisposeAsync();
    }

    private static bool TryGet<T>(ConcurrentDictionary<string, CacheItem<T>> cache, string path, out T value)
        where T : class
    {
        if (cache.TryGetValue(path, out var item))
        {
            if (item.ExpiresAt > DateTimeOffset.UtcNow)
            {
                value = item.Value;
                return true;
            }

            cache.TryRemove(path, out _);
        }

        value = null!;
        return false;
    }

    private void Invalidate()
    {
        _directories.Clear();
        _entries.Clear();
    }

    private sealed record CacheItem<T>(T Value, DateTimeOffset ExpiresAt);
}