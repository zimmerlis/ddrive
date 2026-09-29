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
}