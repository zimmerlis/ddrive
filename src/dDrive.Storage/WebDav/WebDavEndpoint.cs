using dDrive.Core.Connections;
using dDrive.Core.Storage;

namespace dDrive.Storage.WebDav;

public static class WebDavEndpoint
{
    public static Uri CreateRootUri(ConnectionProfile connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(connection.Host);

        var hostValue = connection.Host.Trim();
        string scheme;
        string host;
        int port;
        string rootPath;

        if (Uri.TryCreate(hostValue, UriKind.Absolute, out var serverUri))
        {
              if ((!string.Equals(serverUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                  !string.Equals(serverUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) ||
                !string.IsNullOrEmpty(serverUri.UserInfo) ||
                !string.IsNullOrEmpty(serverUri.Query) ||
                !string.IsNullOrEmpty(serverUri.Fragment))
            {
                throw new ArgumentException("Der WebDAV-Host muss ein HTTP- oder HTTPS-Host ohne Zugangsdaten sein.", nameof(connection));
            }

            scheme = serverUri.Scheme;
            host = serverUri.Host;
            port = serverUri.IsDefaultPort ? connection.Port : serverUri.Port;
            var hostPath = Uri.UnescapeDataString(serverUri.AbsolutePath);
            rootPath = StoragePath.Resolve(hostPath, connection.RemotePath);
        }
        else
        {
            if (hostValue.Contains("://", StringComparison.Ordinal))
            {
                throw new ArgumentException("Der WebDAV-Host verwendet ein nicht unterstütztes URL-Schema.", nameof(connection));
            }

            scheme = connection.UseTls ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
            host = hostValue;
            port = connection.Port;
            rootPath = StoragePath.Resolve("/", connection.RemotePath);
        }

        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(connection), "Der WebDAV-Port muss zwischen 1 und 65535 liegen.");
        }

        var uriBuilder = new UriBuilder(scheme, host, port, EscapePath(rootPath.TrimEnd('/')));
        if (!uriBuilder.Path.EndsWith("/", StringComparison.Ordinal))
        {
            uriBuilder.Path += "/";
        }

        return uriBuilder.Uri;
    }

    internal static string EscapePath(string path) =>
        string.Join('/', path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
}