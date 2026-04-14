using System.Text;
using System.Text.Json;

namespace Sectorbound.Engine;

public sealed partial class GameSession
{
    private const string AirMaterialId = "air";
    private const string WaterMaterialId = "water";
    private const int GameplayVoxelsPerOriginalBlock = 9;
    private const int ChunkSpanInOriginalBlocks = 4;
    private const int ChunkSize = ChunkSpanInOriginalBlocks * GameplayVoxelsPerOriginalBlock;
    private const int WorldFloor = 0;
    private const int WorldHeightInOriginalBlocks = 48;
    private const int WorldCeiling = WorldHeightInOriginalBlocks * GameplayVoxelsPerOriginalBlock;

    private static readonly JsonSerializerOptions SaveJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly ContentDefs _content;
    private readonly string _saveRootPath;
    private readonly string _saveSlotId;
    private readonly Dictionary<int, SaveSectorData> _savedSectors;

    private GameSession(
        ContentDefs content,
        string saveRootPath,
        string saveSlotId,
        long worldSeed,
        PlayerProfile player,
        int currentSectorIndex,
        int currentPlanetIndex,
        Dictionary<int, SaveSectorData> savedSectors)
    {
        _content = content;
        _saveRootPath = saveRootPath;
        _saveSlotId = saveSlotId;
        _savedSectors = savedSectors;
        WorldSeed = worldSeed;
        Player = player;
        CurrentSectorIndex = currentSectorIndex;
        Sector = LoadOrGenerateSector(currentSectorIndex);
        CurrentPlanetIndex = Math.Clamp(currentPlanetIndex, 0, Sector.LoadedPlanets.Count - 1);
    }

    public long WorldSeed { get; }
    public PlayerProfile Player { get; }
    public SectorRuntime Sector { get; private set; }
    public int CurrentSectorIndex { get; private set; }
    public int CurrentPlanetIndex { get; private set; }
    public PlanetRuntime CurrentPlanet => Sector.LoadedPlanets[CurrentPlanetIndex];
    public DifficultyDef CurrentDifficulty => _content.GetDifficulty(Player.DifficultyPresetId);

    public static GameSession CreateNew(
        ContentDefs content,
        string saveRootPath,
        string saveSlotId,
        long worldSeed,
        string difficultyPresetId = "frontier")
    {
        Directory.CreateDirectory(saveRootPath);

        var player = CreateStartingProfile(content, difficultyPresetId);
        return new GameSession(
            content,
            saveRootPath,
            saveSlotId,
            worldSeed,
            player,
            currentSectorIndex: 0,
            currentPlanetIndex: 0,
            savedSectors: []);
    }

    public static GameSession Load(ContentDefs content, string saveRootPath, string saveSlotId)
    {
        var savePath = GetSavePath(saveRootPath, saveSlotId);
        if (!File.Exists(savePath))
        {
            throw new FileNotFoundException("The requested save slot does not exist.", savePath);
        }

        var save = JsonSerializer.Deserialize<SaveGameData>(File.ReadAllText(savePath), SaveJsonOptions)
            ?? throw new InvalidOperationException("The save file could not be deserialized.");

        var savedSectors = save.VoxelsPerOriginalBlock == GameplayVoxelsPerOriginalBlock
            && save.ChunkSizeInVoxels == ChunkSize
            ? save.Sectors.ToDictionary(x => x.SectorIndex)
            : [];

        return new GameSession(
            content,
            saveRootPath,
            saveSlotId,
            save.WorldSeed,
            RehydratePlayer(save.Player),
            save.CurrentSectorIndex,
            save.CurrentPlanetIndex,
            savedSectors);
    }

    public static string GetSavePath(string saveRootPath, string saveSlotId) => Path.Combine(saveRootPath, $"{saveSlotId}.json");

    public string DescribeStatus()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Sector {CurrentSectorIndex} | Seed {Sector.ActiveSectorSeed}");
        builder.AppendLine($"Planet {CurrentPlanetIndex}: {CurrentPlanet.DisplayName} ({CurrentPlanet.BiomeRules.DisplayName})");
        builder.AppendLine($"Boss: {_content.GetBoss(Sector.SectorBossId).DisplayName}");
        builder.AppendLine($"Difficulty: {CurrentDifficulty.DisplayName}");
        builder.AppendLine($"Workbench: {(CurrentPlanetHasPlacedBlock("workbench-block") ? "placed on this planet" : "not placed on this planet")}");
        builder.AppendLine($"Inventory: {FormatKeyValuePairs(Player.Inventory)}");
        builder.AppendLine($"Skills: {FormatSkills()}");
        builder.AppendLine($"Perks: {(Player.PerkUnlocks.Count == 0 ? "none" : string.Join(", ", Player.PerkUnlocks.OrderBy(x => x)))}");
        return builder.ToString().TrimEnd();
    }

    public string DescribeInventory() => FormatKeyValuePairs(Player.Inventory);

    public string DescribeSkills() => FormatSkills();

    private string FormatSkills() =>
        string.Join(
            ", ",
            Player.SkillLevels
                .OrderBy(x => x.Key)
                .Select(x => $"{x.Key} L{x.Value.Level} ({x.Value.Experience} xp)"));

    private static string FormatKeyValuePairs(IReadOnlyDictionary<string, int> values) =>
        values.Count == 0
            ? "empty"
            : string.Join(", ", values.OrderByDescending(x => x.Value).ThenBy(x => x.Key).Select(x => $"{x.Key} x{x.Value}"));
}
