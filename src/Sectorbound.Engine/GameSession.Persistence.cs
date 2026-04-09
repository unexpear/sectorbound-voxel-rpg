using System.Text.Json;

namespace Sectorbound.Engine;

public sealed partial class GameSession
{
    public void Save()
    {
        Directory.CreateDirectory(_saveRootPath);
        PersistCurrentSector();

        var save = new SaveGameData
        {
            WorldSeed = WorldSeed,
            CurrentSectorIndex = CurrentSectorIndex,
            CurrentPlanetIndex = CurrentPlanetIndex,
            Player = CapturePlayer(),
            Sectors = _savedSectors.Values.OrderBy(x => x.SectorIndex).ToList()
        };

        File.WriteAllText(
            GetSavePath(_saveRootPath, _saveSlotId),
            JsonSerializer.Serialize(save, SaveJsonOptions));
    }

    private static PlayerProfile CreateStartingProfile(ContentDefs content, string difficultyPresetId)
    {
        _ = content.GetDifficulty(difficultyPresetId);

        var player = new PlayerProfile
        {
            DifficultyPresetId = difficultyPresetId,
            EquippedLoadout = new EquippedLoadout()
        };

        foreach (var skillId in content.Skills.Keys)
        {
            player.SkillLevels[skillId] = new SkillProgress();
        }

        player.Inventory["timber"] = 12;
        player.Inventory["fiber"] = 6;
        player.Inventory["healing-salve"] = 2;
        return player;
    }

    private static PlayerProfile RehydratePlayer(SavePlayerData data)
    {
        var player = new PlayerProfile
        {
            DifficultyPresetId = data.DifficultyPresetId,
            EquippedLoadout = data.EquippedLoadout ?? new EquippedLoadout()
        };

        foreach (var item in data.Inventory)
        {
            player.Inventory[item.Key] = item.Value;
        }

        foreach (var skill in data.SkillLevels)
        {
            player.SkillLevels[skill.Key] = new SkillProgress
            {
                Level = skill.Value.Level,
                Experience = skill.Value.Experience,
                LifetimeExperience = skill.Value.LifetimeExperience
            };
        }

        foreach (var perk in data.PerkUnlocks)
        {
            player.PerkUnlocks.Add(perk);
        }

        foreach (var bossBonus in data.BossBonuses)
        {
            player.BossBonuses.Add(bossBonus);
        }

        return player;
    }

    private SavePlayerData CapturePlayer() => new()
    {
        DifficultyPresetId = Player.DifficultyPresetId,
        EquippedLoadout = Player.EquippedLoadout,
        Inventory = new Dictionary<string, int>(Player.Inventory, StringComparer.OrdinalIgnoreCase),
        SkillLevels = Player.SkillLevels.ToDictionary(
            x => x.Key,
            x => new SaveSkillProgressData
            {
                Level = x.Value.Level,
                Experience = x.Value.Experience,
                LifetimeExperience = x.Value.LifetimeExperience
            },
            StringComparer.OrdinalIgnoreCase),
        PerkUnlocks = Player.PerkUnlocks.OrderBy(x => x).ToList(),
        BossBonuses = Player.BossBonuses.OrderBy(x => x).ToList()
    };

    private void PersistCurrentSector()
    {
        _savedSectors[CurrentSectorIndex] = CaptureSector(Sector);
    }

    private SaveSectorData CaptureSector(SectorRuntime sector)
    {
        return new SaveSectorData
        {
            SectorIndex = sector.SectorIndex,
            SectorSeed = sector.ActiveSectorSeed,
            SectorBossId = sector.SectorBossId,
            Planets = sector.LoadedPlanets.Select(CapturePlanet).ToList()
        };
    }

    private static SavePlanetData CapturePlanet(PlanetRuntime planet)
    {
        return new SavePlanetData
        {
            PlanetIndex = planet.PlanetIndex,
            ArchetypeId = planet.ArchetypeId,
            PlanetSeed = planet.PlanetSeed,
            Chunks = planet.ChunkRegistry.Values
                .Where(chunk => chunk.IsDirty || chunk.IsPersisted)
                .Select(chunk => new SaveChunkData
                {
                    Coordinate = new SaveChunkCoordinate
                    {
                        X = chunk.Coordinate.X,
                        Y = chunk.Coordinate.Y,
                        Z = chunk.Coordinate.Z
                    },
                    DeformationDeltas = chunk.DeformationDeltas.Select(ToSaveBlockEdit).ToList(),
                    PlacedBlocks = chunk.PlacedBlocks.Select(ToSaveBlockEdit).ToList(),
                    IsPersisted = true
                })
                .ToList()
        };
    }

    private static SaveBlockEdit ToSaveBlockEdit(KeyValuePair<Int3, string> entry) =>
        new()
        {
            X = entry.Key.X,
            Y = entry.Key.Y,
            Z = entry.Key.Z,
            MaterialId = entry.Value
        };

    private sealed class SaveGameData
    {
        public long WorldSeed { get; set; }
        public int CurrentSectorIndex { get; set; }
        public int CurrentPlanetIndex { get; set; }
        public SavePlayerData Player { get; set; } = new();
        public List<SaveSectorData> Sectors { get; set; } = [];
    }

    private sealed class SavePlayerData
    {
        public string DifficultyPresetId { get; set; } = "frontier";
        public EquippedLoadout? EquippedLoadout { get; set; }
        public Dictionary<string, int> Inventory { get; set; } = [];
        public Dictionary<string, SaveSkillProgressData> SkillLevels { get; set; } = [];
        public List<string> PerkUnlocks { get; set; } = [];
        public List<string> BossBonuses { get; set; } = [];
    }

    private sealed class SaveSkillProgressData
    {
        public int Level { get; set; }
        public int Experience { get; set; }
        public int LifetimeExperience { get; set; }
    }

    private sealed class SaveSectorData
    {
        public int SectorIndex { get; set; }
        public long SectorSeed { get; set; }
        public string SectorBossId { get; set; } = string.Empty;
        public List<SavePlanetData> Planets { get; set; } = [];
    }

    private sealed class SavePlanetData
    {
        public int PlanetIndex { get; set; }
        public string ArchetypeId { get; set; } = string.Empty;
        public long PlanetSeed { get; set; }
        public List<SaveChunkData> Chunks { get; set; } = [];
    }

    private sealed class SaveChunkData
    {
        public SaveChunkCoordinate Coordinate { get; set; } = new();
        public List<SaveBlockEdit> DeformationDeltas { get; set; } = [];
        public List<SaveBlockEdit> PlacedBlocks { get; set; } = [];
        public bool IsPersisted { get; set; }
    }

    private sealed class SaveChunkCoordinate
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
    }

    private sealed class SaveBlockEdit
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public string MaterialId { get; set; } = string.Empty;
    }
}
