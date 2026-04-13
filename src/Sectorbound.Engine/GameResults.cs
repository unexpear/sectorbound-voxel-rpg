namespace Sectorbound.Engine;

public sealed record BlockActionResult(bool Success, string Message, string MaterialId, string DropItemId, int DropCount);
public sealed record CraftResult(bool Success, string Message);
public sealed record CombatResult(bool Success, string Message, string EncounterId, IReadOnlyDictionary<string, int> Loot);
public sealed record SectorTransitionResult(int FromSectorIndex, int ToSectorIndex, string Message);
public sealed record DeathPenaltyResult(IReadOnlyDictionary<string, int> LostItems, int TotalSkillExperienceLost, bool CorpseRunEnabled);
public sealed record PlanetSummary(
    int PlanetIndex,
    string Name,
    string BiomeName,
    IReadOnlyList<string> SpawnTable,
    IReadOnlyList<string> StructureGenerators,
    IReadOnlyList<string> BiomeFeatureGenerators);
public sealed record SliceGoalProgress(string Id, string DisplayName, bool IsComplete, string Detail);
public readonly record struct SliceTouchLink(Int3 Position, string MaterialId, bool IsSolid);
public readonly record struct WorldGenSliceBlock(
    Int3 Position,
    string MaterialId,
    bool IsSolid,
    SliceTouchLink? PreviousSliceTouchingBlock);
public sealed record WorldGenSlice(
    int SliceIndex,
    int WorldX,
    int HeightInVoxels,
    int DepthInVoxels,
    IReadOnlyList<WorldGenSliceBlock> Blocks);
