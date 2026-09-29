using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace dDrive.Core.Configuration;

public sealed class ConfigurationStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _filePath;

    public ConfigurationStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "dDrive",
            "connections.json");
    }

    public Task<AppConfiguration> LoadAsync(CancellationToken cancellationToken = default) =>
        LoadFromFileAsync(_filePath, cancellationToken);

    public static async Task<AppConfiguration> LoadFromFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            return new AppConfiguration();
        }

        await using var stream = File.OpenRead(filePath);
        var document = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Die Konfigurationsdatei enthält kein JSON.");
        MigrateCredentialEntries(document);
        return document.Deserialize<AppConfiguration>(SerializerOptions) ?? new AppConfiguration();
    }

    private static void MigrateCredentialEntries(JsonNode document)
    {
        if (document["connections"] is not JsonArray connections)
        {
            return;
        }

        foreach (var connection in connections.OfType<JsonObject>())
        {
            if (connection["winCredManagerEntry"] is null && connection["credentialKey"] is JsonNode legacy)
            {
                connection["winCredManagerEntry"] = legacy.DeepClone();
            }

            connection.Remove("credentialKey");
            if (connection["winCredManagerEntry"] is null && connection["name"] is JsonValue name)
            {
                connection["winCredManagerEntry"] = $"dDrive:{name.GetValue<string>()}";
            }
        }
    }

    public Task SaveAsync(
        AppConfiguration configuration,
        CancellationToken cancellationToken = default) =>
        SaveToFileAsync(_filePath, configuration, cancellationToken);

    public static Task SaveToFileAsync(
        string filePath,
        AppConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return SaveJsonToFileAsync(filePath, configuration, cancellationToken);
    }

    private static async Task SaveJsonToFileAsync<T>(
        string filePath,
        T configuration,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    configuration,
                    SerializerOptions,
                    cancellationToken);
            }

            File.Move(temporaryPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static Task ExportToFileAsync(
        string filePath,
        AppConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var portable = new
        {
            configuration.SchemaVersion,
            configuration.StartWithWindows,
            Connections = configuration.Connections.Select(connection => new
            {
                connection.Id,
                connection.Name,
                connection.Protocol,
                connection.Host,
                connection.Port,
                connection.RemotePath,
                connection.DriveLetter,
                connection.Username,
                connection.ReadOnly,
                connection.AutoMount,
                connection.UseTls,
                connection.VerifyTlsCertificate,
                connection.TimeoutSeconds
            }).ToList()
        };

        return SaveJsonToFileAsync(filePath, portable, cancellationToken);
    }
}