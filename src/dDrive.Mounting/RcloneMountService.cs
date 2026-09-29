using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Security;
using dDrive.Core.Connections;

namespace dDrive.Mounting;

public sealed class RcloneMountService
{
    public async Task<RcloneMountSession> MountAsync(ConnectionProfile connection, SecureString? secret, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var executable = RcloneRuntimeInstaller.ResolveRcloneExecutable()
            ?? throw new InvalidOperationException("Rclone ist nicht installiert. Installiere zuerst Rclone und WinFsp.");
        if (RcloneRuntimeInstaller.ResolveWinFspInstallDirectory() is null)
        {
            throw new InvalidOperationException("WinFsp ist nicht installiert. Installiere zuerst Rclone und WinFsp.");
        }

        var password = SecureStringToString(secret);
        if (string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException("Für den Rclone-Mount wird ein Passwort benötigt.");
        }

        var configDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "dDrive",
            "rclone");
        Directory.CreateDirectory(configDirectory);
        var configPath = Path.Combine(configDirectory, $"{connection.Id:N}.conf");
        var logPath = Path.Combine(configDirectory, $"{connection.Id:N}.log");
        var sessionPath = Path.Combine(configDirectory, $"{connection.Id:N}.session");
        var remoteName = $"ddrive_{connection.Id:N}";
        try
        {
            var obscuredPassword = await ObscurePasswordAsync(executable, password, cancellationToken);
            TryDelete(configPath);
            var configArguments = BuildConfigArguments(connection, remoteName, configPath);
            await RunAsync(executable, configArguments, cancellationToken);

            var mountArguments = BuildMountArguments(connection, remoteName, configPath, logPath);
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = configDirectory
            };
            startInfo.Environment[$"RCLONE_CONFIG_{remoteName.ToUpperInvariant()}_PASS"] = obscuredPassword;
            foreach (var argument in mountArguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Rclone konnte nicht gestartet werden.");
            await Task.Delay(1000, cancellationToken);
            if (process.HasExited)
            {
                var exitCode = process.ExitCode;
                process.Dispose();
                throw new InvalidOperationException($"Rclone konnte das Laufwerk nicht einbinden (Exitcode {exitCode}). {ReadLog(logPath)}");
            }

            if (!await WaitForDriveAsync(connection.DriveLetter, process, cancellationToken))
            {
                var diagnostics = ReadLog(logPath);
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(cancellationToken);
                process.Dispose();
                throw new InvalidOperationException($"Rclone hat das Laufwerk nicht bereitgestellt. {diagnostics}");
            }

            await File.WriteAllTextAsync(sessionPath, process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), cancellationToken);
            return new RcloneMountSession(process, configPath, logPath, sessionPath);
        }
        catch
        {
            TryDelete(configPath);
            TryDelete(sessionPath);
            throw;
        }
    }

    public async Task<RcloneMountSession?> RestoreAsync(ConnectionProfile connection, CancellationToken cancellationToken = default)
    {
        var configDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "dDrive",
            "rclone");
        var configPath = Path.Combine(configDirectory, $"{connection.Id:N}.conf");
        var logPath = Path.Combine(configDirectory, $"{connection.Id:N}.log");
        var sessionPath = Path.Combine(configDirectory, $"{connection.Id:N}.session");
        if (!File.Exists(sessionPath))
        {
            return null;
        }

        try
        {
            var pidText = await File.ReadAllTextAsync(sessionPath, cancellationToken);
            if (!int.TryParse(pidText, out var processId))
            {
                throw new InvalidOperationException("Die gespeicherte Rclone-Prozesskennung ist ungültig.");
            }

            var process = Process.GetProcessById(processId);
            if (process.HasExited || !await WaitForDriveAsync(connection.DriveLetter, process, cancellationToken))
            {
                process.Dispose();
                throw new InvalidOperationException("Der gespeicherte Rclone-Mount ist nicht mehr aktiv.");
            }

            return new RcloneMountSession(process, configPath, logPath, sessionPath);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception)
        {
            TryDelete(sessionPath);
            return null;
        }
    }

    private static IReadOnlyList<string> BuildConfigArguments(ConnectionProfile connection, string remoteName, string configPath)
    {
        var arguments = new List<string> { "--config", configPath, "config", "create", remoteName };
        if (connection.Protocol == ConnectionProtocol.WebDav)
        {
            arguments.AddRange([
                "webdav",
                $"url={BuildBaseUrl(connection)}",
                "vendor=other",
                $"user={connection.Username}"]);
        }
        else if (connection.Protocol == ConnectionProtocol.Ftp)
        {
            arguments.AddRange([
                "ftp",
                $"host={connection.Host}",
                $"port={connection.Port}",
                $"user={connection.Username}",
                $"tls={connection.UseTls}",
                "explicit_tls=true"]);
        }
        else if (connection.Protocol == ConnectionProtocol.Sftp)
        {
            arguments.AddRange([
                "sftp",
                $"host={connection.Host}",
                $"port={connection.Port}",
                $"user={connection.Username}"]);
        }
        else
        {
            arguments.AddRange([
                "smb",
                $"host={connection.Host}",
                $"user={connection.Username}"]);
        }

        if (!connection.VerifyTlsCertificate)
        {
            arguments.Add("no_check_certificate=true");
        }

        return arguments;
    }

    private static IReadOnlyList<string> BuildMountArguments(ConnectionProfile connection, string remoteName, string configPath, string logPath)
    {
        var remotePath = connection.RemotePath.Trim('/');
        var source = string.IsNullOrEmpty(remotePath) ? $"{remoteName}:" : $"{remoteName}:{remotePath}";
        var arguments = new List<string>
        {
            "mount",
            source,
            $"{char.ToUpperInvariant(connection.DriveLetter)}:",
            "--config",
            configPath,
            "--network-mode",
            "--vfs-cache-mode",
            "full",
            "--dir-cache-time",
            "1m",
            "--volname",
            connection.Name,
            "--log-level",
            "DEBUG",
            "--log-file",
            logPath
        };
        if (connection.ReadOnly)
        {
            arguments.Add("--read-only");
        }

        return arguments;
    }

    private static async Task<bool> WaitForDriveAsync(char driveLetter, Process process, CancellationToken cancellationToken)
    {
        var drive = $"{char.ToUpperInvariant(driveLetter)}:\\";
        for (var attempt = 0; attempt < 30; attempt++)
        {
            if (process.HasExited)
            {
                return false;
            }

            try
            {
                if (new DriveInfo(drive).IsReady)
                {
                    return true;
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            await Task.Delay(500, cancellationToken);
        }

        return false;
    }

    private static string ReadLog(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : "Keine Rclone-Diagnose verfügbar.";
        }
        catch (IOException)
        {
            return "Die Rclone-Diagnose konnte nicht gelesen werden.";
        }
    }

    private static string BuildBaseUrl(ConnectionProfile connection)
    {
        var scheme = connection.UseTls ? "https" : "http";
        return $"{scheme}://{WindowsNativeMountService.NormalizeHost(connection.Host)}:{connection.Port}";
    }

    private static async Task<string> ObscurePasswordAsync(string executable, string password, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("obscure");
        startInfo.ArgumentList.Add(password);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Rclone konnte das Passwort nicht verschlüsseln.");
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException($"Rclone konnte das Passwort nicht verschlüsseln: {error.Trim()}");
        }

        return output.Trim();
    }

    private static async Task RunAsync(string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Rclone konnte nicht gestartet werden.");
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var error = await errorTask;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Rclone konnte die Konfiguration nicht erstellen: {error.Trim()}");
        }
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

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }

    }
}

public sealed class RcloneMountSession(Process process, string configPath, string logPath, string sessionPath) : IAsyncDisposable
{
    public ValueTask DetachAsync()
    {
        process.Dispose();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }

        process.Dispose();
        try
        {
            if (File.Exists(configPath))
            {
                File.Delete(configPath);
            }
        }
        catch
        {
        }

        try
        {
            if (File.Exists(logPath))
            {
                File.Delete(logPath);
            }
        }
        catch
        {
        }

        try
        {
            if (File.Exists(sessionPath))
            {
                File.Delete(sessionPath);
            }
        }
        catch
        {
        }
    }
}
