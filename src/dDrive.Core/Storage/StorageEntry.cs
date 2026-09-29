namespace dDrive.Core.Storage;

public sealed record StorageEntry(
    string Name,
    string Path,
    bool IsDirectory,
    long Length,
    DateTimeOffset LastModified,
    bool IsReadOnly);