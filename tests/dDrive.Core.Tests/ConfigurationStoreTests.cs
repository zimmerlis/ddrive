using dDrive.Core.Configuration;
using dDrive.Core.Connections;
using dDrive.Core.Diagnostics;
using dDrive.Core.Storage;
using Xunit;

namespace dDrive.Core.Tests;

public sealed class ConfigurationStoreTests
{
    [Fact]
    public async Task SaveAndLoad_PreservesConnectionSettingsWithoutSecrets()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"dDrive-tests-{Guid.NewGuid():N}");
        var filePath = Path.Combine(directory, "connections.json");
        var store = new ConfigurationStore(filePath);
        var configuration = new AppConfiguration
        {
            UseDarkMode = true,
            Connections =
            [
                new ConnectionProfile
                {
                    Name = "Home server",
                    Host = "files.example.test",
                    Username = "rene",
                    WinCredManagerEntry = "dDrive:Home server",
                    Protocol = ConnectionProtocol.WebDav
                }
            ]
        };

        try
        {
            await store.SaveAsync(configuration);
            var loaded = await store.LoadAsync();
            var json = await File.ReadAllTextAsync(filePath);

            Assert.Equal("Home server", Assert.Single(loaded.Connections).Name);
            Assert.True(loaded.UseDarkMode);
            Assert.Contains("winCredManagerEntry", json);
            Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LoadAsync_WhenFileDoesNotExist_ReturnsEmptyConfiguration()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"dDrive-{Guid.NewGuid():N}", "connections.json");
        var loaded = await new ConfigurationStore(filePath).LoadAsync();

        Assert.Empty(loaded.Connections);
        Assert.Equal(1, loaded.SchemaVersion);
    }

    [Fact]
    public async Task ExportAndImport_PreservesConnectionsWithoutExportingSecrets()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"dDrive-export-{Guid.NewGuid():N}.json");
        var configuration = new AppConfiguration
        {
            Connections =
            [
                new ConnectionProfile
                {
                    Name = "Portable profile",
                    Host = "storage.example.test",
                    WinCredManagerEntry = "dDrive:Portable profile",
                    Username = "user"
                }
            ]
        };

        try
        {
            await ConfigurationStore.ExportToFileAsync(filePath, configuration);
            var imported = await ConfigurationStore.LoadFromFileAsync(filePath);
            var json = await File.ReadAllTextAsync(filePath);

            Assert.Equal("Portable profile", Assert.Single(imported.Connections).Name);
            Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("credentialKey", json, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Theory]
    [InlineData("/", "/documents/report.txt", "/documents/report.txt")]
    [InlineData("/home/rene", "documents\\report.txt", "/home/rene/documents/report.txt")]
    [InlineData("/home/rene", "/documents/../photos", "/home/rene/photos")]
    public void StoragePath_ResolveKeepsPathsUnderRoot(string root, string path, string expected)
    {
        Assert.Equal(expected, StoragePath.Resolve(root, path));
    }

    [Theory]
    [InlineData("/", "../outside")]
    [InlineData("/home/rene", "../../outside")]
    public void StoragePath_ResolveRejectsTraversalOutsideRoot(string root, string path)
    {
        Assert.Throws<ArgumentException>(() => StoragePath.Resolve(root, path));
    }

    [Fact]
    public void FileAppLogger_WritesStructuredEventsWithoutCredentialFields()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"dDrive-logs-{Guid.NewGuid():N}");
        var filePath = Path.Combine(directory, "app.jsonl");

        try
        {
            var logger = new FileAppLogger(filePath);
            logger.LogError("Connection.Mount", "Mount failed", new IOException("Remote unavailable"));
            var line = File.ReadAllText(filePath);

            Assert.Contains("Connection.Mount", line);
            Assert.Contains("Remote unavailable", line);
            Assert.DoesNotContain("password", line, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("credentialKey", line, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}