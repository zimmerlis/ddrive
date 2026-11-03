using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace dDrive.App;

public static class DriveIconRegistry
{
    private const string DriveIconsPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\DriveIcons";
    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    public static void Set(char driveLetter, string iconPath)
    {
        using var key = Registry.CurrentUser.CreateSubKey($@"{DriveIconsPath}\{char.ToUpperInvariant(driveLetter)}\DefaultIcon", writable: true);
        key.SetValue(string.Empty, iconPath);
        Notify();
    }

    public static void Remove(char driveLetter)
    {
        using var key = Registry.CurrentUser.OpenSubKey(DriveIconsPath, writable: true);
        key?.DeleteSubKeyTree(char.ToUpperInvariant(driveLetter).ToString(), throwOnMissingSubKey: false);
        Notify();
    }

    private static void Notify() => SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
}
