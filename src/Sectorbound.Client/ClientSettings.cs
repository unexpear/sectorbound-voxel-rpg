using System.Text.Json;
using Sectorbound.Engine;

namespace Sectorbound.Client;

internal sealed class ClientSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public bool InvertMouseY { get; set; } = true;

    public static string GetDefaultPath() =>
        Path.Combine(SectorboundUserData.GetDataRoot(), "Client", "settings.json");

    public static ClientSettings Load(string path)
    {
        if (!File.Exists(path))
        {
            return new ClientSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<ClientSettings>(File.ReadAllText(path), JsonOptions)
                ?? new ClientSettings();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            ClientDiagnostics.WriteException("settings-load", exception);
            return new ClientSettings();
        }
    }

    public bool TrySave(string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            ClientDiagnostics.WriteException("settings-save", exception);
            return false;
        }
    }
}
