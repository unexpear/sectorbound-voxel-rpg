namespace Sectorbound.Client;

internal sealed record ScenicPropDefinition(
    string Id,
    string RelativePath,
    float HeightOriginalBlocks);

internal static class ScenicPropCatalog
{
    private static readonly IReadOnlyDictionary<string, ScenicPropDefinition> Definitions =
        new Dictionary<string, ScenicPropDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["nature-bamboo"] = new(
                "nature-bamboo",
                @"Content\Voxel\Props\NatureClassic\bamboo.vox",
                5.0f),
            ["nature-tree-stump-round"] = new(
                "nature-tree-stump-round",
                @"Content\Voxel\Props\NatureClassic\treeStump_round.vox",
                2.0f),
            ["nature-rock-tall4"] = new(
                "nature-rock-tall4",
                @"Content\Voxel\Props\NatureClassic\rock_tall4.vox",
                4.25f),
            ["nature-rock-large4"] = new(
                "nature-rock-large4",
                @"Content\Voxel\Props\NatureClassic\rock_large4.vox",
                3.25f),
            ["nature-stone-obelisk"] = new(
                "nature-stone-obelisk",
                @"Content\Voxel\Props\NatureClassic\stone_obelisk.vox",
                5.25f),
            ["nature-stone-column"] = new(
                "nature-stone-column",
                @"Content\Voxel\Props\NatureClassic\stone_column.vox",
                4.5f)
        };

    public static bool TryGetDefinition(string id, out ScenicPropDefinition? definition) =>
        Definitions.TryGetValue(id, out definition);

    public static ScenicPropDefinition GetDefinition(string id) =>
        Definitions.TryGetValue(id, out var definition)
            ? definition
            : throw new KeyNotFoundException($"'{id}' is not a registered scenic prop.");
}
