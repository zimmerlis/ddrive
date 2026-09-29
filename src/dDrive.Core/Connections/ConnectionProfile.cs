namespace dDrive.Core.Connections;

public sealed record ConnectionProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
    public ConnectionProtocol Protocol { get; init; } = ConnectionProtocol.WebDav;
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 443;
    public string RemotePath { get; init; } = "/";
    public char DriveLetter { get; init; } = 'Z';
    public string Username { get; init; } = string.Empty;
    public string WinCredManagerEntry { get; init; } = string.Empty;
    public bool ReadOnly { get; init; }
    public bool AutoMount { get; init; }
    public bool UseTls { get; init; } = true;
    public bool VerifyTlsCertificate { get; init; } = true;
    public int TimeoutSeconds { get; init; } = 30;
}