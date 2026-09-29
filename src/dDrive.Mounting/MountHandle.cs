namespace dDrive.Mounting;

public sealed class MountHandle : IAsyncDisposable
{
    private readonly Func<ValueTask> _dispose;
    private readonly Func<ValueTask> _detach;

    internal MountHandle(Guid connectionId, char driveLetter, bool isRclone, Func<ValueTask> dispose, Func<ValueTask> detach)
    {
        ConnectionId = connectionId;
        DriveLetter = driveLetter;
        IsRclone = isRclone;
        _dispose = dispose;
        _detach = detach;
    }

    public Guid ConnectionId { get; }
    public char DriveLetter { get; }
    internal bool IsRclone { get; }

    public async ValueTask DisposeAsync()
    {
        await _dispose();
    }

    internal ValueTask DetachAsync() => _detach();
}