using dDrive.Core.Connections;

namespace dDrive.Core.Configuration;

public sealed class AppConfiguration
{
    public int SchemaVersion { get; init; } = 1;
    public List<ConnectionProfile> Connections { get; init; } = [];
    public bool StartWithWindows { get; init; }
    public bool StartMinimized { get; init; }
    public bool RcloneEnabled { get; init; }
    public bool HasDonated { get; init; }
    public string Language { get; init; } = "English";
    public bool UseDarkMode { get; init; } = true;
    public string ColorScheme { get; init; } = "ocean";
    public int MobilePopupWidth { get; init; } = 600;
    public int MobilePopupHeight { get; init; } = 760;
    public string MobilePopupPosition { get; init; } = "bottomright";
}