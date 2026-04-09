using System.Text.Json;

namespace Sectorbound.Engine;

public static class ContentLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static ContentDefs LoadFromDirectory(string contentRoot)
    {
        var contentPath = Path.Combine(contentRoot, "content.json");
        if (!File.Exists(contentPath))
        {
            throw new FileNotFoundException("Could not locate content.json.", contentPath);
        }

        return LoadFromJson(File.ReadAllText(contentPath));
    }

    public static ContentDefs LoadFromJson(string json)
    {
        var package = JsonSerializer.Deserialize<ContentPackage>(json, JsonOptions)
            ?? throw new InvalidOperationException("The content package could not be deserialized.");

        return new ContentDefs(
            package.Materials ?? [],
            package.Recipes?.Select(x => x.ToRuntime()) ?? [],
            package.Enemies?.Select(x => x.ToRuntime()) ?? [],
            package.Bosses?.Select(x => x.ToRuntime()) ?? [],
            package.Skills ?? [],
            package.Perks ?? [],
            package.Difficulties ?? [],
            package.Biomes ?? [],
            package.PlanetArchetypes ?? []);
    }

    private sealed class ContentPackage
    {
        public List<MaterialDef>? Materials { get; set; }
        public List<RecipeData>? Recipes { get; set; }
        public List<EnemyData>? Enemies { get; set; }
        public List<BossData>? Bosses { get; set; }
        public List<SkillDef>? Skills { get; set; }
        public List<PerkDef>? Perks { get; set; }
        public List<DifficultyDef>? Difficulties { get; set; }
        public List<BiomeDef>? Biomes { get; set; }
        public List<PlanetArchetypeDef>? PlanetArchetypes { get; set; }
    }

    private sealed class RecipeData
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public Dictionary<string, int> Inputs { get; set; } = [];
        public Dictionary<string, int> Outputs { get; set; } = [];
        public string? RequiredStationMaterialId { get; set; }
        public string SkillId { get; set; } = string.Empty;
        public int SkillGain { get; set; }

        public RecipeDef ToRuntime() => new(Id, DisplayName, Inputs, Outputs, RequiredStationMaterialId, SkillId, SkillGain);
    }

    private sealed class EnemyData
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Family { get; set; } = string.Empty;
        public int Danger { get; set; }
        public int Health { get; set; }
        public int Damage { get; set; }
        public Dictionary<string, int> Loot { get; set; } = [];
        public int SkillGain { get; set; }

        public EnemyDef ToRuntime() => new(Id, DisplayName, Family, Danger, Health, Damage, Loot, SkillGain);
    }

    private sealed class BossData
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int Danger { get; set; }
        public int Health { get; set; }
        public int Damage { get; set; }
        public Dictionary<string, int> Rewards { get; set; } = [];
        public Dictionary<string, int> BonusXpBySkill { get; set; } = [];
        public string BonusPerkId { get; set; } = string.Empty;

        public BossDef ToRuntime() => new(Id, DisplayName, Danger, Health, Damage, Rewards, BonusXpBySkill, BonusPerkId);
    }
}
