using System.Text.Json;
using Sectorbound.Engine;

namespace Sectorbound.Client;

internal sealed class ClientSettings
{
    public const string DefaultDifficultyPresetId = "frontier";
    public const float DefaultMouseSensitivity = 0.012f;
    public const float MinMouseSensitivity = 0.004f;
    public const float MaxMouseSensitivity = 0.030f;
    public const float MouseSensitivityStep = 0.002f;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public const int DefaultViewDistanceBlocks = 40;
    public const int MinViewDistanceBlocks = 8;
    public const int MaxViewDistanceBlocks = 80;

    private float _mouseSensitivity = DefaultMouseSensitivity;

    public bool InvertMouseY { get; set; } = true;
    public int ViewDistanceBlocks { get; set; } = DefaultViewDistanceBlocks;
    public string SelectedCharacterBodyId { get; set; } = CharacterCatalog.DefaultBodyId;
    public string SelectedCharacterHairId { get; set; } = CharacterCatalog.DefaultHairId;
    public string PreferredDifficultyPresetId { get; set; } = DefaultDifficultyPresetId;
    public float MouseSensitivity
    {
        get => _mouseSensitivity;
        set => _mouseSensitivity = NormalizeMouseSensitivity(value);
    }

    public static string GetDefaultPath() =>
        Path.Combine(SectorboundUserData.GetDataRoot(), "Client", "settings.json");

    public static float NormalizeMouseSensitivity(float value) =>
        float.IsFinite(value)
            ? Math.Clamp(value, MinMouseSensitivity, MaxMouseSensitivity)
            : DefaultMouseSensitivity;

    public static string NormalizeDifficultyPresetId(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? DefaultDifficultyPresetId
            : value.Trim().ToLowerInvariant();

    public static ClientSettings Load(string path)
    {
        if (!File.Exists(path))
        {
            return new ClientSettings();
        }

        try
        {
            var settings = JsonSerializer.Deserialize<ClientSettings>(File.ReadAllText(path), JsonOptions)
                ?? new ClientSettings();
            settings.Normalize();
            return settings;
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
            Normalize();
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

    public CharacterSelection GetCharacterSelection() =>
        CharacterCatalog.Normalize(SelectedCharacterBodyId, SelectedCharacterHairId);

    public string GetPreferredDifficultyPresetId() =>
        NormalizeDifficultyPresetId(PreferredDifficultyPresetId);

    public void SetCharacterSelection(CharacterSelection selection)
    {
        var normalized = CharacterCatalog.Normalize(selection.BodyId, selection.HairId);
        SelectedCharacterBodyId = normalized.BodyId;
        SelectedCharacterHairId = normalized.HairId;
    }

    public void SetPreferredDifficultyPreset(string? difficultyPresetId) =>
        PreferredDifficultyPresetId = NormalizeDifficultyPresetId(difficultyPresetId);

    private void Normalize()
    {
        MouseSensitivity = _mouseSensitivity;
        ViewDistanceBlocks = Math.Clamp(ViewDistanceBlocks, MinViewDistanceBlocks, MaxViewDistanceBlocks);
        SetCharacterSelection(GetCharacterSelection());
        SetPreferredDifficultyPreset(PreferredDifficultyPresetId);
    }
}
