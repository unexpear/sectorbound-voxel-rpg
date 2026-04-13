namespace Sectorbound.Engine;

public sealed partial class GameSession
{
    private readonly record struct DrainageProfile(
        double ChannelStrength,
        double BasinStrength,
        double ChannelDepth,
        double SedimentDepth,
        double Moisture);

    private readonly record struct BiomeFieldSample(
        double Heat,
        double Moisture,
        double Relief);

    private readonly record struct ScenicPropTemplate(
        string AssetId,
        int OffsetXOriginalBlocks,
        int OffsetZOriginalBlocks,
        Int3 FacingDirection);

    private static readonly ScenicPropTemplate[] ReedClusterScenicProps =
    [
        new("nature-bamboo", 2, -1, new Int3(1, 0, 0)),
        new("nature-tree-stump-round", -2, 2, new Int3(0, 0, 1))
    ];

    private static readonly ScenicPropTemplate[] RootBulbScenicProps =
    [
        new("nature-bamboo", -3, 1, new Int3(0, 0, 1)),
        new("nature-tree-stump-round", 2, -2, new Int3(-1, 0, 0))
    ];

    private static readonly ScenicPropTemplate[] EmberVentScenicProps =
    [
        new("nature-rock-tall4", 2, 0, new Int3(0, 0, -1)),
        new("nature-rock-large4", -2, -2, new Int3(1, 0, 0))
    ];

    private static readonly ScenicPropTemplate[] SlagColumnScenicProps =
    [
        new("nature-rock-tall4", -2, 1, new Int3(-1, 0, 0)),
        new("nature-rock-large4", 2, -1, new Int3(0, 0, 1))
    ];

    private static readonly ScenicPropTemplate[] CrystalClusterScenicProps =
    [
        new("nature-stone-obelisk", 3, 0, new Int3(0, 0, -1)),
        new("nature-stone-column", -2, 2, new Int3(1, 0, 0))
    ];

    private static readonly ScenicPropTemplate[] ShardFanScenicProps =
    [
        new("nature-stone-obelisk", -3, 1, new Int3(-1, 0, 0)),
        new("nature-stone-column", 2, -2, new Int3(0, 0, 1))
    ];

    private static readonly ScenicPropTemplate[] RootArchScenicProps =
    [
        new("nature-bamboo", -8, 3, new Int3(1, 0, 0)),
        new("nature-bamboo", 8, 2, new Int3(-1, 0, 0)),
        new("nature-tree-stump-round", 7, -3, new Int3(0, 0, 1)),
        new("nature-tree-stump-round", -7, -4, new Int3(1, 0, 0))
    ];

    private static readonly ScenicPropTemplate[] RuinedBeaconScenicProps =
    [
        new("nature-tree-stump-round", -7, 1, new Int3(1, 0, 0)),
        new("nature-tree-stump-round", 1, 7, new Int3(0, 0, -1)),
        new("nature-bamboo", 6, 3, new Int3(-1, 0, 0)),
        new("nature-bamboo", 7, -3, new Int3(-1, 0, 0))
    ];

    private static readonly ScenicPropTemplate[] AshSpireScenicProps =
    [
        new("nature-rock-tall4", -8, 2, new Int3(1, 0, 0)),
        new("nature-rock-tall4", 8, 0, new Int3(-1, 0, 0)),
        new("nature-rock-large4", 7, -2, new Int3(0, 0, 1)),
        new("nature-rock-large4", -6, -4, new Int3(1, 0, 0))
    ];

    private static readonly ScenicPropTemplate[] BrokenFurnaceScenicProps =
    [
        new("nature-rock-large4", -7, -3, new Int3(1, 0, 0)),
        new("nature-rock-large4", 7, 3, new Int3(-1, 0, 0)),
        new("nature-rock-large4", 0, 10, new Int3(0, 0, -1)),
        new("nature-rock-large4", 10, 0, new Int3(-1, 0, 0)),
        new("nature-rock-large4", 0, -10, new Int3(0, 0, 1)),
        new("nature-rock-tall4", 6, 3, new Int3(-1, 0, 0)),
        new("nature-rock-tall4", -6, 2, new Int3(1, 0, 0)),
        new("nature-rock-tall4", -10, 0, new Int3(1, 0, 0)),
        new("nature-rock-tall4", 0, -11, new Int3(0, 0, 1))
    ];

    private static readonly ScenicPropTemplate[] CrystalFissureScenicProps =
    [
        new("nature-stone-obelisk", -8, 2, new Int3(1, 0, 0)),
        new("nature-stone-obelisk", 8, -2, new Int3(-1, 0, 0)),
        new("nature-stone-column", 7, -2, new Int3(0, 0, 1)),
        new("nature-stone-column", -7, 3, new Int3(1, 0, 0))
    ];

    private static readonly ScenicPropTemplate[] BuriedObeliskScenicProps =
    [
        new("nature-stone-column", -6, -3, new Int3(1, 0, 0)),
        new("nature-stone-column", 6, 3, new Int3(-1, 0, 0)),
        new("nature-stone-obelisk", 7, 2, new Int3(-1, 0, 0)),
        new("nature-stone-obelisk", -7, -2, new Int3(1, 0, 0))
    ];

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
            var secondaryBiome = !string.IsNullOrEmpty(archetype.SecondaryBiomeId)
                ? _content.GetBiome(archetype.SecondaryBiomeId)
                : biome;
            var landform = _content.GetLandform(archetype.LandformId);
            var secondaryLandform = !string.IsNullOrEmpty(archetype.SecondaryLandformId)
                ? _content.GetLandform(archetype.SecondaryLandformId)
                : landform;
            var planetSeed = Hash(sectorSeed, planetIndex, 97);
            var landmarks = CreateGuaranteedLandmarks(planetSeed, landform, secondaryLandform);
            planets.Add(
                new PlanetRuntime(
                    planetIndex,
                    archetype.Id,
                    GeneratePlanetName(archetype.DisplayName, planetSeed),
                    planetSeed,
                    biome,
                    secondaryBiome,
                    landform,
                    secondaryLandform,
                    biome.EnemyIds
                        .Concat(secondaryBiome.EnemyIds)
                        .Distinct(StringComparer.OrdinalIgnoreCase),
                    landmarks));
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
        var activeBiome = ResolveActiveBiome(planet, worldPosition.X, worldPosition.Z);
        var activeLandform = ResolveActiveLandform(planet, worldPosition.X, worldPosition.Z);

        var structureMaterial = ResolveStructureMaterial(planet, worldPosition, surfaceHeight);
        if (structureMaterial is not null)
            return structureMaterial;

        var scaledX = worldPosition.X / (double)GameplayVoxelsPerOriginalBlock;
        var scaledY = worldPosition.Y / (double)GameplayVoxelsPerOriginalBlock;
        var scaledZ = worldPosition.Z / (double)GameplayVoxelsPerOriginalBlock;
        var region = CalculateRegion(planet, worldPosition.X, worldPosition.Z);
        var drainage = CalculateDrainageProfile(planet, worldPosition.X, worldPosition.Z, activeLandform, region);
        var waterSurfaceHeight = CalculateWaterSurfaceHeight(
            planet,
            worldPosition.X,
            worldPosition.Z,
            surfaceHeight,
            activeLandform,
            region,
            drainage);

        if (worldPosition.Y > surfaceHeight)
        {
            var featureAboveSurface = ResolveBiomeFeatureMaterial(
                planet,
                worldPosition,
                activeBiome,
                surfaceHeight,
                waterSurfaceHeight);
            if (featureAboveSurface is not null)
            {
                return featureAboveSurface;
            }

            return waterSurfaceHeight > surfaceHeight && worldPosition.Y <= waterSurfaceHeight
                ? WaterMaterialId
                : AirMaterialId;
        }

        var biomeFeatureMaterial = ResolveBiomeFeatureMaterial(
            planet,
            worldPosition,
            activeBiome,
            surfaceHeight,
            waterSurfaceHeight);
        if (biomeFeatureMaterial is not null)
        {
            return biomeFeatureMaterial;
        }

        var caveNoise = SampleFractalNoise3D(
            planet.PlanetSeed,
            scaledX * activeLandform.CaveScale,
            scaledY * (activeLandform.CaveScale * 0.85),
            scaledZ * activeLandform.CaveScale,
            octaves: 3,
            lacunarity: 2.05,
            gain: 0.52,
            salt: 17);
        var caveBand = (SampleValueNoise2D(
                           planet.PlanetSeed,
                           scaledX * (activeLandform.LargeFeatureScale * 0.65),
                           scaledZ * (activeLandform.LargeFeatureScale * 0.65),
                           salt: 23) - 0.5) * 0.14;
        // Lowlands (region→0) are more cavernous; highlands (region→1) are denser rock.
        var caveDensityMod = Lerp(1.2, 0.8, region) * Lerp(0.82, 1.05, 1.0 - drainage.Moisture);
        if (worldPosition.Y < surfaceHeight - (6 * GameplayVoxelsPerOriginalBlock)
            && worldPosition.Y > WorldFloor + (3 * GameplayVoxelsPerOriginalBlock)
            && caveNoise > (1.0 - activeLandform.CaveDensity * caveDensityMod + caveBand))
        {
            return AirMaterialId;
        }

