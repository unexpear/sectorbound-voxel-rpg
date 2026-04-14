using System.Collections.ObjectModel;

namespace Sectorbound.Engine;

public enum TransitionPhase
{
    Idle,
    Generating,
    Activating
}

public enum CombatApproach
{
    Blade,
    Bow,
    Sigil
}

public readonly record struct Int3(int X, int Y, int Z)
{
    public override string ToString() => $"({X}, {Y}, {Z})";
}

public readonly record struct ChunkCoordinate(int X, int Y, int Z)
{
    public override string ToString() => $"[{X}, {Y}, {Z}]";
}

public sealed record TransitionState(
    TransitionPhase Phase,
    int FromSectorIndex,
    int ToSectorIndex,
    string Message,
    DateTimeOffset StartedAtUtc);

public sealed record MaterialDef(
    string Id,
    string DisplayName,
    bool IsSolid,
    string DropItemId,
    int DropCount,
    int Hardness,
    int Tier);

public sealed record RecipeDef(
    string Id,
    string DisplayName,
    IReadOnlyDictionary<string, int> Inputs,
    IReadOnlyDictionary<string, int> Outputs,
    string? RequiredStationMaterialId,
    string SkillId,
    int SkillGain);

public sealed record ActorScaleDef(
    float HeightMeters,
    float WidthMeters,
    float DepthMeters);

public sealed record EnemyDef(
    string Id,
    string DisplayName,
    string Family,
    int Danger,
    int Health,
    int Damage,
    IReadOnlyDictionary<string, int> Loot,
    int SkillGain,
    ActorScaleDef Scale);

public sealed record BossDef(
    string Id,
    string DisplayName,
    int Danger,
    int Health,
    int Damage,
    IReadOnlyDictionary<string, int> Rewards,
    IReadOnlyDictionary<string, int> BonusXpBySkill,
    string BonusPerkId,
    ActorScaleDef Scale);

public sealed record SkillDef(
    string Id,
    string DisplayName,
    string Description);

public sealed record PerkDef(
    string Id,
    string DisplayName,
    string Description,
    string? UnlockSkillId,
    int RequiredLevel);

public sealed record DifficultyDef(
    string Id,
    string DisplayName,
    double InventoryDropPercent,
    double SkillPenaltyPercent,
    bool CorpseRun);

public sealed record BiomeDef(
    string Id,
    string DisplayName,
    string SurfaceMaterialId,
    string SubsurfaceMaterialId,
    string CoreMaterialId,
    IReadOnlyList<string> EnemyIds,
    IReadOnlyList<string>? FeatureGenerators = null,
    double Heat = 0.5,
    double Moisture = 0.5,
    double Relief = 0.5);

public sealed record LandformDef(
    string Id,
    string DisplayName,
    IReadOnlyList<string> StructureGenerators,
    int HeightBias,
    double CaveDensity,
    int BaseHeight = 18,
    int LargeFeatureAmplitude = 8,
    double LargeFeatureScale = 0.04,
    int RidgeAmplitude = 5,
    double RidgeScale = 0.08,
    int DetailAmplitude = 2,
    double DetailScale = 0.16,
    double CaveScale = 0.12);

public sealed record PlanetArchetypeDef(
    string Id,
    string DisplayName,
    string BiomeId,
    string LandformId,
    string EnemyFocusId,
    string BossId,
    string SecondaryBiomeId = "",
    string SecondaryLandformId = "");

public sealed record PlanetLandmark(
    string StructureId,
    int OriginX,
    int OriginZ,
    int RadiusInOriginalBlocks);

public readonly record struct BiomeFeatureSite(
    string FeatureId,
    int OriginX,
    int OriginZ,
    int RadiusInOriginalBlocks);

public readonly record struct ScenicPropInstance(
    string AssetId,
    Int3 AnchorVoxel,
    Int3 FacingDirection);

