namespace Sectorbound.Client;

internal readonly record struct CharacterSelection(string BodyId, string HairId);

internal readonly record struct CharacterOption(string Id, string DisplayName);

internal static class CharacterCatalog
{
    public const string DefaultBodyId = "male";
    public const string DefaultHairId = "buzzed";

    public static readonly CharacterSelection DefaultSelection = new(DefaultBodyId, DefaultHairId);

    public static IReadOnlyList<CharacterOption> Bodies { get; } =
    [
        new CharacterOption("male", "Male Frame"),
        new CharacterOption("female", "Female Frame")
    ];

    public static IReadOnlyList<CharacterOption> Hairstyles { get; } =
    [
        new CharacterOption("buzzed", "Buzzed"),
        new CharacterOption("parted", "Parted"),
        new CharacterOption("buns", "Buns")
    ];

    public static CharacterSelection Normalize(string? bodyId, string? hairId)
    {
        var normalizedBody = Bodies.Any(option => string.Equals(option.Id, bodyId, StringComparison.OrdinalIgnoreCase))
            ? bodyId!.ToLowerInvariant()
            : DefaultBodyId;
        var normalizedHair = Hairstyles.Any(option => string.Equals(option.Id, hairId, StringComparison.OrdinalIgnoreCase))
            ? hairId!.ToLowerInvariant()
            : DefaultHairId;
        return new CharacterSelection(normalizedBody, normalizedHair);
    }

    public static CharacterOption GetBody(string id) =>
        Bodies.FirstOrDefault(option => string.Equals(option.Id, id, StringComparison.OrdinalIgnoreCase))
            is var option && !string.IsNullOrWhiteSpace(option.Id)
                ? option
                : Bodies[0];

    public static CharacterOption GetHair(string id) =>
        Hairstyles.FirstOrDefault(option => string.Equals(option.Id, id, StringComparison.OrdinalIgnoreCase))
            is var option && !string.IsNullOrWhiteSpace(option.Id)
                ? option
                : Hairstyles[0];

    public static string GetDisplayName(CharacterSelection selection)
    {
        var body = GetBody(selection.BodyId);
        var hair = GetHair(selection.HairId);
        return $"{body.DisplayName}, {hair.DisplayName}";
    }

    public static string GetRelativeAssetPath(CharacterSelection selection)
    {
        var normalized = Normalize(selection.BodyId, selection.HairId);
        return Path.Combine("Content", "Voxel", "Characters", $"{normalized.BodyId}-{normalized.HairId}.vox");
    }
}
