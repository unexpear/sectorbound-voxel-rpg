using Sectorbound.Engine;

namespace Sectorbound.Engine.Tests;

public sealed class GameSessionTests : IDisposable
{
    private readonly string _workspaceRoot;
    private readonly string _tempSaveRoot;
    private readonly ContentDefs _content;

    public GameSessionTests()
    {
        _workspaceRoot = Path.Combine(Path.GetTempPath(), "sectorbound-tests", Guid.NewGuid().ToString("N"));
        _tempSaveRoot = Path.Combine(_workspaceRoot, "saves");
        Directory.CreateDirectory(_tempSaveRoot);
        _content = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "Content"));
    }

    [Fact]
    public void ChunkEditsPersistAcrossSaveAndReload()
    {
        var session = GameSession.CreateNew(_content, _tempSaveRoot, "persist", 424242);
        session.TravelToPlanet(0);

        var digSite = session.GetSurfaceVoxel(0, 0);
        var placeSite = new Int3(0, digSite.Y + 1, 0);

        session.Mine(digSite);
        session.Player.Inventory["shelter-block"] = 1;
        session.PlaceBlock(placeSite, "shelter-block");
        session.Save();

        var loaded = GameSession.Load(_content, _tempSaveRoot, "persist");
        loaded.TravelToPlanet(0);

        Assert.Equal("air", loaded.GetMaterialIdAtCurrentPlanet(digSite));
        Assert.Equal("shelter-block", loaded.GetMaterialIdAtCurrentPlanet(placeSite));
    }

    [Fact]
    public void SectorTransitionCreatesThreePlanetsAndKeepsPriorSectorState()
    {
        var session = GameSession.CreateNew(_content, _tempSaveRoot, "sector", 5555);
        session.TravelToPlanet(0);
        var digSite = session.GetSurfaceVoxel(1, 1);
        session.Mine(digSite);
        session.Save();

        var startSeed = session.Sector.ActiveSectorSeed;
        var transition = session.AdvanceToNextSector();

        Assert.Equal(3, session.GetPlanetSummaries().Count);
        Assert.NotEqual(startSeed, session.Sector.ActiveSectorSeed);
        Assert.Equal(0, session.CurrentPlanetIndex);
        Assert.Equal(1, transition.ToSectorIndex);

        session.Save();
        var reloaded = GameSession.Load(_content, _tempSaveRoot, "sector");
        reloaded.TravelToPlanet(0);
        Assert.Equal(1, reloaded.CurrentSectorIndex);

        var originalSector = GameSession.Load(_content, _tempSaveRoot, "sector");
        originalSector.TravelToPlanet(0);
        Assert.Equal(1, originalSector.CurrentSectorIndex);
    }

    [Fact]
    public void MiningCombatAndCraftingAdvanceTheirSkillTracks()
    {
        var session = GameSession.CreateNew(_content, _tempSaveRoot, "skills", 7777);
        session.TravelToPlanet(0);

        MineShelterColumn(session, 0, 0);
        MineShelterColumn(session, 1, 0);
        session.Craft("workbench-block");
        var surface = session.GetSurfaceVoxel(0, 0);
        session.PlaceBlock(new Int3(surface.X, surface.Y + 1, surface.Z + 1), "workbench-block");
        session.FightEnemy(session.CurrentPlanet.SpawnTables.First(), CombatApproach.Blade);
        session.Craft("shelter-block");

        Assert.True(session.GetSkillProgress("mining").LifetimeExperience > 0);
        Assert.True(session.GetSkillProgress("combat").LifetimeExperience > 0);
        Assert.True(session.GetSkillProgress("crafting").LifetimeExperience > 0);
    }

    [Fact]
    public void ShelterCraftingRequiresWorkbenchPlacedOnCurrentPlanet()
    {
        var session = GameSession.CreateNew(_content, _tempSaveRoot, "workbench", 7788);
        session.TravelToPlanet(0);

        MineShelterColumn(session, 0, 0);
        MineShelterColumn(session, 1, 0);
        var craftedWorkbench = session.Craft("workbench-block");
        var blockedCraft = session.Craft("shelter-block");

        var surface = session.GetSurfaceVoxel(0, 0);
        var workbenchSpot = new Int3(surface.X, surface.Y + 1, surface.Z + 1);
        var placedWorkbench = session.PlaceBlock(workbenchSpot, "workbench-block");
        var unlockedCraft = session.Craft("shelter-block");

        Assert.True(craftedWorkbench.Success);
        Assert.False(blockedCraft.Success);
        Assert.Contains("Workbench", blockedCraft.Message);
        Assert.True(placedWorkbench.Success);
        Assert.True(unlockedCraft.Success);
    }

    [Fact]
    public void BossBonusesApplyOnlyOnceAndSurviveReload()
    {
        var session = GameSession.CreateNew(_content, _tempSaveRoot, "boss", 8888);
        session.TravelToPlanet(0);

        MineShelterColumn(session, 0, 0);
        MineShelterColumn(session, 1, 0);
        session.FightEnemy(session.CurrentPlanet.SpawnTables.First(), CombatApproach.Blade);
        session.TravelToPlanet(1);
        session.FightEnemy(session.CurrentPlanet.SpawnTables.First(), CombatApproach.Bow);
        session.TravelToPlanet(2);
        session.FightEnemy(session.CurrentPlanet.SpawnTables.First(), CombatApproach.Sigil);

        var firstKill = session.FightSectorBoss(CombatApproach.Sigil);
        var combatXpAfterFirstKill = session.GetSkillProgress("combat").LifetimeExperience;
        var secondKill = session.FightSectorBoss(CombatApproach.Sigil);

        session.Save();
        var loaded = GameSession.Load(_content, _tempSaveRoot, "boss");

        Assert.True(firstKill.Success);
        Assert.True(secondKill.Success);
        Assert.Equal(combatXpAfterFirstKill + 50, session.GetSkillProgress("combat").LifetimeExperience);
        Assert.Contains("void-wyrm", loaded.Player.BossBonuses);
        Assert.Contains("void-strider", loaded.Player.PerkUnlocks);
    }

    [Fact]
    public void BuildingIsAllowedAcrossAllThreePlanets()
    {
        var session = GameSession.CreateNew(_content, _tempSaveRoot, "build", 9999);

        for (var planetIndex = 0; planetIndex < 3; planetIndex++)
        {
            session.TravelToPlanet(planetIndex);
            var surface = session.GetSurfaceVoxel(planetIndex, 0);
            var buildSite = new Int3(surface.X, surface.Y + 1, surface.Z);
            session.Player.Inventory["shelter-block"] = session.GetInventoryCount("shelter-block") + 1;
            var result = session.PlaceBlock(buildSite, "shelter-block");

            Assert.True(result.Success);
            Assert.Equal("shelter-block", session.GetMaterialIdAtCurrentPlanet(buildSite));
        }
    }

    [Fact]
    public void DifficultyPresetsChangeDeathPenalties()
    {
        var easySession = GameSession.CreateNew(_content, _tempSaveRoot, "easy", 1001, "wayfarer");
        easySession.Player.Inventory["timber"] = 10;
        var easyPenalty = easySession.TriggerDeath();

        var hardSession = GameSession.CreateNew(_content, _tempSaveRoot, "hard", 1002, "hardcore");
        hardSession.Player.Inventory["timber"] = 10;
        var hardPenalty = hardSession.TriggerDeath();

        Assert.True(hardPenalty.LostItems["timber"] > easyPenalty.LostItems["timber"]);
        Assert.False(easyPenalty.CorpseRunEnabled);
        Assert.True(hardPenalty.CorpseRunEnabled);
    }

    [Fact]
    public void WorldQueriesExposeStableChunkCoordinatesAcrossBoundaries()
    {
        var session = GameSession.CreateNew(_content, _tempSaveRoot, "chunks", 1003);

        Assert.Equal(9, session.VoxelsPerOriginalBlock);
        Assert.Equal(36, session.ChunkSizeInVoxels);
        Assert.Equal(0, session.MinWorldY);
        Assert.Equal(432, session.MaxWorldY);
        Assert.Equal(new ChunkCoordinate(0, 0, 0), session.GetChunkCoordinateForWorldPosition(new Int3(0, 0, 0)));
        Assert.Equal(new ChunkCoordinate(0, 0, 0), session.GetChunkCoordinateForWorldPosition(new Int3(35, 35, 35)));
        Assert.Equal(new ChunkCoordinate(-1, 0, -1), session.GetChunkCoordinateForWorldPosition(new Int3(-1, 4, -1)));
        Assert.Equal(new ChunkCoordinate(1, 0, 1), session.GetChunkCoordinateForWorldPosition(new Int3(36, 2, 36)));
    }

    [Fact]
    public void PlanetsExposeNoticeableTerrainVariation()
    {
        var session = GameSession.CreateNew(_content, _tempSaveRoot, "terrain", 1005);

        for (var planetIndex = 0; planetIndex < 3; planetIndex++)
        {
            session.TravelToPlanet(planetIndex);
            var heights =
                (from x in Enumerable.Range(-24, 49)
                 from z in Enumerable.Range(-24, 49)
                 select session.GetSurfaceHeight(x, z)).ToArray();

            var range = heights.Max() - heights.Min();
            var samples = new[]
            {
                session.GetSurfaceHeight(-24, -24),
                session.GetSurfaceHeight(-12, 9),
                session.GetSurfaceHeight(0, 0),
                session.GetSurfaceHeight(11, -7),
                session.GetSurfaceHeight(24, 24)
            };
            Assert.True(range >= 8, $"Planet {planetIndex} terrain range was {range}. Samples: {string.Join(", ", samples)}");
        }
    }

    [Fact]
    public void PlanetArchetypesLoadBiomesAndLandformsSeparately()
    {
        var session = GameSession.CreateNew(_content, _tempSaveRoot, "identity", 1006);
        session.TravelToPlanet(0);

        Assert.Equal("sunken-wilds", session.CurrentPlanet.BiomeRules.Id);
        Assert.Equal("ember-reaches", session.CurrentPlanet.SecondaryBiomeRules.Id);
        Assert.Equal("rooted-highlands", session.CurrentPlanet.LandformRules.Id);
        Assert.Equal("ash-badlands", session.CurrentPlanet.SecondaryLandformRules.Id);
        Assert.Contains("root-arch", session.CurrentPlanet.StructureGenerators);
        Assert.Contains("ash-spire", session.CurrentPlanet.StructureGenerators);
        Assert.Contains("reed-cluster", session.CurrentPlanet.BiomeFeatureGenerators);
        Assert.Contains("ember-vent", session.CurrentPlanet.BiomeFeatureGenerators);
    }

    [Fact]
    public void EnemyAndBossScaleMetadataIsExplicitlyAuthoredInMeters()
    {
        Assert.Equal(1.05f, _content.GetEnemy("burrowling").Scale.HeightMeters, precision: 2);
        Assert.Equal(0.92f, _content.GetEnemy("cinderbat").Scale.HeightMeters, precision: 2);
        Assert.Equal(2.35f, _content.GetEnemy("shardling").Scale.HeightMeters, precision: 2);
        Assert.Equal(4.8f, _content.GetBoss("void-wyrm").Scale.HeightMeters, precision: 2);

        Assert.All(_content.Enemies.Values, enemy =>
        {
            Assert.True(enemy.Scale.HeightMeters > 0f);
            Assert.True(enemy.Scale.WidthMeters > 0f);
            Assert.True(enemy.Scale.DepthMeters > 0f);
        });

        Assert.All(_content.Bosses.Values, boss =>
        {
            Assert.True(boss.Scale.HeightMeters > 0f);
            Assert.True(boss.Scale.WidthMeters > 0f);
            Assert.True(boss.Scale.DepthMeters > 0f);
        });
    }

    [Fact]
    public void BiomesDeclareTwoUniqueFeatureGeneratorsEach()
    {
        var allFeatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var biome in _content.Biomes.Values)
        {
            var featureGenerators = biome.FeatureGenerators ?? [];
            Assert.Equal(2, featureGenerators.Count);

            foreach (var featureId in featureGenerators)
            {
                Assert.True(allFeatures.Add(featureId), $"Feature '{featureId}' was reused across biomes.");
            }
        }
    }

    [Fact]
    public void PlanetsGenerateDrainageCutsAndSpawnSitesAvoidTroughs()
    {
        var session = GameSession.CreateNew(_content, _tempSaveRoot, "drainage", 1007);

        for (var planetIndex = 0; planetIndex < 3; planetIndex++)
        {
            session.TravelToPlanet(planetIndex);
            var sampleStep = 2 * session.VoxelsPerOriginalBlock;
            var troughThreshold = session.VoxelsPerOriginalBlock;
            var cutCount = 0;

            for (var x = -96; x <= 96; x += sampleStep)
            {
                for (var z = -96; z <= 96; z += sampleStep)
                {
                    var center = session.GetSurfaceHeight(x, z);
                    var neighborMean =
                        (session.GetSurfaceHeight(x + sampleStep, z)
                         + session.GetSurfaceHeight(x - sampleStep, z)
                         + session.GetSurfaceHeight(x, z + sampleStep)
                         + session.GetSurfaceHeight(x, z - sampleStep))
                        / 4.0;
                    if (neighborMean - center >= troughThreshold)
                    {
                        cutCount++;
                    }
                }
            }

            Assert.True(cutCount >= 2, $"Planet {planetIndex} exposed only {cutCount} drainage cuts.");

            var spawn = session.GetRecommendedSpawnPosition();
            var spawnHeight = session.GetSurfaceHeight(spawn.X, spawn.Z);
            var spawnNeighborMean =
                (session.GetSurfaceHeight(spawn.X + sampleStep, spawn.Z)
                 + session.GetSurfaceHeight(spawn.X - sampleStep, spawn.Z)
                 + session.GetSurfaceHeight(spawn.X, spawn.Z + sampleStep)
                 + session.GetSurfaceHeight(spawn.X, spawn.Z - sampleStep))
                / 4.0;
            Assert.True(
                spawnHeight >= spawnNeighborMean - troughThreshold,
                $"Planet {planetIndex} spawn settled in a trough: center={spawnHeight}, neighbors={spawnNeighborMean:0.0}.");
        }
    }

    [Fact]
    public void PlanetsGenerateSurfaceWaterAndSpawnsStayAboveIt()
    {
        var session = GameSession.CreateNew(_content, _tempSaveRoot, "water", 1009);
        var waterBearingPlanets = 0;

        for (var planetIndex = 0; planetIndex < 3; planetIndex++)
        {
            session.TravelToPlanet(planetIndex);
            var waterTiles = 0;
            var sampleStep = 2 * session.VoxelsPerOriginalBlock;

            for (var x = -128; x <= 128; x += sampleStep)
            {
                for (var z = -128; z <= 128; z += sampleStep)
                {
                    var surface = session.GetSurfaceHeight(x, z);
                    var aboveSurface = new Int3(x, surface + 1, z);
                    if (string.Equals(session.GetMaterialIdAtCurrentPlanet(aboveSurface), "water", StringComparison.OrdinalIgnoreCase))
                    {
                        waterTiles++;
                    }
                }
            }

            if (waterTiles >= 4)
            {
                waterBearingPlanets++;
            }

            var spawn = session.GetRecommendedSpawnPosition();
            var spawnSurface = session.GetSurfaceHeight(spawn.X, spawn.Z);
            Assert.NotEqual(
                "water",
                session.GetMaterialIdAtCurrentPlanet(new Int3(spawn.X, spawnSurface + 1, spawn.Z)));
        }

        Assert.True(waterBearingPlanets >= 2, $"Only {waterBearingPlanets} planets generated sampled surface water.");
    }

    [Fact]
    public void PlanetsExposeGuaranteedLandmarksWithSolidSignatureBlocks()
    {
        var session = GameSession.CreateNew(_content, _tempSaveRoot, "landmarks", 1008);

        for (var planetIndex = 0; planetIndex < 3; planetIndex++)
        {
            session.TravelToPlanet(planetIndex);
            Assert.True(session.CurrentPlanet.Landmarks.Count >= 2, $"Planet {planetIndex} had too few landmarks.");

            foreach (var landmark in session.CurrentPlanet.Landmarks)
            {
                var surface = session.GetSurfaceHeight(landmark.OriginX, landmark.OriginZ);
                var probe = GetLandmarkProbe(session, landmark, surface);
                Assert.True(
                    session.IsSolidAtCurrentPlanet(probe),
                    $"Planet {planetIndex} landmark {landmark.StructureId} was missing its probe voxel at {probe}.");
            }
        }
    }

    [Fact]
    public void PlanetsGenerateBiomeOwnedFeaturesAcrossTheLoadedSlice()
    {
        var session = GameSession.CreateNew(_content, _tempSaveRoot, "biome-features", 1011);
        var expectedFeatures = _content.Biomes.Values
            .SelectMany(biome => biome.FeatureGenerators ?? [])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var foundFeatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sampleStep = 2 * session.VoxelsPerOriginalBlock;

        for (var planetIndex = 0; planetIndex < 3; planetIndex++)
        {
            session.TravelToPlanet(planetIndex);

            for (var x = -256; x <= 256; x += sampleStep)
            {
                for (var z = -256; z <= 256; z += sampleStep)
                {
                    var featureId = session.GetBiomeFeatureIdAtCurrentPlanet(x, z);
                    if (!string.IsNullOrWhiteSpace(featureId))
                    {
                        foundFeatures.Add(featureId);
                    }
                }
            }
        }

        foreach (var expectedFeature in expectedFeatures)
        {
            Assert.Contains(expectedFeature, foundFeatures);
        }
    }

    [Fact]
    public void PlanetsGenerateCuratedScenicPropsStayGroundedAndOutOfWater()
    {
        var session = GameSession.CreateNew(_content, _tempSaveRoot, "scenic-props", 1012);
        var expectedAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "nature-bamboo",
            "nature-tree-stump-round",
            "nature-rock-tall4",
            "nature-rock-large4",
            "nature-stone-obelisk",
            "nature-stone-column"
        };
        var foundAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var planetIndex = 0; planetIndex < 3; planetIndex++)
        {
            session.TravelToPlanet(planetIndex);

            for (var chunkX = -20; chunkX <= 20; chunkX++)
            {
                for (var chunkZ = -20; chunkZ <= 20; chunkZ++)
                {
                    foreach (var prop in session.GetScenicPropsForCurrentPlanet(chunkX, chunkZ))
                    {
                        foundAssets.Add(prop.AssetId);
                        Assert.NotEqual("water", session.GetMaterialIdAtCurrentPlanet(prop.AnchorVoxel));
                        Assert.True(
                            session.IsSolidAtCurrentPlanet(new Int3(prop.AnchorVoxel.X, prop.AnchorVoxel.Y - 1, prop.AnchorVoxel.Z)),
                            $"Prop '{prop.AssetId}' floated at {prop.AnchorVoxel}.");
                    }
                }
            }
        }

        foreach (var expectedAsset in expectedAssets)
        {
            Assert.Contains(expectedAsset, foundAssets);
        }
    }

    [Fact]
    public void PlanetsGenerateLandmarkMatchedScenicPropDressing()
    {
        var session = GameSession.CreateNew(_content, _tempSaveRoot, "landmark-props", 1013);

        for (var planetIndex = 0; planetIndex < 3; planetIndex++)
        {
            session.TravelToPlanet(planetIndex);
            var landmarkPropsOnPlanet = 0;

            foreach (var landmark in session.CurrentPlanet.Landmarks)
            {
                var nearbyProps = GetScenicPropsNearLandmark(session, landmark);
                string[] expectedAssets = landmark.StructureId switch
                {
                    "root-arch" => ["nature-bamboo", "nature-tree-stump-round"],
                    "ruined-beacon" => ["nature-tree-stump-round", "nature-bamboo"],
                    "ash-spire" => ["nature-rock-tall4", "nature-rock-large4"],
                    "broken-furnace" => ["nature-rock-large4", "nature-rock-tall4"],
                    "crystal-fissure" => ["nature-stone-obelisk", "nature-stone-column"],
                    "buried-obelisk" => ["nature-stone-column", "nature-stone-obelisk"],
                    _ => Array.Empty<string>()
                };

                if (nearbyProps.Count == 0)
                {
                    continue;
                }

                landmarkPropsOnPlanet += nearbyProps.Count;
                Assert.All(
                    nearbyProps,
                    prop => Assert.Contains(
                        expectedAssets,
                        expectedAsset => string.Equals(expectedAsset, prop.AssetId, StringComparison.OrdinalIgnoreCase)));
            }

            Assert.True(landmarkPropsOnPlanet > 0, $"Planet {planetIndex} produced no landmark-adjacent scenic props.");
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void WorldGenSlicesSupportOneToFiveSlices(int sliceCount)
    {
        var session = GameSession.CreateNew(_content, _tempSaveRoot, $"slice-span-{sliceCount}", 1200 + sliceCount);
        session.TravelToPlanet(0);

        var origin = GetSliceTestOrigin(session, 0, 0);
        var slices = session.GenerateCurrentPlanetSlices(origin, sliceCount, sliceHeightVoxels: 4, sliceDepthVoxels: 3);

        Assert.Equal(sliceCount, slices.Count);

        for (var sliceIndex = 0; sliceIndex < slices.Count; sliceIndex++)
        {
            Assert.Equal(sliceIndex, slices[sliceIndex].SliceIndex);
            Assert.Equal(origin.X + sliceIndex, slices[sliceIndex].WorldX);
            Assert.Equal(12, slices[sliceIndex].Blocks.Count);
        }

        Assert.All(slices[0].Blocks, block => Assert.False(block.PreviousSliceTouchingBlock.HasValue));
    }

    [Fact]
    public void WorldGenSlicesCarryTouchingBlockLinksFromThePreviousSlice()
    {
        var session = GameSession.CreateNew(_content, _tempSaveRoot, "slice-links", 1206);
        session.TravelToPlanet(0);

        var origin = GetSliceTestOrigin(session, 2, -2);
        var slices = session.GenerateCurrentPlanetSlices(origin, 5, sliceHeightVoxels: 5, sliceDepthVoxels: 3);

        for (var sliceIndex = 1; sliceIndex < slices.Count; sliceIndex++)
        {
            var previousBlocks = slices[sliceIndex - 1].Blocks.ToDictionary(
                block => (block.Position.Y, block.Position.Z),
                block => block);

            foreach (var block in slices[sliceIndex].Blocks)
            {
                Assert.True(block.PreviousSliceTouchingBlock.HasValue, $"Block {block.Position} was missing its previous-slice link.");

                var link = block.PreviousSliceTouchingBlock.Value;
                var expectedPreviousBlock = previousBlocks[(block.Position.Y, block.Position.Z)];

                Assert.Equal(new Int3(block.Position.X - 1, block.Position.Y, block.Position.Z), link.Position);
                Assert.Equal(expectedPreviousBlock.Position, link.Position);
                Assert.Equal(expectedPreviousBlock.MaterialId, link.MaterialId);
                Assert.Equal(expectedPreviousBlock.IsSolid, link.IsSolid);
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void WorldGenSlicesRejectCountsOutsideOneToFive(int sliceCount)
    {
        var session = GameSession.CreateNew(_content, _tempSaveRoot, $"slice-range-{sliceCount}", 1212 + sliceCount);
        session.TravelToPlanet(0);

        var origin = GetSliceTestOrigin(session, 0, 0);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => session.GenerateCurrentPlanetSlices(origin, sliceCount, sliceHeightVoxels: 2, sliceDepthVoxels: 2));
    }

    [Fact]
    public void VerticalSliceProgressPersistsAcrossSaveReloadAndSectorTravel()
    {
        var session = GameSession.CreateNew(_content, _tempSaveRoot, "progress", 1004);
        session.TravelToPlanet(0);

        MineShelterColumn(session, 0, 0);
        MineShelterColumn(session, 1, 0);
        session.Craft("workbench-block");

        var surface = session.GetSurfaceVoxel(0, 0);
        session.PlaceBlock(new Int3(surface.X, surface.Y + 1, surface.Z + 1), "workbench-block");
        session.Craft("shelter-block");
        for (var offset = 1; offset <= 4; offset++)
        {
            session.PlaceBlock(new Int3(surface.X + offset, surface.Y + 1, surface.Z), "shelter-block");
        }

        session.FightEnemy(session.CurrentPlanet.SpawnTables.First(), CombatApproach.Blade);
        session.TravelToPlanet(1);
        session.FightEnemy(session.CurrentPlanet.SpawnTables.First(), CombatApproach.Bow);
        session.TravelToPlanet(2);
        session.FightEnemy(session.CurrentPlanet.SpawnTables.First(), CombatApproach.Sigil);
        session.FightSectorBoss(CombatApproach.Sigil);
        session.AdvanceToNextSector();
        session.Save();

        var loaded = GameSession.Load(_content, _tempSaveRoot, "progress");
        var progress = loaded.GetVerticalSliceProgress().ToDictionary(x => x.Id);

        Assert.True(progress["mine-anything"].IsComplete);
        Assert.True(progress["craft-shelter"].IsComplete);
        Assert.True(progress["build-shelter"].IsComplete);
        Assert.True(progress["place-workbench"].IsComplete);
        Assert.True(progress["enemy-families"].IsComplete);
        Assert.True(progress["boss-kill"].IsComplete);
        Assert.True(progress["next-sector"].IsComplete);
        Assert.Contains("crawler", loaded.Player.DefeatedEnemyFamilies);
        Assert.Contains("flier", loaded.Player.DefeatedEnemyFamilies);
        Assert.Contains("construct", loaded.Player.DefeatedEnemyFamilies);
    }

    [Fact]
    public void LoadingScaleMismatchedSaveRegeneratesSectorDataWithoutBreakingPlayerProgress()
    {
        var session = GameSession.CreateNew(_content, _tempSaveRoot, "legacy-scale", 1010);
        session.TravelToPlanet(0);

        var surface = session.GetSurfaceVoxel(0, 0);
        var buildSite = new Int3(surface.X, surface.Y + 1, surface.Z);
        session.Player.Inventory["fiber"] = 23;
        session.Player.Inventory["shelter-block"] = 1;
        session.PlaceBlock(buildSite, "shelter-block");
        session.Save();

        var savePath = GameSession.GetSavePath(_tempSaveRoot, "legacy-scale");
        var legacyJson = File.ReadAllText(savePath)
            .Replace("\"VoxelsPerOriginalBlock\": 9", "\"VoxelsPerOriginalBlock\": 6", StringComparison.Ordinal)
            .Replace("\"ChunkSizeInVoxels\": 36", "\"ChunkSizeInVoxels\": 16", StringComparison.Ordinal);
        File.WriteAllText(savePath, legacyJson);

        var loaded = GameSession.Load(_content, _tempSaveRoot, "legacy-scale");
        loaded.TravelToPlanet(0);

        Assert.Equal(23, loaded.GetInventoryCount("fiber"));
        Assert.NotEqual("shelter-block", loaded.GetMaterialIdAtCurrentPlanet(buildSite));
    }

    private static Int3 GetSliceTestOrigin(GameSession session, int x, int z)
    {
        var surface = session.GetSurfaceVoxel(x, z);
        return new Int3(surface.X - 1, Math.Max(session.MinWorldY, surface.Y - 2), surface.Z - 1);
    }

    private static Int3 GetLandmarkProbe(GameSession session, PlanetLandmark landmark, int surfaceHeight)
    {
        var scale = session.VoxelsPerOriginalBlock;
        var (offsetX, offsetY, offsetZ) = landmark.StructureId switch
        {
            "root-arch" => (0, 6, 0),
            "ruined-beacon" => (4, 4, 0),
            "ash-spire" => (0, 10, 0),
            "broken-furnace" => (0, 3, 0),
            "crystal-fissure" => (0, 2, 0),
            "buried-obelisk" => (0, 4, 0),
            _ => (0, 1, 0)
        };

        return new Int3(
            landmark.OriginX + (offsetX * scale),
            surfaceHeight + (offsetY * scale),
            landmark.OriginZ + (offsetZ * scale));
    }

    private static IReadOnlyList<ScenicPropInstance> GetScenicPropsNearLandmark(GameSession session, PlanetLandmark landmark)
    {
        var center = new Int3(landmark.OriginX, session.GetSurfaceHeight(landmark.OriginX, landmark.OriginZ), landmark.OriginZ);
        var centerChunk = session.GetChunkCoordinateForWorldPosition(center);
        var searchRadius = (landmark.RadiusInOriginalBlocks + 4) * session.VoxelsPerOriginalBlock;
        var props = new Dictionary<(string AssetId, Int3 AnchorVoxel), ScenicPropInstance>();

        for (var chunkX = centerChunk.X - 2; chunkX <= centerChunk.X + 2; chunkX++)
        {
            for (var chunkZ = centerChunk.Z - 2; chunkZ <= centerChunk.Z + 2; chunkZ++)
            {
                foreach (var prop in session.GetScenicPropsForCurrentPlanet(chunkX, chunkZ))
                {
                    if (Math.Abs(prop.AnchorVoxel.X - landmark.OriginX) > searchRadius
                        || Math.Abs(prop.AnchorVoxel.Z - landmark.OriginZ) > searchRadius)
                    {
                        continue;
                    }

                    props[(prop.AssetId, prop.AnchorVoxel)] = prop;
                }
            }
        }

        return props.Values.ToArray();
    }

    private static void MineShelterColumn(GameSession session, int x, int z)
    {
        var surface = session.GetSurfaceVoxel(x, z);
        session.Mine(surface);
        session.Mine(new Int3(x, surface.Y - 1, z));
        session.Mine(new Int3(x, surface.Y - 2, z));
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspaceRoot))
        {
            Directory.Delete(_workspaceRoot, recursive: true);
        }
    }
}
