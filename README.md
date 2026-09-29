# dDrive

dDrive is a Windows tray application for managing WebDAV and FTP connections as drive-oriented remote storage. It provides a native Windows WebDAV mount by default and can optionally use Rclone with WinFsp for WebDAV, FTP, SFTP, and SMB mounts.

## Features

- WPF tray application for Windows 10 and 11
- Connection profiles for WebDAV and FTP by default
- Optional Rclone support for WebDAV, FTP, SFTP, and SMB
- Native Windows WebDAV mounting without an additional filesystem runtime
- Persistent Rclone mounts that survive dDrive shutdown and are restored on the next start
- Windows Credential Manager for passwords
- JSON configuration under `%APPDATA%\\dDrive\\connections.json`
- Import and export without passwords
- Directory and metadata caching with limited transport retries
- Dark/light theme, global startup and Rclone settings
- English, Mandarin Chinese, Hindi, Spanish, and French UI selection
- About dialog with project link and Buy Me a Coffee QR code
- Portable self-contained Windows x64 single-file publishing

## Mounting

Native mode uses the Windows WebClient service and the Windows network-drive API for WebDAV. The Windows WebClient service must be available for native WebDAV mounts.

When global Rclone support is enabled, dDrive installs Rclone and WinFsp through WinGet after explicit user consent:

```powershell
winget install --id Rclone.Rclone -e --accept-source-agreements --accept-package-agreements
winget install --id WinFsp.WinFsp -e --accept-source-agreements --accept-package-agreements
```

Rclone credentials are supplied at runtime. Passwords remain in Windows Credential Manager and are not written to the Rclone configuration file. An Rclone mount remains active when dDrive exits; explicitly disconnecting the profile terminates it and removes its temporary session data.

## Configuration and Logs

Connection settings and global preferences are stored at:

```text
%APPDATA%\\dDrive\\connections.json
```

Application logs are stored at:

```text
%LOCALAPPDATA%\\dDrive\\logs\\dDrive.jsonl
```

Rclone session metadata and diagnostics are stored temporarily at:

```text
%LOCALAPPDATA%\\dDrive\\rclone\\
```

No original passwords are stored in these files.

## Build

The repository contains a repeatable portable build command. Run `build.cmd` from the repository root. It increments the patch version, publishes a self-contained single-file Windows x64 executable, removes PDB files from the output, and writes the result to:

```text
artifacts\\publish\\portable-win-x64-profile\\dDrive.App.exe
```

The portable executable includes the .NET runtime. Native WebDAV still requires the Windows WebClient service, while Rclone mode requires Rclone and WinFsp.

## Development

```powershell
dotnet build dDrive.sln
dotnet test dDrive.sln
```

The solution targets .NET 8 and contains the application, core, storage, mounting, and test projects.
