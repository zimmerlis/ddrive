using System.Diagnostics;
using Microsoft.Win32;

namespace dDrive.Mounting;

public sealed class RcloneRuntimeInstaller
{
    public bool IsReady() => ResolveRcloneExecutable() is not null && ResolveWinFspInstallDirectory() is not null;

    public async Task InstallAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Rclone und WinFsp können nur unter Windows installiert werden.");
        }

        if (ResolveRcloneExecutable() is null)
        {
            await RunWingetAsync("Rclone.Rclone", cancellationToken);
        }

        if (ResolveWinFspInstallDirectory() is null)
        {
            await RunWingetAsync("WinFsp.WinFsp", cancellationToken);
        }
    }

    internal static string? ResolveRcloneExecutable()
    {
        var result = RunLocator("where.exe", "rclone.exe");
        var located = result.ExitCode == 0
            ? result.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(File.Exists)
            : null;
        if (located is not null)
        {
            return located;
        }

        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "rclone", "rclone.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "rclone", "rclone.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "rclone", "rclone.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scoop", "shims", "rclone.exe")
        };
        var candidate = candidates.FirstOrDefault(File.Exists);
        if (candidate is not null)
        {
            return candidate;
        }

        var wingetRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages");
        if (!Directory.Exists(wingetRoot))
        {
            return null;
        }

        try
        {
            return Directory.EnumerateFiles(wingetRoot, "rclone.exe", SearchOption.AllDirectories).FirstOrDefault();
        }
        catch (IOException)
        {
            return null;
        }
    }

    internal static string? ResolveWinFspInstallDirectory()
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32, RegistryView.Default })
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var key = baseKey.OpenSubKey("SOFTWARE\\WinFsp");
            var installDirectory = key?.GetValue("InstallDir") as string;
            if (!string.IsNullOrWhiteSpace(installDirectory) && Directory.Exists(installDirectory))
            {
                return installDirectory;
            }
        }

        var knownPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WinFsp"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "WinFsp")
        };
        return knownPaths.FirstOrDefault(Directory.Exists);
    }

    private static async Task RunWingetAsync(string packageId, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "winget.exe",
            UseShellExecute = true,
            Verb = "runas",
            CreateNoWindow = false
        };
        startInfo.ArgumentList.Add("install");
        startInfo.ArgumentList.Add("--id");
        startInfo.ArgumentList.Add(packageId);
        startInfo.ArgumentList.Add("-e");
        startInfo.ArgumentList.Add("--accept-source-agreements");
        startInfo.ArgumentList.Add("--accept-package-agreements");

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("WinGet konnte nicht gestartet werden.");
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"WinGet konnte {packageId} nicht installieren (Exitcode {process.ExitCode}).");
        }
    }

    private static (int ExitCode, string Output) RunLocator(string fileName, string argument)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = argument,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }
}
