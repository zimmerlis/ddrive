using System.ComponentModel;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using dDrive.Core.Connections;

namespace dDrive.Mounting;

public sealed class WindowsNativeMountService
{
    private const int ResourceTypeDisk = 0x00000001;
    private const int NoError = 0;

    public void Mount(ConnectionProfile connection, SecureString? secret)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.Protocol != ConnectionProtocol.WebDav)
        {
            throw new NotSupportedException("Native Windows-Laufwerks-Mounts werden nur für WebDAV unterstützt. Aktiviere die globale Rclone-Unterstützung für dieses Protokoll.");
        }

        var drive = $"{char.ToUpperInvariant(connection.DriveLetter)}:";
        var resource = new NetResource
        {
            ResourceType = ResourceTypeDisk,
            LocalName = drive,
            RemoteName = BuildWebDavPath(connection)
        };

        var password = SecureStringToString(secret);
        var result = WNetAddConnection2(ref resource, password, connection.Username, 0);
        if (result != NoError)
        {
            throw new Win32Exception(result, $"Windows konnte das WebDAV-Laufwerk {drive} nicht verbinden.");
        }

        SetExplorerLabel(resource.RemoteName!, connection.Name);
    }

    public void Unmount(char driveLetter)
    {
        var drive = $"{char.ToUpperInvariant(driveLetter)}:";
        var result = WNetCancelConnection2(drive, 0, true);
        if (result != NoError && result != 2250)
        {
            throw new Win32Exception(result, $"Windows konnte das Laufwerk {drive} nicht trennen.");
        }
    }

    public void DisconnectExistingMapping(char driveLetter)
    {
        var drive = $"{char.ToUpperInvariant(driveLetter)}:";
        var remoteName = new StringBuilder(1024);
        var length = remoteName.Capacity;
        var lookupResult = WNetGetConnection(drive, remoteName, ref length);
        if (lookupResult is 0)
        {
            var result = WNetCancelConnection2(drive, 0, true);
            if (result != NoError && result != 2250)
            {
                throw new Win32Exception(result, $"Windows konnte die bestehende Zuordnung von {drive} nicht trennen.");
            }
        }
    }

    private static string BuildWebDavPath(ConnectionProfile connection)
    {
        var host = NormalizeHost(connection.Host);

        var schemeMarker = connection.UseTls ? "@SSL" : string.Empty;
        var defaultPort = connection.UseTls ? 443 : 80;
        var portMarker = connection.Port == defaultPort ? string.Empty : $"@{connection.Port}";
        var path = connection.RemotePath.Replace('/', '\\').Trim('\\');
        return string.IsNullOrEmpty(path)
            ? $"\\\\{host}{schemeMarker}{portMarker}\\DavWWWRoot"
            : $"\\\\{host}{schemeMarker}{portMarker}\\DavWWWRoot\\{path}";
    }

    internal static string NormalizeHost(string value)
    {
        var host = value.Trim();
        host = host.Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("http://", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("https//", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("http//", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim('/');
        return host;
    }

    private static void SetExplorerLabel(string remoteName, string label)
    {
        var mountPointKey = remoteName.Replace('\\', '#');
        using var key = Registry.CurrentUser.CreateSubKey(
            $"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\MountPoints2\\{mountPointKey}",
            writable: true);
        key?.SetValue("_LabelFromReg", label, RegistryValueKind.String);
        SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    }

    private static string? SecureStringToString(SecureString? secret)
    {
        if (secret is null || secret.Length == 0)
        {
            return null;
        }

        var pointer = Marshal.SecureStringToBSTR(secret);
        try
        {
            return Marshal.PtrToStringBSTR(pointer);
        }
        finally
        {
            Marshal.ZeroFreeBSTR(pointer);
        }
    }

    [DllImport("mpr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int WNetAddConnection2(ref NetResource netResource, string? password, string? username, int flags);

    [DllImport("mpr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int WNetCancelConnection2(string name, int flags, bool force);

    [DllImport("mpr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int WNetGetConnection(string localName, StringBuilder remoteName, ref int length);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NetResource
    {
        public int Scope;
        public int ResourceType;
        public int DisplayType;
        public int Usage;
        public string? LocalName;
        public string? RemoteName;
        public string? Comment;
        public string? Provider;
    }
}
