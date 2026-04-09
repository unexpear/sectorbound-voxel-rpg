namespace Sectorbound.Engine;

public sealed partial class GameSession
{
    private SectorRuntime LoadOrGenerateSector(int sectorIndex)
    {
        return _savedSectors.TryGetValue(sectorIndex, out var savedSector)
            ? RehydrateSector(savedSector)
            : GenerateSector(sectorIndex);
    }

    private SectorRuntime GenerateSector(int sectorIndex)
    {
        var sectorSeed = Hash(WorldSeed, sectorIndex, 7_921);
        var preferredOrder = new[] { "wilds-home", "ember-forge", "marrow-depths" };
        var archetypes = preferredOrder.All(_content.PlanetArchetypes.ContainsKey)
            ? preferredOrder.Select(_content.GetPlanetArchetype).ToArray()
            : _content.PlanetArchetypes.Values.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase).ToArray();
        var startIndex = sectorIndex == 0
            ? 0
            : PositiveMod((int)(sectorSeed % archetypes.Length), archetypes.Length);

        var planets = new List<PlanetRuntime>();
        for (var planetIndex = 0; planetIndex < 3; planetIndex++)
        {
            var archetype = archetypes[(startIndex + planetIndex) % archetypes.Length];
            var biome = _content.GetBiome(archetype.BiomeId);
            var planetSeed = Hash(sectorSeed, planetIndex, 97);
            planets.Add(
                new PlanetRuntime(
                    planetIndex,
                    archetype.Id,
                    GeneratePlanetName(archetype.DisplayName, planetSeed),
                    planetSeed,
                    biome,
                    biome.EnemyIds,
                    biome.StructureGenerators));
        }

