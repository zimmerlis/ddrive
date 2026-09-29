using System.Collections.Concurrent;
using System.Security;
using dDrive.Core.Connections;

namespace dDrive.Mounting;

public sealed class MountService : IAsyncDisposable
{
    private readonly ConcurrentDictionary<Guid, MountHandle> _mounts = new();
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly WindowsNativeMountService _windowsMountService = new();
    private readonly RcloneMountService _rcloneMountService = new();

    public IReadOnlyCollection<MountHandle> Mounts => _mounts.Values.ToArray();

    public async Task<MountHandle> MountAsync(ConnectionProfile connection, SecureString? secret, bool useRclone, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("Laufwerks-Mounts sind nur unter Windows verfügbar.");
            }

            if (!char.IsAsciiLetter(connection.DriveLetter))
            {
                throw new ArgumentException("Der Laufwerksbuchstabe ist ungültig.", nameof(connection));
            }

            if (_mounts.ContainsKey(connection.Id))
            {
                throw new InvalidOperationException("Diese Verbindung ist bereits eingebunden.");
            }

            if (_mounts.Values.Any(mount => mount.DriveLetter == char.ToUpperInvariant(connection.DriveLetter)))
            {
                throw new InvalidOperationException($"Das Laufwerk {connection.DriveLetter}: ist bereits eingebunden.");
            }

            MountHandle handle;
            if (useRclone)
            {
                _windowsMountService.DisconnectExistingMapping(connection.DriveLetter);
                var session = await _rcloneMountService.MountAsync(connection, secret, cancellationToken);
                handle = new MountHandle(
                    connection.Id,
                    char.ToUpperInvariant(connection.DriveLetter),
                    isRclone: true,
                    session.DisposeAsync,
                    session.DetachAsync);
            }
            else
            {
                await Task.Run(() => _windowsMountService.Mount(connection, secret), cancellationToken);
                handle = new MountHandle(
                    connection.Id,
                    char.ToUpperInvariant(connection.DriveLetter),
                    isRclone: false,
                    () =>
                    {
                        _windowsMountService.Unmount(connection.DriveLetter);
                        return ValueTask.CompletedTask;
                    },
                    () =>
                    {
                        return ValueTask.CompletedTask;
                    });
            }

            _mounts[connection.Id] = handle;
            return handle;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task<IReadOnlySet<Guid>> RestoreRcloneMountsAsync(IEnumerable<ConnectionProfile> connections, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connections);
        var restored = new HashSet<Guid>();
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            foreach (var connection in connections)
            {
                var session = await _rcloneMountService.RestoreAsync(connection, cancellationToken);
                if (session is null || _mounts.ContainsKey(connection.Id))
                {
                    continue;
                }

                var handle = new MountHandle(
                    connection.Id,
                    char.ToUpperInvariant(connection.DriveLetter),
                    isRclone: true,
                    session.DisposeAsync,
                    session.DetachAsync);
                _mounts[connection.Id] = handle;
                restored.Add(connection.Id);
            }

            return restored;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task<bool> UnmountAsync(Guid connectionId)
    {
        await _lifecycleLock.WaitAsync();
        try
        {
            if (!_mounts.TryRemove(connectionId, out var handle))
            {
                return false;
            }

            await handle.DisposeAsync();
            return true;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public ValueTask DisposeAsync() => DisposeAsync(keepRcloneMounts: false);

    public async ValueTask DisposeAsync(bool keepRcloneMounts)
    {
        foreach (var connectionId in _mounts.Keys)
        {
            if (keepRcloneMounts && _mounts.TryGetValue(connectionId, out var handle) && handle.IsRclone)
            {
                await _lifecycleLock.WaitAsync();
                try
                {
                    if (_mounts.TryRemove(connectionId, out handle))
                    {
                        await handle.DetachAsync();
                    }
                }
                finally
                {
                    _lifecycleLock.Release();
                }
            }
            else
            {
                await UnmountAsync(connectionId);
            }
        }
    }
}