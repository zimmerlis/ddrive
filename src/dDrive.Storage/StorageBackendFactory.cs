using dDrive.Core.Connections;
using dDrive.Core.Storage;
using dDrive.Storage.Ftp;
using dDrive.Storage.WebDav;

namespace dDrive.Storage;

public static class StorageBackendFactory
{
    public static IStorageBackend Create(ConnectionProtocol protocol)
    {
        IStorageBackend backend = protocol switch
        {
            ConnectionProtocol.WebDav => new WebDavStorageBackend(),
            ConnectionProtocol.Ftp => new FtpStorageBackend(),
            _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, "Nicht unterstütztes Protokoll.")
        };

        return new CachingStorageBackend(new ReconnectingStorageBackend(backend));
    }
}