        return new SectorRuntime(
            sectorIndex,
            sectorSeed,
            planets,
            new TransitionState(TransitionPhase.Idle, sectorIndex, sectorIndex, "Sector stable.", DateTimeOffset.UtcNow),
            streamingBudget: 24,
            sectorBossId: archetypes[startIndex].BossId);
    }

    private SectorRuntime RehydrateSector(SaveSectorData savedSector)
    {
        var sector = GenerateSector(savedSector.SectorIndex);
        foreach (var savedPlanet in savedSector.Planets)
        {
            var runtimePlanet = sector.LoadedPlanets[savedPlanet.PlanetIndex];
            foreach (var savedChunk in savedPlanet.Chunks)
            {
                var chunk = new ChunkData(
                    new ChunkCoordinate(savedChunk.Coordinate.X, savedChunk.Coordinate.Y, savedChunk.Coordinate.Z))
                {
                    IsPersisted = savedChunk.IsPersisted,
                    IsDirty = false
                };

                foreach (var delta in savedChunk.DeformationDeltas)
                {
                    var local = new Int3(delta.X, delta.Y, delta.Z);
                    chunk.DeformationDeltas[local] = delta.MaterialId;
                    chunk.VoxelMaterials[local] = delta.MaterialId;
                }

                foreach (var block in savedChunk.PlacedBlocks)
                {
                    var local = new Int3(block.X, block.Y, block.Z);
                    chunk.PlacedBlocks[local] = block.MaterialId;
                    chunk.VoxelMaterials[local] = block.MaterialId;
                }

                runtimePlanet.ChunkRegistry[chunk.Coordinate] = chunk;
            }
        }

        sector.TransitionState = new TransitionState(
            TransitionPhase.Idle,
            savedSector.SectorIndex,
            savedSector.SectorIndex,
            "Sector restored from save.",
            DateTimeOffset.UtcNow);

        return sector;
    }

    private bool ResolveCombatSuccess(int danger, CombatApproach approach, bool isBoss)
    {
        var combat = GetSkillProgress("combat");
        var arcana = GetSkillProgress("arcana");
        var mining = GetSkillProgress("mining");

        var power = 10
            + (combat.Level * 4)
            + (Player.PerkUnlocks.Contains("void-strider") ? 2 : 0)
            + (isBoss ? mining.Level : 0);

        if (approach == CombatApproach.Sigil)
        {
            power += arcana.Level * 3;
        }

        if (approach == CombatApproach.Bow)
        {
            power += 2;
        }

        var target = 8 + (danger * 2) + (isBoss ? 4 : 0);
        return power >= target;
    }

    private void AwardSkillExperience(string skillId, int amount)
    {
        var progress = GetSkillProgress(skillId);
        progress.Experience += amount;
        progress.LifetimeExperience += amount;

        while (progress.Experience >= ExperienceToNextLevel(progress.Level))
        {
            progress.Experience -= ExperienceToNextLevel(progress.Level);
            progress.Level += 1;
            UnlockSkillPerks(skillId, progress.Level);
        }
    }

    private void UnlockSkillPerks(string skillId, int newLevel)
    {
        foreach (var perk in _content.Perks.Values.Where(x =>
                     string.Equals(x.UnlockSkillId, skillId, StringComparison.OrdinalIgnoreCase)
                     && x.RequiredLevel <= newLevel))
        {
            Player.PerkUnlocks.Add(perk.Id);
        }
    }

    private static int ExperienceToNextLevel(int currentLevel) => 80 + ((currentLevel - 1) * 20);

    private void AddInventory(string itemId, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        Player.Inventory[itemId] = GetInventoryCount(itemId) + amount;
    }

    private bool RemoveInventory(string itemId, int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (!Player.Inventory.TryGetValue(itemId, out var currentCount) || currentCount < amount)
        {
            return false;
        }

        var remaining = currentCount - amount;
        if (remaining == 0)
        {
            Player.Inventory.Remove(itemId);
        }
        else
        {
            Player.Inventory[itemId] = remaining;
        }

        return true;
    }

    private ChunkData GetOrCreateChunk(PlanetRuntime planet, Int3 worldPosition)
    {
        var coordinate = ToChunkCoordinate(worldPosition);
        if (!planet.ChunkRegistry.TryGetValue(coordinate, out var chunk))
        {
            chunk = new ChunkData(coordinate);
            planet.ChunkRegistry[coordinate] = chunk;
        }

        return chunk;
    }

    private static ChunkCoordinate ToChunkCoordinate(Int3 worldPosition) =>
        new(FloorDiv(worldPosition.X, ChunkSize), FloorDiv(worldPosition.Y, ChunkSize), FloorDiv(worldPosition.Z, ChunkSize));

    private static Int3 ToLocal(Int3 worldPosition) =>
        new(PositiveMod(worldPosition.X, ChunkSize), PositiveMod(worldPosition.Y, ChunkSize), PositiveMod(worldPosition.Z, ChunkSize));

    private string ResolveMaterial(PlanetRuntime planet, Int3 worldPosition)
    {
        if (worldPosition.Y < WorldFloor || worldPosition.Y > WorldCeiling)
        {
            return AirMaterialId;
        }

        var chunkCoordinate = ToChunkCoordinate(worldPosition);
        if (planet.ChunkRegistry.TryGetValue(chunkCoordinate, out var chunk))
        {
            var local = ToLocal(worldPosition);
            if (chunk.PlacedBlocks.TryGetValue(local, out var placedBlock))
            {
                return placedBlock;
            }

            if (chunk.DeformationDeltas.TryGetValue(local, out var delta))
            {
                return delta;
            }
        }

        return ResolveGeneratedMaterial(planet, worldPosition);
    }

    private string ResolveGeneratedMaterial(PlanetRuntime planet, Int3 worldPosition)
    {
        var surfaceHeight = CalculateSurfaceHeight(planet, worldPosition.X, worldPosition.Z);
        if (worldPosition.Y > surfaceHeight)
        {
            return AirMaterialId;
        }

        var biome = planet.BiomeRules;
        var caveNoise = SampleUnit(planet.PlanetSeed, worldPosition.X, worldPosition.Y, worldPosition.Z, 17);
        if (worldPosition.Y < surfaceHeight - 2 && worldPosition.Y > WorldFloor + 2 && caveNoise > 1.0 - biome.CaveDensity)
        {
            return AirMaterialId;
        }

        if (worldPosition.Y == surfaceHeight)
        {
            return biome.SurfaceMaterialId;
        }

        if (worldPosition.Y >= surfaceHeight - 2)
        {
            return biome.SubsurfaceMaterialId;
        }

        return biome.CoreMaterialId;
    }

    private int CalculateSurfaceHeight(PlanetRuntime planet, int x, int z)
    {
        var broadNoise = SampleUnit(planet.PlanetSeed, x / 3, z / 3, 29);
        var detailNoise = SampleUnit(planet.PlanetSeed, x, z, 43);
        var signedValue = ((broadNoise - 0.5) * 10.0) + ((detailNoise - 0.5) * 4.0);
        var rawHeight = 18 + planet.BiomeRules.HeightBias + (int)Math.Round(signedValue);
        return Math.Clamp(rawHeight, WorldFloor + 4, WorldCeiling - 8);
    }

    private static string GeneratePlanetName(string baseName, long seed)
    {
        var prefixes = new[] { "Astra", "Cinder", "Hollow", "Lumen", "Vesper", "Thorn", "Gale", "Echo" };
        var suffixes = new[] { "reach", "fall", "spire", "rift", "mere", "vault", "scar", "hollow" };
        var prefix = prefixes[PositiveMod((int)(seed % prefixes.Length), prefixes.Length)];
        var suffix = suffixes[PositiveMod((int)((seed / 7) % suffixes.Length), suffixes.Length)];
        return $"{prefix} {baseName} {suffix}";
    }

    private static long Hash(long seed, params int[] values)
    {
        unchecked
        {
            var hash = seed;
            foreach (var value in values)
            {
                hash ^= value + 0x9e3779b9 + (hash << 6) + (hash >> 2);
            }

            return Math.Abs(hash);
        }
    }

    private static double SampleUnit(long seed, params int[] values)
    {
        unchecked
        {
            ulong hash = (ulong)seed + 0x9e3779b97f4a7c15UL;
            foreach (var value in values)
            {
                hash ^= (uint)value + 0x9e3779b97f4a7c15UL + (hash << 6) + (hash >> 2);
            }

            return (hash & 0x00ff_ffffUL) / (double)0x00ff_ffffUL;
        }
    }

    private static int FloorDiv(int value, int divisor)
    {
        var quotient = value / divisor;
        var remainder = value % divisor;
        return remainder < 0 ? quotient - 1 : quotient;
    }

    private static int PositiveMod(int value, int modulo)
    {
        var result = value % modulo;
        return result < 0 ? result + modulo : result;
    }

    private static int CalculatePenaltyAmount(int value, double penaltyPercent)
    {
        if (value <= 0 || penaltyPercent <= 0)
        {
            return 0;
        }

        return Math.Clamp((int)Math.Ceiling(value * penaltyPercent), 0, value);
    }
}