        if (worldPosition.Y == surfaceHeight)
        {
            return drainage.ChannelStrength > 0.62
                ? activeBiome.SubsurfaceMaterialId
                : activeBiome.SurfaceMaterialId;
        }

        // Lowlands accumulate more sediment; highlands are rocky with thin topsoil.
        var subsurfaceDepth = (int)Math.Round((Lerp(5.0, 2.0, region) + (drainage.SedimentDepth * 1.75)) * GameplayVoxelsPerOriginalBlock);
        if (worldPosition.Y >= surfaceHeight - subsurfaceDepth)
        {
            return activeBiome.SubsurfaceMaterialId;
        }

        var vein = ResolveVeinMaterial(planet, worldPosition, surfaceHeight, activeBiome);
        return vein ?? activeBiome.CoreMaterialId;
    }

    private int CalculateSurfaceHeight(PlanetRuntime planet, int x, int z)
    {
        var landform = ResolveActiveLandform(planet, x, z);
        var scaledX = x / (double)GameplayVoxelsPerOriginalBlock;
        var scaledZ = z / (double)GameplayVoxelsPerOriginalBlock;
        var continental = SampleFractalNoise2D(
            planet.PlanetSeed,
            scaledX * landform.LargeFeatureScale,
            scaledZ * landform.LargeFeatureScale,
            octaves: 4,
            lacunarity: 2.05,
            gain: 0.5,
            salt: 29);
        var ridges = SampleRidgedNoise2D(
            planet.PlanetSeed,
            scaledX * landform.RidgeScale,
            scaledZ * landform.RidgeScale,
            octaves: 3,
            lacunarity: 2.15,
            gain: 0.55,
            salt: 43);
        var detail = (SampleFractalNoise2D(
                          planet.PlanetSeed,
                          scaledX * landform.DetailScale,
                          scaledZ * landform.DetailScale,
                          octaves: 2,
                          lacunarity: 2.4,
                          gain: 0.45,
                          salt: 59) * 2.0) - 1.0;
        var microDetail = (SampleValueNoise2D(
                               planet.PlanetSeed,
                               scaledX * (landform.DetailScale * 4.5),
                               scaledZ * (landform.DetailScale * 4.5),
                               salt: 67) * 2.0) - 1.0;
        var terraceDetail = (SampleUnit(
                                 planet.PlanetSeed,
                                 FloorDiv(x, 2 * GameplayVoxelsPerOriginalBlock),
                                 FloorDiv(z, 2 * GameplayVoxelsPerOriginalBlock),
                                 79) * 2.0) - 1.0;
        var basin = (SampleValueNoise2D(
                         planet.PlanetSeed,
                         scaledX * (landform.LargeFeatureScale * 0.55),
                         scaledZ * (landform.LargeFeatureScale * 0.55),
                         salt: 71) * 2.0) - 1.0;

        // region: 0 = lowland (flat, sediment-rich), 1 = highland (tall, ridge-heavy).
        var region = CalculateRegion(planet, x, z);
        var drainage = CalculateDrainageProfile(planet, x, z, landform, region);
        var ridgeAmplitudeMod = Lerp(0.55, 1.40, region);
        var rawHeight =
            landform.BaseHeight
            + landform.HeightBias
            + ((region - 0.5) * landform.LargeFeatureAmplitude * 0.9)
            + ((continental - 0.5) * 2.0 * landform.LargeFeatureAmplitude * 1.15)
            + ((ridges - 0.30) * landform.RidgeAmplitude * ridgeAmplitudeMod)
            + (detail * landform.DetailAmplitude * 1.25)
            + (microDetail * 2.0)
            + (terraceDetail * 3.5)
            + (basin * 3.0)
            - drainage.ChannelDepth
            - (drainage.BasinStrength * 1.8);

        var craterDepression = CalculateCraterDepression(planet, x, z);
        var scaledHeight = (rawHeight - craterDepression) * GameplayVoxelsPerOriginalBlock;
        return Math.Clamp((int)Math.Round(scaledHeight), WorldFloor + (4 * GameplayVoxelsPerOriginalBlock), WorldCeiling - (6 * GameplayVoxelsPerOriginalBlock));
    }

    // Returns 0 (lowland) to 1 (highland) for a given world XZ position.
    // Samples at very low frequency so region blobs are hundreds of voxels wide.
    private double CalculateRegion(PlanetRuntime planet, int x, int z)
    {
        var scaledX = x / (double)GameplayVoxelsPerOriginalBlock;
        var scaledZ = z / (double)GameplayVoxelsPerOriginalBlock;
        return SmoothStep(SampleValueNoise2D(planet.PlanetSeed, scaledX * 0.012, scaledZ * 0.012, salt: 91));
    }

    private DrainageProfile CalculateDrainageProfile(PlanetRuntime planet, int x, int z, LandformDef landform, double region)
    {
        var scaledX = x / (double)GameplayVoxelsPerOriginalBlock;
        var scaledZ = z / (double)GameplayVoxelsPerOriginalBlock;
        var warpX = ((SampleValueNoise2D(planet.PlanetSeed, scaledX * 0.018, scaledZ * 0.018, salt: 181) * 2.0) - 1.0) * 5.5;
        var warpZ = ((SampleValueNoise2D(planet.PlanetSeed, scaledX * 0.018, scaledZ * 0.018, salt: 191) * 2.0) - 1.0) * 5.5;
        var warpedX = scaledX + warpX;
        var warpedZ = scaledZ + warpZ;

        var trunkSignal = 1.0 - Math.Abs((SampleValueNoise2D(planet.PlanetSeed, warpedX * 0.030, warpedZ * 0.030, salt: 193) * 2.0) - 1.0);
        var tributarySignal = 1.0 - Math.Abs((SampleValueNoise2D(
                                   planet.PlanetSeed,
                                   (warpedX + (warpZ * 0.35)) * 0.055,
                                   (warpedZ - (warpX * 0.35)) * 0.055,
                                   salt: 197) * 2.0) - 1.0);
        var drainageLine = Math.Pow(Math.Clamp((trunkSignal * 0.65) + (tributarySignal * 0.35), 0.0, 1.0), 4.5);

        var macroLocal = SampleMacroRelief(planet, scaledX, scaledZ, landform);
        var macroEast = SampleMacroRelief(planet, scaledX + 3.0, scaledZ, landform);
        var macroWest = SampleMacroRelief(planet, scaledX - 3.0, scaledZ, landform);
        var macroNorth = SampleMacroRelief(planet, scaledX, scaledZ - 3.0, landform);
        var macroSouth = SampleMacroRelief(planet, scaledX, scaledZ + 3.0, landform);
        var neighborMean = (macroEast + macroWest + macroNorth + macroSouth) * 0.25;
        var valleySignal = SmoothStep(Math.Clamp((neighborMean - macroLocal + 1.25) / 2.75, 0.0, 1.0));

        var lowlandBias = 1.0 - region;
        var basinMask = 1.0 - SmoothStep(SampleValueNoise2D(planet.PlanetSeed, warpedX * 0.012, warpedZ * 0.012, salt: 211));
        var basinStrength = Math.Clamp((basinMask * 0.70 + valleySignal * 0.50) * (0.35 + (lowlandBias * 0.95)), 0.0, 1.0);
        basinStrength *= basinStrength;

        var channelStrength = Math.Clamp((drainageLine * 0.80 + valleySignal * 0.55) * (0.30 + (lowlandBias * 1.05)), 0.0, 1.0);
        var channelDepth = channelStrength * Lerp(0.8, 3.8, lowlandBias) * (0.85 + (valleySignal * 0.45));
        var sedimentDepth = Math.Clamp((channelStrength * 0.65) + (basinStrength * 0.85), 0.0, 1.0);
        var moisture = Math.Clamp(0.18 + (lowlandBias * 0.35) + (channelStrength * 0.60) + (basinStrength * 0.50), 0.0, 1.0);
        return new DrainageProfile(channelStrength, basinStrength, channelDepth, sedimentDepth, moisture);
    }

    private int CalculateWaterSurfaceHeight(
        PlanetRuntime planet,
        int x,
        int z,
        int surfaceHeight,
        LandformDef landform,
        double region,
        DrainageProfile drainage)
    {
        var lowlandBias = 1.0 - region;
        var saturation =
            (lowlandBias * 0.42)
            + (drainage.Moisture * 0.58)
            + (drainage.BasinStrength * 0.34)
            + (drainage.ChannelStrength * 0.28);
        if (saturation < 0.74)
        {
            return int.MinValue;
        }

        var probeStep = 2 * GameplayVoxelsPerOriginalBlock;
        var eastHeight = CalculateSurfaceHeight(planet, x + probeStep, z);
        var westHeight = CalculateSurfaceHeight(planet, x - probeStep, z);
        var northHeight = CalculateSurfaceHeight(planet, x, z - probeStep);
        var southHeight = CalculateSurfaceHeight(planet, x, z + probeStep);
        var eastWestBank = Math.Min(eastHeight, westHeight);
        var northSouthBank = Math.Min(northHeight, southHeight);
        var bankHeight = Math.Max(eastWestBank, northSouthBank);
        var bankMargin = Math.Max(1, GameplayVoxelsPerOriginalBlock / 2);
        var localRimHeight = bankHeight - bankMargin;
        var desiredDepth =
            (0.55 + (lowlandBias * 0.65) + (drainage.BasinStrength * 2.10) + (drainage.ChannelStrength * 1.45))
            * GameplayVoxelsPerOriginalBlock;
        var maxDepth = Math.Max(GameplayVoxelsPerOriginalBlock, (int)Math.Round(desiredDepth));
        var macroWaterline = (int)Math.Round(
            (landform.BaseHeight
             + landform.HeightBias
             - 1.8
             + (lowlandBias * 2.1)
             + (drainage.Moisture * 1.1)
             + (drainage.BasinStrength * 1.35))
            * GameplayVoxelsPerOriginalBlock);
        var localWaterHeight = localRimHeight > surfaceHeight
            ? Math.Min(surfaceHeight + maxDepth, localRimHeight)
            : int.MinValue;
        var tableWaterHeight = surfaceHeight <= macroWaterline
            ? Math.Min(macroWaterline, surfaceHeight + maxDepth)
            : int.MinValue;
        var saturationWaterHeight = saturation >= 0.94
            ? surfaceHeight + Math.Max(1, (int)Math.Round((saturation - 0.90) * GameplayVoxelsPerOriginalBlock * 3.0))
            : int.MinValue;
        var waterHeight = Math.Max(Math.Max(localWaterHeight, tableWaterHeight), saturationWaterHeight);
        return waterHeight <= surfaceHeight
            ? int.MinValue
            : Math.Clamp(waterHeight, WorldFloor + (3 * GameplayVoxelsPerOriginalBlock), WorldCeiling - (4 * GameplayVoxelsPerOriginalBlock));
    }

    private double SampleMacroRelief(PlanetRuntime planet, double scaledX, double scaledZ, LandformDef landform)
    {
        var continental = SampleFractalNoise2D(
            planet.PlanetSeed,
            scaledX * landform.LargeFeatureScale,
            scaledZ * landform.LargeFeatureScale,
            octaves: 3,
            lacunarity: 2.0,
            gain: 0.52,
            salt: 223);
        var ridges = SampleRidgedNoise2D(
            planet.PlanetSeed,
            scaledX * landform.RidgeScale,
            scaledZ * landform.RidgeScale,
            octaves: 2,
            lacunarity: 2.1,
            gain: 0.55,
            salt: 227);
        var basin = (SampleValueNoise2D(
                         planet.PlanetSeed,
                         scaledX * (landform.LargeFeatureScale * 0.60),
                         scaledZ * (landform.LargeFeatureScale * 0.60),
                         salt: 229) * 2.0) - 1.0;
        var regionBias = (SampleValueNoise2D(planet.PlanetSeed, scaledX * 0.012, scaledZ * 0.012, salt: 233) - 0.5)
            * landform.LargeFeatureAmplitude
            * 0.75;
        return ((continental - 0.5) * 2.0 * landform.LargeFeatureAmplitude * 1.10)
            + ((ridges - 0.30) * landform.RidgeAmplitude * 0.65)
            + (basin * 2.5)
            + regionBias;
    }

    private IReadOnlyList<PlanetLandmark> CreateGuaranteedLandmarks(long planetSeed, LandformDef primaryLandform, LandformDef secondaryLandform)
    {
        var orderedGenerators = primaryLandform.StructureGenerators
            .Concat(secondaryLandform.StructureGenerators)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();
        if (orderedGenerators.Length == 0)
        {
            return [];
        }

        var ringDistances = new[] { 46.0, 74.0, 102.0 };
        var landmarks = new List<PlanetLandmark>(orderedGenerators.Length);
        for (var index = 0; index < orderedGenerators.Length; index++)
        {
            var structureId = orderedGenerators[index];
            var angle = SampleUnit(planetSeed, index, 239) * Math.PI * 2.0;
            var radiusBlocks = ringDistances[Math.Min(index, ringDistances.Length - 1)]
                + (SampleUnit(planetSeed, index, 241) * 10.0)
                - 5.0;
            var originX = (int)Math.Round(Math.Cos(angle) * radiusBlocks * GameplayVoxelsPerOriginalBlock);
            var originZ = (int)Math.Round(Math.Sin(angle) * radiusBlocks * GameplayVoxelsPerOriginalBlock);
            landmarks.Add(new PlanetLandmark(structureId, originX, originZ, GetLandmarkRadius(structureId)));
        }

        return landmarks;
    }

    private static int GetLandmarkRadius(string structureId) =>
        structureId switch
        {
            "root-arch" => 8,
            "ruined-beacon" => 8,
            "ash-spire" => 9,
            "broken-furnace" => 8,
            "crystal-fissure" => 9,
            "buried-obelisk" => 8,
            _ => 6
        };

    // ~30 % of cells on this grid contain one structure; footprint max radius = 6 original blocks.
    private const int StructureCellSizeBlocks = 40;
    private const int BiomeFeatureCellSizeBlocks = 18;
    private const int SpawnSpacingOriginalBlocks = 12;
    private const int ScenicPropCellSearchMarginBlocks = 4;
    private const int ScenicPropLandmarkClearanceBlocks = 3;
    private const int ScenicPropOriginClearanceBlocks = 8;
    private const int ScenicPropMaxSlopeDeltaVoxels = 2 * GameplayVoxelsPerOriginalBlock;

    private static int GetBiomeFeatureRadius(string featureId) =>
        featureId switch
        {
            "reed-cluster" => 3,
            "root-bulb" => 4,
            "ember-vent" => 4,
            "slag-column" => 3,
            "crystal-cluster" => 4,
            "shard-fan" => 5,
            _ => 3
        };

    private string? ResolveStructureMaterial(PlanetRuntime planet, Int3 worldPosition, int columnSurfaceHeight)
    {
        var landmarkMaterial = ResolveLandmarkMaterial(planet, worldPosition);
        if (landmarkMaterial is not null)
        {
            return landmarkMaterial;
        }

        var cellVoxelSize = StructureCellSizeBlocks * GameplayVoxelsPerOriginalBlock;
        var cellX = FloorDiv(worldPosition.X, cellVoxelSize);
        var cellZ = FloorDiv(worldPosition.Z, cellVoxelSize);

        var cellCenterX = (cellX * cellVoxelSize) + (cellVoxelSize / 2);
        var cellCenterZ = (cellZ * cellVoxelSize) + (cellVoxelSize / 2);
        var activeLandform = ResolveActiveLandform(planet, cellCenterX, cellCenterZ);
        if (activeLandform.StructureGenerators.Count == 0)
            return null;

        // ~70 % of cells are empty — bail before any expensive work.
        if (SampleUnit(planet.PlanetSeed, cellX, cellZ, 103) >= 0.30)
            return null;

        var generatorCount = activeLandform.StructureGenerators.Count;
        var typeIndex = PositiveMod(
            (int)(SampleUnit(planet.PlanetSeed, cellX, cellZ, 107) * generatorCount),
            generatorCount);
        var structureId = activeLandform.StructureGenerators[typeIndex];

        // Keep origin in the inner 70 % of the cell so structures don't straddle cell borders.
        var margin    = (int)(cellVoxelSize * 0.15);
        var innerSize = (int)(cellVoxelSize * 0.70);
        var originX = cellX * cellVoxelSize + margin + (int)(SampleUnit(planet.PlanetSeed, cellX, cellZ, 109) * innerSize);
        var originZ = cellZ * cellVoxelSize + margin + (int)(SampleUnit(planet.PlanetSeed, cellX, cellZ, 113) * innerSize);

        // Horizontal relative position in original-block units (no surface lookup yet).
        var relX = FloorDiv(worldPosition.X - originX, GameplayVoxelsPerOriginalBlock);
        var relZ = FloorDiv(worldPosition.Z - originZ, GameplayVoxelsPerOriginalBlock);

        // Cull here to avoid calling CalculateSurfaceHeight for distant voxels.
        if (Math.Abs(relX) > 6 || Math.Abs(relZ) > 6)
            return null;

        var originSurface = CalculateSurfaceHeight(planet, originX, originZ);
        var relY = FloorDiv(worldPosition.Y - originSurface, GameplayVoxelsPerOriginalBlock);
        var activeBiome = ResolveActiveBiome(planet, originX, originZ);

        return structureId switch
        {
            "root-arch"       => SampleRootArch(activeBiome, relX, relY, relZ),
            "ruined-beacon"   => SampleRuinedBeacon(planet, activeBiome, relX, relY, relZ, cellX, cellZ),
            "ash-spire"       => SampleAshSpire(planet, activeBiome, relX, relY, relZ, cellX, cellZ),
            "broken-furnace"  => SampleBrokenFurnace(planet, activeBiome, relX, relY, relZ, cellX, cellZ),
            "crystal-fissure" => SampleCrystalFissure(activeBiome, relX, relY, relZ),
            "buried-obelisk"  => SampleBuriedObelisk(activeBiome, relX, relY, relZ),
            _                 => null
        };
    }

    private string? ResolveLandmarkMaterial(PlanetRuntime planet, Int3 worldPosition)
    {
        foreach (var landmark in planet.Landmarks)
        {
            var radiusVoxels = landmark.RadiusInOriginalBlocks * GameplayVoxelsPerOriginalBlock;
            if (Math.Abs(worldPosition.X - landmark.OriginX) > radiusVoxels
                || Math.Abs(worldPosition.Z - landmark.OriginZ) > radiusVoxels)
            {
                continue;
            }

            var originSurface = CalculateSurfaceHeight(planet, landmark.OriginX, landmark.OriginZ);
            var relX = FloorDiv(worldPosition.X - landmark.OriginX, GameplayVoxelsPerOriginalBlock);
            var relY = FloorDiv(worldPosition.Y - originSurface, GameplayVoxelsPerOriginalBlock);
            var relZ = FloorDiv(worldPosition.Z - landmark.OriginZ, GameplayVoxelsPerOriginalBlock);
            var biome = ResolveActiveBiome(planet, landmark.OriginX, landmark.OriginZ);
            var material = SampleLandmarkStructure(planet, biome, landmark, relX, relY, relZ);
            if (material is not null)
            {
                return material;
            }
        }

        return null;
    }

    private BiomeFeatureSite? ResolveBiomeFeaturePlacement(PlanetRuntime planet, int x, int z)
    {
        var cellVoxelSize = BiomeFeatureCellSizeBlocks * GameplayVoxelsPerOriginalBlock;
        var cellX = FloorDiv(x, cellVoxelSize);
        var cellZ = FloorDiv(z, cellVoxelSize);
        var placement = ResolveBiomeFeaturePlacementForCell(planet, cellX, cellZ);
        if (placement is null)
        {
            return null;
        }

        var radiusInVoxels = placement.Value.RadiusInOriginalBlocks * GameplayVoxelsPerOriginalBlock;
        if (Math.Abs(x - placement.Value.OriginX) > radiusInVoxels || Math.Abs(z - placement.Value.OriginZ) > radiusInVoxels)
        {
            return null;
        }

        return placement;
    }

    private BiomeFeatureSite? ResolveBiomeFeaturePlacementForCell(PlanetRuntime planet, int cellX, int cellZ)
    {
        var cellVoxelSize = BiomeFeatureCellSizeBlocks * GameplayVoxelsPerOriginalBlock;
        var cellCenterX = (cellX * cellVoxelSize) + (cellVoxelSize / 2);
        var cellCenterZ = (cellZ * cellVoxelSize) + (cellVoxelSize / 2);
        var biome = ResolveActiveBiome(planet, cellCenterX, cellCenterZ);
        var featureGenerators = biome.FeatureGenerators ?? [];
        if (featureGenerators.Count == 0)
        {
            return null;
        }

        if (PositiveMod((cellX * 7) + (cellZ * 11), 4) == 0)
        {
            return null;
        }

        var typeIndex = PositiveMod((cellX * 3) + (cellZ * 5), featureGenerators.Count);
        var featureId = featureGenerators[typeIndex];
        var margin = (int)(cellVoxelSize * 0.18);
        var innerSize = (int)(cellVoxelSize * 0.64);
        var originX = (cellX * cellVoxelSize) + margin + (int)(SampleUnit(planet.PlanetSeed, cellX, cellZ, 359) * innerSize);
        var originZ = (cellZ * cellVoxelSize) + margin + (int)(SampleUnit(planet.PlanetSeed, cellX, cellZ, 367) * innerSize);
        return new BiomeFeatureSite(featureId, originX, originZ, GetBiomeFeatureRadius(featureId));
    }

    private IReadOnlyList<ScenicPropInstance> ResolveScenicPropsForChunk(PlanetRuntime planet, int chunkX, int chunkZ)
    {
        var minX = chunkX * ChunkSize;
        var minZ = chunkZ * ChunkSize;
        var maxX = minX + ChunkSize - 1;
        var maxZ = minZ + ChunkSize - 1;
        var cellVoxelSize = BiomeFeatureCellSizeBlocks * GameplayVoxelsPerOriginalBlock;
        var searchMarginVoxels = ScenicPropCellSearchMarginBlocks * GameplayVoxelsPerOriginalBlock;
        var minCellX = FloorDiv(minX - searchMarginVoxels, cellVoxelSize);
        var maxCellX = FloorDiv(maxX + searchMarginVoxels, cellVoxelSize);
        var minCellZ = FloorDiv(minZ - searchMarginVoxels, cellVoxelSize);
        var maxCellZ = FloorDiv(maxZ + searchMarginVoxels, cellVoxelSize);
        var props = new List<ScenicPropInstance>();
        var seen = new HashSet<(string AssetId, Int3 AnchorVoxel)>();

        for (var cellX = minCellX; cellX <= maxCellX; cellX++)
        {
            for (var cellZ = minCellZ; cellZ <= maxCellZ; cellZ++)
            {
                var feature = ResolveBiomeFeaturePlacementForCell(planet, cellX, cellZ);
                if (feature is null)
                {
                    continue;
                }

                AppendScenicPropsForFeature(planet, feature.Value, minX, maxX, minZ, maxZ, props, seen);
            }
        }

        foreach (var landmark in planet.Landmarks)
        {
            AppendScenicPropsForLandmark(planet, landmark, minX, maxX, minZ, maxZ, props, seen);
        }

        return props
            .OrderBy(x => x.AnchorVoxel.X)
            .ThenBy(x => x.AnchorVoxel.Z)
            .ThenBy(x => x.AssetId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<WorldGenSlice> GenerateSlicesAlongX(
        PlanetRuntime planet,
        Int3 origin,
        int sliceCount,
        int sliceHeightVoxels,
        int sliceDepthVoxels)
    {
        if (sliceCount < 1 || sliceCount > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(sliceCount), "Slice generation currently supports 1 to 5 slices.");
        }

        if (sliceHeightVoxels < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sliceHeightVoxels), "Slice height must be at least one voxel.");
        }

        if (sliceDepthVoxels < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sliceDepthVoxels), "Slice depth must be at least one voxel.");
        }

        var slices = new List<WorldGenSlice>(sliceCount);
        Dictionary<(int Y, int Z), WorldGenSliceBlock>? previousSliceLookup = null;

        for (var sliceIndex = 0; sliceIndex < sliceCount; sliceIndex++)
        {
            var worldX = origin.X + sliceIndex;
            var blocks = new List<WorldGenSliceBlock>(sliceHeightVoxels * sliceDepthVoxels);
            var currentSliceLookup = new Dictionary<(int Y, int Z), WorldGenSliceBlock>(sliceHeightVoxels * sliceDepthVoxels);

            for (var yOffset = 0; yOffset < sliceHeightVoxels; yOffset++)
            {
                var worldY = origin.Y + yOffset;
                for (var zOffset = 0; zOffset < sliceDepthVoxels; zOffset++)
                {
                    var worldZ = origin.Z + zOffset;
                    var position = new Int3(worldX, worldY, worldZ);
                    var materialId = ResolveMaterial(planet, position);
                    var material = _content.GetMaterial(materialId);
                    SliceTouchLink? previousTouch = null;

                    if (previousSliceLookup is not null
                        && previousSliceLookup.TryGetValue((worldY, worldZ), out var previousBlock))
                    {
                        previousTouch = new SliceTouchLink(
                            previousBlock.Position,
                            previousBlock.MaterialId,
                            previousBlock.IsSolid);
                    }

                    var block = new WorldGenSliceBlock(position, materialId, material.IsSolid, previousTouch);
                    blocks.Add(block);
                    currentSliceLookup[(worldY, worldZ)] = block;
                }
            }

            slices.Add(new WorldGenSlice(sliceIndex, worldX, sliceHeightVoxels, sliceDepthVoxels, blocks.ToArray()));
            previousSliceLookup = currentSliceLookup;
        }

        return slices.ToArray();
    }

    private void AppendScenicPropsForFeature(
        PlanetRuntime planet,
        BiomeFeatureSite feature,
        int minX,
        int maxX,
        int minZ,
        int maxZ,
        List<ScenicPropInstance> props,
        HashSet<(string AssetId, Int3 AnchorVoxel)> seen)
    {
        var templates = GetScenicPropTemplates(feature.FeatureId);
        if (templates.Length == 0)
        {
            return;
        }

        var featureBiome = ResolveActiveBiome(planet, feature.OriginX, feature.OriginZ);
        var featureSurface = CalculateSurfaceHeight(planet, feature.OriginX, feature.OriginZ);

        foreach (var template in templates)
        {
            var worldX = feature.OriginX + (template.OffsetXOriginalBlocks * GameplayVoxelsPerOriginalBlock);
            var worldZ = feature.OriginZ + (template.OffsetZOriginalBlocks * GameplayVoxelsPerOriginalBlock);
            if (worldX < minX || worldX > maxX || worldZ < minZ || worldZ > maxZ)
            {
                continue;
            }

            if ((worldX * worldX) + (worldZ * worldZ)
                <= (ScenicPropOriginClearanceBlocks * GameplayVoxelsPerOriginalBlock)
                * (ScenicPropOriginClearanceBlocks * GameplayVoxelsPerOriginalBlock))
            {
                continue;
            }

            if (IsNearLandmark(planet, worldX, worldZ))
            {
                continue;
            }

            var propBiome = ResolveActiveBiome(planet, worldX, worldZ);
            if (!string.Equals(propBiome.Id, featureBiome.Id, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var surfaceHeight = CalculateSurfaceHeight(planet, worldX, worldZ);
            if (Math.Abs(surfaceHeight - featureSurface) > ScenicPropMaxSlopeDeltaVoxels)
            {
                continue;
            }

            var anchorVoxel = new Int3(worldX, surfaceHeight + 1, worldZ);
            if (!string.Equals(ResolveMaterial(planet, anchorVoxel), AirMaterialId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (seen.Add((template.AssetId, anchorVoxel)))
            {
                props.Add(new ScenicPropInstance(template.AssetId, anchorVoxel, template.FacingDirection));
            }
        }
    }

    private void AppendScenicPropsForLandmark(
        PlanetRuntime planet,
        PlanetLandmark landmark,
        int minX,
        int maxX,
        int minZ,
        int maxZ,
        List<ScenicPropInstance> props,
        HashSet<(string AssetId, Int3 AnchorVoxel)> seen)
    {
        var templates = GetLandmarkScenicPropTemplates(landmark.StructureId);
        if (templates.Length == 0)
        {
            return;
        }

        var placedAssetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var template in templates)
        {
            if (placedAssetIds.Contains(template.AssetId))
            {
                continue;
            }

            var worldX = landmark.OriginX + (template.OffsetXOriginalBlocks * GameplayVoxelsPerOriginalBlock);
            var worldZ = landmark.OriginZ + (template.OffsetZOriginalBlocks * GameplayVoxelsPerOriginalBlock);
            if (worldX < minX || worldX > maxX || worldZ < minZ || worldZ > maxZ)
            {
                continue;
            }

            if ((worldX * worldX) + (worldZ * worldZ)
                <= (ScenicPropOriginClearanceBlocks * GameplayVoxelsPerOriginalBlock)
                * (ScenicPropOriginClearanceBlocks * GameplayVoxelsPerOriginalBlock))
            {
                continue;
            }

            var surfaceHeight = CalculateSurfaceHeight(planet, worldX, worldZ);
            var anchorVoxel = new Int3(worldX, surfaceHeight + 1, worldZ);
            if (string.Equals(ResolveMaterial(planet, anchorVoxel), WaterMaterialId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (seen.Add((template.AssetId, anchorVoxel)))
            {
                placedAssetIds.Add(template.AssetId);
                props.Add(new ScenicPropInstance(template.AssetId, anchorVoxel, template.FacingDirection));
            }
        }
    }

    private static ScenicPropTemplate[] GetScenicPropTemplates(string featureId) =>
        featureId switch
        {
            "reed-cluster" => ReedClusterScenicProps,
            "root-bulb" => RootBulbScenicProps,
            "ember-vent" => EmberVentScenicProps,
            "slag-column" => SlagColumnScenicProps,
            "crystal-cluster" => CrystalClusterScenicProps,
            "shard-fan" => ShardFanScenicProps,
            _ => []
        };

    private static ScenicPropTemplate[] GetLandmarkScenicPropTemplates(string structureId) =>
        structureId switch
        {
            "root-arch" => RootArchScenicProps,
            "ruined-beacon" => RuinedBeaconScenicProps,
            "ash-spire" => AshSpireScenicProps,
            "broken-furnace" => BrokenFurnaceScenicProps,
            "crystal-fissure" => CrystalFissureScenicProps,
            "buried-obelisk" => BuriedObeliskScenicProps,
            _ => []
        };

    private static bool IsNearLandmark(PlanetRuntime planet, int worldX, int worldZ)
    {
        var clearance = ScenicPropLandmarkClearanceBlocks * GameplayVoxelsPerOriginalBlock;
        foreach (var landmark in planet.Landmarks)
        {
            var radius = (landmark.RadiusInOriginalBlocks * GameplayVoxelsPerOriginalBlock) + clearance;
            if (Math.Abs(worldX - landmark.OriginX) <= radius
                && Math.Abs(worldZ - landmark.OriginZ) <= radius)
            {
                return true;
            }
        }

        return false;
    }

    private string? ResolveBiomeFeatureMaterial(
        PlanetRuntime planet,
        Int3 worldPosition,
        BiomeDef activeBiome,
        int surfaceHeight,
        int waterSurfaceHeight)
    {
        var placement = ResolveBiomeFeaturePlacement(planet, worldPosition.X, worldPosition.Z);
        if (placement is null)
        {
            return null;
        }

        if (worldPosition.Y > surfaceHeight && waterSurfaceHeight > surfaceHeight && worldPosition.Y <= waterSurfaceHeight)
        {
            return null;
        }

        var feature = placement.Value;
        var featureBiome = ResolveActiveBiome(planet, feature.OriginX, feature.OriginZ);
        if (!string.Equals(featureBiome.Id, activeBiome.Id, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var originSurface = CalculateSurfaceHeight(planet, feature.OriginX, feature.OriginZ);
        var relX = FloorDiv(worldPosition.X - feature.OriginX, GameplayVoxelsPerOriginalBlock);
        var relY = FloorDiv(worldPosition.Y - originSurface, GameplayVoxelsPerOriginalBlock);
        var relZ = FloorDiv(worldPosition.Z - feature.OriginZ, GameplayVoxelsPerOriginalBlock);

        return feature.FeatureId switch
        {
            "reed-cluster" => SampleReedCluster(planet, relX, relY, relZ, feature.OriginX, feature.OriginZ),
            "root-bulb" => SampleRootBulb(activeBiome, relX, relY, relZ),
            "ember-vent" => SampleEmberVent(activeBiome, relX, relY, relZ),
            "slag-column" => SampleSlagColumn(planet, activeBiome, relX, relY, relZ, feature.OriginX, feature.OriginZ),
            "crystal-cluster" => SampleCrystalCluster(planet, relX, relY, relZ, feature.OriginX, feature.OriginZ),
            "shard-fan" => SampleShardFan(activeBiome, relX, relY, relZ),
            _ => null
        };
    }

    private static string? SampleLandmarkStructure(
        PlanetRuntime planet,
        BiomeDef biome,
        PlanetLandmark landmark,
        int relX,
        int relY,
        int relZ)
    {
        return landmark.StructureId switch
        {
            "root-arch" => SampleRootArchLandmark(biome, relX, relY, relZ),
            "ruined-beacon" => SampleRuinedBeaconLandmark(biome, relX, relY, relZ),
            "ash-spire" => SampleAshSpireLandmark(planet, biome, relX, relY, relZ, landmark.OriginX, landmark.OriginZ),
            "broken-furnace" => SampleBrokenFurnaceLandmark(planet, biome, relX, relY, relZ, landmark.OriginX, landmark.OriginZ),
            "crystal-fissure" => SampleCrystalFissureLandmark(biome, relX, relY, relZ),
            "buried-obelisk" => SampleBuriedObeliskLandmark(biome, relX, relY, relZ),
            _ => null
        };
    }

    // Two pillars (relX = ±2) joined by a lintel at the top — all at relZ = 0.
    private static string? SampleRootArchLandmark(BiomeDef biome, int relX, int relY, int relZ)
    {
        if (Math.Abs(relZ) > 1 || relY < 0 || relY > 8) return null;
        var absX = Math.Abs(relX);
        var pillar = ((absX == 5 || absX == 4) && relY <= 6) || (absX == 3 && relY <= 2);
        var lintel = relY >= 6 && relY <= 7 && relX >= -5 && relX <= 5;
        var rootBand = relY == 0 && absX <= 6 && Math.Abs(relZ) == 1;
        return pillar || lintel
            ? biome.CoreMaterialId
            : rootBand
                ? biome.SubsurfaceMaterialId
                : null;
    }

    private static string? SampleRuinedBeaconLandmark(BiomeDef biome, int relX, int relY, int relZ)
    {
        if (relY < 0 || relY > 9) return null;
        var distSq = relX * relX + relZ * relZ;
        if (relY == 0 && distSq <= 30)
        {
            return biome.CoreMaterialId;
        }

        var ring = distSq >= 9 && distSq <= 25;
        if (!ring)
        {
            return null;
        }

        var brokenCap = relY >= 7
            ? (relX == 0 || relZ == 0 || Math.Abs(relX) == Math.Abs(relZ))
            : true;
        return brokenCap ? biome.SubsurfaceMaterialId : null;
    }

    private static string? SampleAshSpireLandmark(PlanetRuntime planet, BiomeDef biome, int relX, int relY, int relZ, int originX, int originZ)
    {
        if (relY < 0 || relY > 15) return null;

        if (Math.Abs(relX) <= 1 && Math.Abs(relZ) <= 1 && relY <= 15)
        {
            return biome.CoreMaterialId;
        }

        var satelliteHeight = 6 + PositiveMod((int)(SampleUnit(planet.PlanetSeed, originX, originZ, 283) * 3), 3);
        var satellite = ((Math.Abs(relX) == 4 && relZ == 0) || (Math.Abs(relZ) == 4 && relX == 0)) && relY <= satelliteHeight;
        if (satellite)
        {
            return biome.SubsurfaceMaterialId;
        }

        var rubbleRing = relY == 0 && relX * relX + relZ * relZ <= 20;
        return rubbleRing ? biome.SubsurfaceMaterialId : null;
    }

    private static string? SampleBrokenFurnaceLandmark(PlanetRuntime planet, BiomeDef biome, int relX, int relY, int relZ, int originX, int originZ)
    {
        if (relX < -4 || relX > 4 || relZ < -4 || relZ > 4 || relY < 0 || relY > 5) return null;
        var distSq = relX * relX + relZ * relZ;
        if (relY == 0 && distSq <= 20)
        {
            return biome.SubsurfaceMaterialId;
        }

        if (Math.Abs(relX) <= 1 && Math.Abs(relZ) <= 1 && relY <= 5)
        {
            return biome.CoreMaterialId;
        }

        if (distSq >= 8 && distSq <= 18 && relY <= 3)
        {
            var filled = SampleUnit(planet.PlanetSeed, originX + relX, relY * 11, originZ + relZ, 293) >= 0.22;
            return filled ? biome.SubsurfaceMaterialId : null;
        }

        return null;
    }

    private static string? SampleCrystalFissureLandmark(BiomeDef biome, int relX, int relY, int relZ)
    {
        if (relY < -3 || relY > 7) return null;
        var mainRift = ((relX == 0 && Math.Abs(relZ) <= 5) || (relZ == 0 && Math.Abs(relX) <= 5)) && relY <= 2;
        if (mainRift)
        {
            return biome.SurfaceMaterialId;
        }

        var shard =
            ((Math.Abs(relX) == 3 && Math.Abs(relZ) <= 1) || (Math.Abs(relZ) == 3 && Math.Abs(relX) <= 1))
            && relY >= 0
            && relY <= 6;
        return shard ? biome.CoreMaterialId : null;
    }

    private static string? SampleBuriedObeliskLandmark(BiomeDef biome, int relX, int relY, int relZ)
    {
        if (relY < -2 || relY > 9) return null;
        if (relY <= 0 && Math.Abs(relX) <= 2 && Math.Abs(relZ) <= 2)
        {
            return biome.SubsurfaceMaterialId;
        }

        if (Math.Abs(relX) <= 1 && Math.Abs(relZ) <= 1 && relY <= 8)
        {
            return biome.CoreMaterialId;
        }

        var crown = relY >= 7 && relY <= 8 && Math.Abs(relX) + Math.Abs(relZ) <= 3;
        return crown ? biome.SurfaceMaterialId : null;
    }

    private static string? SampleReedCluster(PlanetRuntime planet, int relX, int relY, int relZ, int originX, int originZ)
    {
        if (relY < 0 || relY > 6)
        {
            return null;
        }

        if (MatchesStem(planet, relX, relY, relZ, originX, originZ, 0, 0, 401, 4, 3)
            || MatchesStem(planet, relX, relY, relZ, originX, originZ, 1, 0, 409, 3, 3)
            || MatchesStem(planet, relX, relY, relZ, originX, originZ, -1, 1, 419, 2, 3)
            || MatchesStem(planet, relX, relY, relZ, originX, originZ, 0, -1, 431, 2, 2))
        {
            return "rootwood";
        }

        return null;
    }

    private static string? SampleRootBulb(BiomeDef biome, int relX, int relY, int relZ)
    {
        if (relX < -3 || relX > 3 || relZ < -3 || relZ > 3 || relY < -1 || relY > 3)
        {
            return null;
        }

        var distSq = relX * relX + relZ * relZ;
        if (relY <= 0 && distSq <= 5)
        {
            return biome.SubsurfaceMaterialId;
        }

        var bulb = relY switch
        {
            1 => distSq <= 4,
            2 => distSq <= 2,
            3 => distSq == 0,
            _ => false
        };
        if (bulb)
        {
            return "rootwood";
        }

        var lateralRoots = relY == 1
            && ((Math.Abs(relX) == 3 && Math.Abs(relZ) <= 1) || (Math.Abs(relZ) == 3 && Math.Abs(relX) <= 1));
        return lateralRoots ? "rootwood" : null;
    }

    private static string? SampleEmberVent(BiomeDef biome, int relX, int relY, int relZ)
    {
        if (relX < -3 || relX > 3 || relZ < -3 || relZ > 3 || relY < -1 || relY > 4)
        {
            return null;
        }

        var distSq = relX * relX + relZ * relZ;
        if (relY == -1 && distSq <= 4)
        {
            return biome.SubsurfaceMaterialId;
        }

        if (relX == 0 && relZ == 0 && relY >= 0 && relY <= 4)
        {
            return "ember-vein";
        }

        if (relY == 0 && distSq >= 2 && distSq <= 8)
        {
            return biome.CoreMaterialId;
        }

        var flare = relY == 1 && distSq == 1;
        return flare ? "ember-vein" : null;
    }

    private static string? SampleSlagColumn(PlanetRuntime planet, BiomeDef biome, int relX, int relY, int relZ, int originX, int originZ)
    {
        if (relX < -2 || relX > 2 || relZ < -2 || relZ > 2 || relY < 0 || relY > 7)
        {
            return null;
        }

        var height = 4 + PositiveMod((int)(SampleUnit(planet.PlanetSeed, originX, originZ, 439) * 4), 4);
        if (Math.Abs(relX) <= 1 && Math.Abs(relZ) <= 1 && relY <= height)
        {
            return relX == 0 && relZ == 0 && relY >= 2
                ? "ember-vein"
                : biome.CoreMaterialId;
        }

        var slagSkirt = relY == 0 && relX * relX + relZ * relZ <= 4;
        return slagSkirt ? biome.SubsurfaceMaterialId : null;
    }

    private static string? SampleCrystalCluster(PlanetRuntime planet, int relX, int relY, int relZ, int originX, int originZ)
    {
        if (relX < -3 || relX > 3 || relZ < -3 || relZ > 3 || relY < 0 || relY > 7)
        {
            return null;
        }

        if (relY == 0 && relX * relX + relZ * relZ <= 3)
        {
            return "shardstone";
        }

        if (MatchesStem(planet, relX, relY, relZ, originX, originZ, 0, 0, 449, 5, 3)
            || MatchesStem(planet, relX, relY, relZ, originX, originZ, 2, 0, 457, 3, 3)
            || MatchesStem(planet, relX, relY, relZ, originX, originZ, -1, 1, 463, 2, 3)
            || MatchesStem(planet, relX, relY, relZ, originX, originZ, 0, -2, 467, 2, 2))
        {
            return "void-crystal";
        }

        return null;
    }

    private static string? SampleShardFan(BiomeDef biome, int relX, int relY, int relZ)
    {
        if (relX < -4 || relX > 4 || relZ < -2 || relZ > 2 || relY < 0 || relY > 5)
        {
            return null;
        }

        var fanHeight = 4 - Math.Abs(relX / 2);
        if (Math.Abs(relZ) == 0 && relY <= fanHeight)
        {
            return "void-crystal";
        }

        var skirt = relY == 0 && Math.Abs(relZ) == 1 && Math.Abs(relX) <= 3;
        return skirt ? biome.SubsurfaceMaterialId : null;
    }

    private static bool MatchesStem(
        PlanetRuntime planet,
        int relX,
        int relY,
        int relZ,
        int originX,
        int originZ,
        int offsetX,
        int offsetZ,
        int salt,
        int minHeight,
        int heightVariance)
    {
        if (relX != offsetX || relZ != offsetZ)
        {
            return false;
        }

        var height = minHeight + PositiveMod(
            (int)(SampleUnit(planet.PlanetSeed, originX + offsetX, originZ + offsetZ, salt) * heightVariance),
            heightVariance);
        return relY >= 0 && relY <= height;
    }

    private static string? SampleRootArch(BiomeDef biome, int relX, int relY, int relZ)
    {
        if (relZ != 0 || relY < 0 || relY > 4) return null;
        var pillar = relX == -2 || relX == 2;
        var lintel = relY == 4 && relX >= -2 && relX <= 2;
        return pillar || lintel ? biome.CoreMaterialId : null;
    }

    // Hollow ring cylinder (outer r²≤4, inner r²≥1), 6 blocks tall, with random decay.
    private static string? SampleRuinedBeacon(PlanetRuntime planet, BiomeDef biome, int relX, int relY, int relZ, int cellX, int cellZ)
    {
        if (relY < 0 || relY > 6) return null;
        var distSq = relX * relX + relZ * relZ;
        if (distSq < 1 || distSq > 4) return null;
        var filled = SampleUnit(planet.PlanetSeed, cellX * 200 + relX, relY * 13, cellZ * 200 + relZ, 131) >= 0.35;
        return filled ? biome.SubsurfaceMaterialId : null;
    }

    // Thin 1×1 pillar, 8–11 blocks tall.
    private static string? SampleAshSpire(PlanetRuntime planet, BiomeDef biome, int relX, int relY, int relZ, int cellX, int cellZ)
    {
        if (relX != 0 || relZ != 0) return null;
        var height = 8 + PositiveMod((int)(SampleUnit(planet.PlanetSeed, cellX, cellZ, 137) * 4), 4);
        return relY >= 0 && relY <= height ? biome.CoreMaterialId : null;
    }

    // Squat rubble mound (5×3×5 bounding box, ~40 % of voxels present).
    private static string? SampleBrokenFurnace(PlanetRuntime planet, BiomeDef biome, int relX, int relY, int relZ, int cellX, int cellZ)
    {
        if (relX < -2 || relX > 2 || relZ < -2 || relZ > 2 || relY < 0 || relY > 2) return null;
        var filled = SampleUnit(planet.PlanetSeed, cellX * 200 + relX, relY * 7, cellZ * 200 + relZ, 139) >= 0.40;
        return filled ? biome.SubsurfaceMaterialId : null;
    }

    // Thin vertical crack, 2 blocks below surface to 4 above.
    private static string? SampleCrystalFissure(BiomeDef biome, int relX, int relY, int relZ)
    {
        if (relX != 0 || relZ != 0) return null;
        return relY >= -2 && relY <= 4 ? biome.SurfaceMaterialId : null;
    }

    // 1×1 column, mostly buried: 3 blocks below surface to 5 above.
    private static string? SampleBuriedObelisk(BiomeDef biome, int relX, int relY, int relZ)
    {
        if (relX != 0 || relZ != 0) return null;
        return relY >= -3 && relY <= 5 ? biome.CoreMaterialId : null;
    }

    // Ore veins appear in the deep core layer as fractal noise blobs.
    // Each biome gets a distinct secondary material.
    private string? ResolveVeinMaterial(PlanetRuntime planet, Int3 worldPosition, int surfaceHeight, BiomeDef activeBiome)
    {
        var depthBelow = (surfaceHeight - worldPosition.Y) / GameplayVoxelsPerOriginalBlock;
        if (depthBelow < 6) return null;

        var scaledX = worldPosition.X / (double)GameplayVoxelsPerOriginalBlock;
        var scaledY = worldPosition.Y / (double)GameplayVoxelsPerOriginalBlock;
        var scaledZ = worldPosition.Z / (double)GameplayVoxelsPerOriginalBlock;

        var veinNoise = SampleFractalNoise3D(
            planet.PlanetSeed,
            scaledX * 0.24,
            scaledY * 0.20,
            scaledZ * 0.24,
            octaves: 2,
            lacunarity: 2.1,
            gain: 0.50,
            salt: 151);

        if (veinNoise < 0.86) return null;

        // Vein material matches the active biome so veins visually belong to their zone.
        return activeBiome.Id switch
        {
            "ember-reaches" when depthBelow >= 8 => "ember-vein",
            "sunken-wilds"                       => "rootwood",
            "glass-marrow"                       => "void-crystal",
            _                                    => null
        };
    }

    // Sparse circular craters carved from the surface height.
    // They expose cave systems and break up flat terrain.
    private double CalculateCraterDepression(PlanetRuntime planet, int x, int z)
    {
        const int CraterCellSizeBlocks = 28;
        var cellVoxelSize = CraterCellSizeBlocks * GameplayVoxelsPerOriginalBlock;
        var cellX = FloorDiv(x, cellVoxelSize);
        var cellZ = FloorDiv(z, cellVoxelSize);

        var maxDepression = 0.0;
        for (var dcx = -1; dcx <= 1; dcx++)
        {
            for (var dcz = -1; dcz <= 1; dcz++)
            {
                var cx = cellX + dcx;
                var cz = cellZ + dcz;
                if (SampleUnit(planet.PlanetSeed, cx, cz, 157) >= 0.20) continue;

                var margin   = (int)(cellVoxelSize * 0.20);
                var inner    = (int)(cellVoxelSize * 0.60);
                var originX  = cx * cellVoxelSize + margin + (int)(SampleUnit(planet.PlanetSeed, cx, cz, 159) * inner);
                var originZ  = cz * cellVoxelSize + margin + (int)(SampleUnit(planet.PlanetSeed, cx, cz, 163) * inner);
                var radius   = 3.0 + SampleUnit(planet.PlanetSeed, cx, cz, 167) * 3.0;
                var depth    = 3.0 + SampleUnit(planet.PlanetSeed, cx, cz, 173) * 5.0;

                var relX = (x - originX) / (double)GameplayVoxelsPerOriginalBlock;
                var relZ = (z - originZ) / (double)GameplayVoxelsPerOriginalBlock;
                var dist = Math.Sqrt(relX * relX + relZ * relZ);
                if (dist >= radius) continue;

                var t = 1.0 - dist / radius;
                maxDepression = Math.Max(maxDepression, depth * t * t);
            }
        }

        return maxDepression;
    }

    private static double CalculateBiomeAffinity(BiomeDef biome, BiomeFieldSample field)
    {
        var heatDistance = Math.Abs(field.Heat - biome.Heat);
        var moistureDistance = Math.Abs(field.Moisture - biome.Moisture);
        var reliefDistance = Math.Abs(field.Relief - biome.Relief);
        return (heatDistance * 1.15) + (moistureDistance * 1.30) + (reliefDistance * 0.90);
    }

    private BiomeFieldSample SampleBiomeField(PlanetRuntime planet, int x, int z)
    {
        var scaledX = x / (double)GameplayVoxelsPerOriginalBlock;
        var scaledZ = z / (double)GameplayVoxelsPerOriginalBlock;
        var heat = SampleFractalNoise2D(
            planet.PlanetSeed,
            scaledX * 0.010,
            scaledZ * 0.010,
            octaves: 3,
            lacunarity: 2.02,
            gain: 0.54,
            salt: 307);
        var moisture = SampleFractalNoise2D(
            planet.PlanetSeed,
            scaledX * 0.012,
            scaledZ * 0.012,
            octaves: 3,
            lacunarity: 2.08,
            gain: 0.56,
            salt: 313);
        var relief = CalculateRegion(planet, x, z);
        return new BiomeFieldSample(heat, moisture, relief);
    }

    private BiomeDef ResolveActiveBiome(PlanetRuntime planet, int x, int z)
    {
        var starterBiomeRadius = 24 * GameplayVoxelsPerOriginalBlock;
        if ((x * x) + (z * z) <= starterBiomeRadius * starterBiomeRadius)
        {
            return planet.BiomeRules;
        }

        var field = SampleBiomeField(planet, x, z);
        var primaryAffinity = CalculateBiomeAffinity(planet.BiomeRules, field);
        var secondaryAffinity = CalculateBiomeAffinity(planet.SecondaryBiomeRules, field);
        var scaledX = x / (double)GameplayVoxelsPerOriginalBlock;
        var scaledZ = z / (double)GameplayVoxelsPerOriginalBlock;
        var materialZone = SampleValueNoise2D(planet.PlanetSeed, scaledX * 0.018, scaledZ * 0.018, salt: 97);
        var climateBias = Math.Clamp(primaryAffinity - secondaryAffinity, -0.30, 0.30) * 0.22;
        return materialZone - climateBias > 0.52
            ? planet.SecondaryBiomeRules
            : planet.BiomeRules;
    }

    private LandformDef ResolveActiveLandform(PlanetRuntime planet, int x, int z)
    {
        var scaledX = x / (double)GameplayVoxelsPerOriginalBlock;
        var scaledZ = z / (double)GameplayVoxelsPerOriginalBlock;
        var landformZone = SmoothStep(SampleValueNoise2D(planet.PlanetSeed, scaledX * 0.010, scaledZ * 0.010, salt: 89));
        return landformZone > 0.54 ? planet.SecondaryLandformRules : planet.LandformRules;
    }

    // Scores candidate spawn positions by region value (prefer highlands).
    // Returns voxel-space XZ; Y is ignored — PlayerController finds the ground.
    public Int3 GetRecommendedSpawnPosition()
    {
        var spacing = SpawnSpacingOriginalBlocks * GameplayVoxelsPerOriginalBlock;
        var bestX   = 0;
        var bestZ   = 0;
        var bestScore = double.NegativeInfinity;

        for (var dx = -3; dx <= 3; dx++)
        {
            for (var dz = -3; dz <= 3; dz++)
            {
                var x      = dx * spacing;
                var z      = dz * spacing;
                var region = CalculateRegion(CurrentPlanet, x, z);
                var landform = ResolveActiveLandform(CurrentPlanet, x, z);
                var drainage = CalculateDrainageProfile(CurrentPlanet, x, z, landform, region);
                var centerHeight = CalculateSurfaceHeight(CurrentPlanet, x, z);
                var waterSurfaceHeight = CalculateWaterSurfaceHeight(
                    CurrentPlanet,
                    x,
                    z,
                    centerHeight,
                    landform,
                    region,
                    drainage);
                var probeStep = 2 * GameplayVoxelsPerOriginalBlock;
                var eastHeight = CalculateSurfaceHeight(CurrentPlanet, x + probeStep, z);
                var westHeight = CalculateSurfaceHeight(CurrentPlanet, x - probeStep, z);
                var northHeight = CalculateSurfaceHeight(CurrentPlanet, x, z - probeStep);
                var southHeight = CalculateSurfaceHeight(CurrentPlanet, x, z + probeStep);
                var neighborMean = (eastHeight + westHeight + northHeight + southHeight) / 4.0;
                var troughPenalty = Math.Max(0.0, (neighborMean - centerHeight) / (2.0 * GameplayVoxelsPerOriginalBlock));
                var deepTroughPenalty = neighborMean - centerHeight >= GameplayVoxelsPerOriginalBlock
                    ? 2.4 + ((neighborMean - centerHeight) / (double)GameplayVoxelsPerOriginalBlock)
                    : 0.0;
                var roughnessPenalty =
                    (Math.Abs(eastHeight - westHeight) + Math.Abs(northHeight - southHeight))
                    / (12.0 * GameplayVoxelsPerOriginalBlock);
                var floodedPenalty = waterSurfaceHeight > centerHeight
                    ? 1.4 + ((waterSurfaceHeight - centerHeight) / (double)GameplayVoxelsPerOriginalBlock)
                    : 0.0;
                // Slight penalty for sitting exactly at the world origin.
                var score  =
                    region
                    + (drainage.Moisture * 0.18)
                    - (drainage.ChannelStrength * 0.95)
                    - (drainage.BasinStrength * 0.35)
                    - (troughPenalty * 0.75)
                    - deepTroughPenalty
                    - (roughnessPenalty * 0.35)
                    - (floodedPenalty * 1.25)
                    - (dx == 0 && dz == 0 ? 0.1 : 0.0);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestX     = x;
                    bestZ     = z;
                }
            }
        }

        return new Int3(bestX, 0, bestZ);
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
                hash += (uint)value + 0x9e3779b97f4a7c15UL;
                hash = MixHash(hash);
            }

            hash = MixHash(hash);
            return (hash >> 11) * (1.0 / (1UL << 53));
        }
    }

    private static double SampleValueNoise2D(long seed, double x, double z, int salt)
    {
        var x0 = (int)Math.Floor(x);
        var z0 = (int)Math.Floor(z);
        var tx = SmoothStep(x - x0);
        var tz = SmoothStep(z - z0);

        var n00 = SampleUnit(seed, x0, z0, salt);
        var n10 = SampleUnit(seed, x0 + 1, z0, salt);
        var n01 = SampleUnit(seed, x0, z0 + 1, salt);
        var n11 = SampleUnit(seed, x0 + 1, z0 + 1, salt);

        var nx0 = Lerp(n00, n10, tx);
        var nx1 = Lerp(n01, n11, tx);
        return Lerp(nx0, nx1, tz);
    }

    private static double SampleValueNoise3D(long seed, double x, double y, double z, int salt)
    {
        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var z0 = (int)Math.Floor(z);
        var tx = SmoothStep(x - x0);
        var ty = SmoothStep(y - y0);
        var tz = SmoothStep(z - z0);

        var n000 = SampleUnit(seed, x0, y0, z0, salt);
        var n100 = SampleUnit(seed, x0 + 1, y0, z0, salt);
        var n010 = SampleUnit(seed, x0, y0 + 1, z0, salt);
        var n110 = SampleUnit(seed, x0 + 1, y0 + 1, z0, salt);
        var n001 = SampleUnit(seed, x0, y0, z0 + 1, salt);
        var n101 = SampleUnit(seed, x0 + 1, y0, z0 + 1, salt);
        var n011 = SampleUnit(seed, x0, y0 + 1, z0 + 1, salt);
        var n111 = SampleUnit(seed, x0 + 1, y0 + 1, z0 + 1, salt);

        var nx00 = Lerp(n000, n100, tx);
        var nx10 = Lerp(n010, n110, tx);
        var nx01 = Lerp(n001, n101, tx);
        var nx11 = Lerp(n011, n111, tx);
        var nxy0 = Lerp(nx00, nx10, ty);
        var nxy1 = Lerp(nx01, nx11, ty);
        return Lerp(nxy0, nxy1, tz);
    }

    private static double SampleFractalNoise2D(
        long seed,
        double x,
        double z,
        int octaves,
        double lacunarity,
        double gain,
        int salt)
    {
        var amplitude = 1.0;
        var frequency = 1.0;
        var total = 0.0;
        var maxValue = 0.0;

        for (var octave = 0; octave < octaves; octave++)
        {
            total += SampleValueNoise2D(seed, x * frequency, z * frequency, salt + (octave * 19)) * amplitude;
            maxValue += amplitude;
            amplitude *= gain;
            frequency *= lacunarity;
        }

        return maxValue <= 0.0 ? 0.0 : total / maxValue;
    }

    private static double SampleFractalNoise3D(
        long seed,
        double x,
        double y,
        double z,
        int octaves,
        double lacunarity,
        double gain,
        int salt)
    {
        var amplitude = 1.0;
        var frequency = 1.0;
        var total = 0.0;
        var maxValue = 0.0;

        for (var octave = 0; octave < octaves; octave++)
        {
            total += SampleValueNoise3D(seed, x * frequency, y * frequency, z * frequency, salt + (octave * 23)) * amplitude;
            maxValue += amplitude;
            amplitude *= gain;
            frequency *= lacunarity;
        }

        return maxValue <= 0.0 ? 0.0 : total / maxValue;
    }

    private static double SampleRidgedNoise2D(
        long seed,
        double x,
        double z,
        int octaves,
        double lacunarity,
        double gain,
        int salt)
    {
        var amplitude = 1.0;
        var frequency = 1.0;
        var total = 0.0;
        var maxValue = 0.0;

        for (var octave = 0; octave < octaves; octave++)
        {
            var value = SampleValueNoise2D(seed, x * frequency, z * frequency, salt + (octave * 29));
            var ridge = 1.0 - Math.Abs((value * 2.0) - 1.0);
            total += ridge * amplitude;
            maxValue += amplitude;
            amplitude *= gain;
            frequency *= lacunarity;
        }

        return maxValue <= 0.0 ? 0.0 : total / maxValue;
    }

    private static double SmoothStep(double value) => value * value * (3.0 - (2.0 * value));

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);

    private static ulong MixHash(ulong value)
    {
        value ^= value >> 30;
        value *= 0xbf58476d1ce4e5b9UL;
        value ^= value >> 27;
        value *= 0x94d049bb133111ebUL;
        value ^= value >> 31;
        return value;
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