public sealed class ContentDefs
{
    private readonly ReadOnlyDictionary<string, MaterialDef> _materials;
    private readonly ReadOnlyDictionary<string, RecipeDef> _recipes;
    private readonly ReadOnlyDictionary<string, EnemyDef> _enemies;
    private readonly ReadOnlyDictionary<string, BossDef> _bosses;
    private readonly ReadOnlyDictionary<string, SkillDef> _skills;
    private readonly ReadOnlyDictionary<string, PerkDef> _perks;
    private readonly ReadOnlyDictionary<string, DifficultyDef> _difficulties;
    private readonly ReadOnlyDictionary<string, BiomeDef> _biomes;
    private readonly ReadOnlyDictionary<string, LandformDef> _landforms;
    private readonly ReadOnlyDictionary<string, PlanetArchetypeDef> _planetArchetypes;

    public ContentDefs(
        IEnumerable<MaterialDef> materials,
        IEnumerable<RecipeDef> recipes,
        IEnumerable<EnemyDef> enemies,
        IEnumerable<BossDef> bosses,
        IEnumerable<SkillDef> skills,
        IEnumerable<PerkDef> perks,
        IEnumerable<DifficultyDef> difficulties,
        IEnumerable<BiomeDef> biomes,
        IEnumerable<LandformDef> landforms,
        IEnumerable<PlanetArchetypeDef> planetArchetypes)
    {
        _materials = ToReadOnlyLookup(materials, x => x.Id);
        _recipes = ToReadOnlyLookup(recipes, x => x.Id);
        _enemies = ToReadOnlyLookup(enemies, x => x.Id);
        _bosses = ToReadOnlyLookup(bosses, x => x.Id);
        _skills = ToReadOnlyLookup(skills, x => x.Id);
        _perks = ToReadOnlyLookup(perks, x => x.Id);
        _difficulties = ToReadOnlyLookup(difficulties, x => x.Id);
        _biomes = ToReadOnlyLookup(biomes, x => x.Id);
        _landforms = ToReadOnlyLookup(landforms, x => x.Id);
        _planetArchetypes = ToReadOnlyLookup(planetArchetypes, x => x.Id);
    }

    public IReadOnlyDictionary<string, MaterialDef> Materials => _materials;
    public IReadOnlyDictionary<string, RecipeDef> Recipes => _recipes;
    public IReadOnlyDictionary<string, EnemyDef> Enemies => _enemies;
    public IReadOnlyDictionary<string, BossDef> Bosses => _bosses;
    public IReadOnlyDictionary<string, SkillDef> Skills => _skills;
    public IReadOnlyDictionary<string, PerkDef> Perks => _perks;
    public IReadOnlyDictionary<string, DifficultyDef> Difficulties => _difficulties;
    public IReadOnlyDictionary<string, BiomeDef> Biomes => _biomes;
    public IReadOnlyDictionary<string, LandformDef> Landforms => _landforms;
    public IReadOnlyDictionary<string, PlanetArchetypeDef> PlanetArchetypes => _planetArchetypes;

    public MaterialDef GetMaterial(string id) => Lookup(_materials, id, nameof(Materials));
    public RecipeDef GetRecipe(string id) => Lookup(_recipes, id, nameof(Recipes));
    public EnemyDef GetEnemy(string id) => Lookup(_enemies, id, nameof(Enemies));
    public BossDef GetBoss(string id) => Lookup(_bosses, id, nameof(Bosses));
    public SkillDef GetSkill(string id) => Lookup(_skills, id, nameof(Skills));
    public DifficultyDef GetDifficulty(string id) => Lookup(_difficulties, id, nameof(Difficulties));
    public BiomeDef GetBiome(string id) => Lookup(_biomes, id, nameof(Biomes));
    public LandformDef GetLandform(string id) => Lookup(_landforms, id, nameof(Landforms));
    public PlanetArchetypeDef GetPlanetArchetype(string id) => Lookup(_planetArchetypes, id, nameof(PlanetArchetypes));

    private static ReadOnlyDictionary<string, TItem> ToReadOnlyLookup<TItem>(
        IEnumerable<TItem> items,
        Func<TItem, string> keySelector)
        where TItem : notnull
    {
        return new ReadOnlyDictionary<string, TItem>(
            items.ToDictionary(keySelector, StringComparer.OrdinalIgnoreCase));
    }

    private static TItem Lookup<TItem>(IReadOnlyDictionary<string, TItem> dictionary, string id, string collectionName)
    {
        if (dictionary.TryGetValue(id, out var value))
        {
            return value;
        }

        throw new KeyNotFoundException($"'{id}' was not found in {collectionName}.");
    }
}

