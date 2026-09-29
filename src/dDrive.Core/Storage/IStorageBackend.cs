using System.Security;
using dDrive.Core.Connections;

namespace dDrive.Core.Storage;

public interface IStorageBackend : IAsyncDisposable
{
    Task ConnectAsync(ConnectionProfile connection, SecureString? secret, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StorageEntry>> ListDirectoryAsync(string path, CancellationToken cancellationToken = default);
    Task<StorageEntry?> GetEntryAsync(string path, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default);
    Task<Stream> OpenWriteAsync(string path, bool overwrite, CancellationToken cancellationToken = default);
    Task<Stream> OpenAsync(string path, FileMode mode, FileAccess access, CancellationToken cancellationToken = default);
    Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default);
    Task DeleteAsync(string path, bool isDirectory, CancellationToken cancellationToken = default);
    Task MoveAsync(string sourcePath, string destinationPath, bool overwrite, CancellationToken cancellationToken = default);
}