public sealed class SkillProgress
{
    public int Level { get; set; } = 1;
    public int Experience { get; set; }
    public int LifetimeExperience { get; set; }
}

public sealed class EquippedLoadout
{
    public string PrimaryWeaponId { get; set; } = "rust-cutter";
    public string SecondaryWeaponId { get; set; } = "flare-bow";
    public string UtilityAbilityId { get; set; } = "rift-sigil";
}

public sealed class PlayerProfile
{
    public Dictionary<string, int> Inventory { get; } = new(StringComparer.OrdinalIgnoreCase);
    public EquippedLoadout EquippedLoadout { get; set; } = new();
    public Dictionary<string, SkillProgress> SkillLevels { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> PerkUnlocks { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> BossBonuses { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> CraftedRecipes { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> DefeatedEnemyFamilies { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> MinedMaterialIds { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> PlacedMaterialCounts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int SectorTransitionsCompleted { get; set; }
    public string DifficultyPresetId { get; set; } = "frontier";
}

public sealed class ChunkData
{
    public ChunkData(ChunkCoordinate coordinate)
    {
        Coordinate = coordinate;
    }

    public ChunkCoordinate Coordinate { get; }
    public Dictionary<Int3, string> VoxelMaterials { get; } = [];
    public Dictionary<Int3, string> DeformationDeltas { get; } = [];
    public Dictionary<Int3, string> PlacedBlocks { get; } = [];
    public bool IsDirty { get; set; }
    public bool IsPersisted { get; set; }
}

public sealed class PlanetRuntime
{
    public PlanetRuntime(
        int planetIndex,
        string archetypeId,
        string displayName,
        long planetSeed,
        BiomeDef biomeRules,
        BiomeDef secondaryBiomeRules,
        LandformDef landformRules,
        LandformDef secondaryLandformRules,
        IEnumerable<string> spawnTables,
        IEnumerable<PlanetLandmark> landmarks)
    {
        PlanetIndex = planetIndex;
        ArchetypeId = archetypeId;
        DisplayName = displayName;
        PlanetSeed = planetSeed;
        BiomeRules = biomeRules;
        SecondaryBiomeRules = secondaryBiomeRules;
        LandformRules = landformRules;
        SecondaryLandformRules = secondaryLandformRules;
        SpawnTables = spawnTables.ToArray();
        Landmarks = landmarks.ToArray();
        StructureGenerators = landformRules.StructureGenerators
            .Concat(secondaryLandformRules.StructureGenerators)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        BiomeFeatureGenerators = (biomeRules.FeatureGenerators ?? [])
            .Concat(secondaryBiomeRules.FeatureGenerators ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public int PlanetIndex { get; }
    public string ArchetypeId { get; }
    public string DisplayName { get; }
    public long PlanetSeed { get; }
    public BiomeDef BiomeRules { get; }
    public BiomeDef SecondaryBiomeRules { get; }
    public LandformDef LandformRules { get; }
    public LandformDef SecondaryLandformRules { get; }
    public Dictionary<ChunkCoordinate, ChunkData> ChunkRegistry { get; } = [];
    public IReadOnlyList<string> SpawnTables { get; }
    public IReadOnlyList<PlanetLandmark> Landmarks { get; }
    public IReadOnlyList<string> StructureGenerators { get; }
    public IReadOnlyList<string> BiomeFeatureGenerators { get; }
}

public sealed class SectorRuntime
{
    public SectorRuntime(
        int sectorIndex,
        long activeSectorSeed,
        IEnumerable<PlanetRuntime> loadedPlanets,
        TransitionState transitionState,
        int streamingBudget,
        string sectorBossId)
    {
        SectorIndex = sectorIndex;
        ActiveSectorSeed = activeSectorSeed;
        LoadedPlanets = loadedPlanets.ToList();
        TransitionState = transitionState;
        StreamingBudget = streamingBudget;
        SectorBossId = sectorBossId;
    }

    public int SectorIndex { get; }
    public long ActiveSectorSeed { get; }
    public List<PlanetRuntime> LoadedPlanets { get; }
    public TransitionState TransitionState { get; set; }
    public int StreamingBudget { get; }
    public string SectorBossId { get; }
}
