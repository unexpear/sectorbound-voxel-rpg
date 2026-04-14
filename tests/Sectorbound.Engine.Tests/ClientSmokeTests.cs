using Sectorbound.Client;
using System.Numerics;

namespace Sectorbound.Engine.Tests;

public sealed class ClientSmokeTests : IDisposable
{
    private readonly string _workspaceRoot;

    public ClientSmokeTests()
    {
        _workspaceRoot = Path.Combine(Path.GetTempPath(), "sectorbound-client-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workspaceRoot);
    }

    [Fact]
    public void InputBindings_MapBracketKeysToMouseSensitivityShortcuts()
    {
        Assert.True(ClientInputBindings.IsDecreaseMouseSensitivityKey(Keys.Oem4));
        Assert.False(ClientInputBindings.IsDecreaseMouseSensitivityKey(Keys.Oem6));
        Assert.True(ClientInputBindings.IsIncreaseMouseSensitivityKey(Keys.Oem6));
        Assert.False(ClientInputBindings.IsIncreaseMouseSensitivityKey(Keys.Oem4));
    }

    [Fact]
    public void ClientSettings_SaveAndLoadRoundTripNormalizedPreferences()
    {
        var settingsPath = Path.Combine(_workspaceRoot, "settings.json");
        var settings = new ClientSettings
        {
            InvertMouseY = false,
            ViewDistanceBlocks = ClientSettings.MaxViewDistanceBlocks + 20,
            PreferredDifficultyPresetId = " HARDCORE ",
            MouseSensitivity = ClientSettings.MaxMouseSensitivity + 0.50f
        };
        settings.SetCharacterSelection(new CharacterSelection("female", "buns"));

        Assert.True(settings.TrySave(settingsPath));

        var loaded = ClientSettings.Load(settingsPath);
        var selection = loaded.GetCharacterSelection();

        Assert.False(loaded.InvertMouseY);
        Assert.Equal(ClientSettings.MaxViewDistanceBlocks, loaded.ViewDistanceBlocks);
        Assert.Equal(ClientSettings.MaxMouseSensitivity, loaded.MouseSensitivity);
        Assert.Equal("hardcore", loaded.GetPreferredDifficultyPresetId());
        Assert.Equal("female", selection.BodyId);
        Assert.Equal("buns", selection.HairId);
    }

    [Fact]
    public void ImportedVoxelAssets_LoadBundledPlayerAndCharacterModels()
    {
        var defaultAsset = ImportedVoxelAssets.GetPlayerDefault();
        var selectedAsset = ImportedVoxelAssets.GetCharacter(new CharacterSelection("female", "parted"));
        var selectedProfileAsset = ImportedVoxelAssets.GetActor(
            ActorRenderProfileCatalog.GetPlayerProfile(new CharacterSelection("female", "parted")));

        Assert.NotNull(defaultAsset);
        Assert.NotNull(selectedAsset);
        Assert.NotNull(selectedProfileAsset);
        Assert.True(defaultAsset!.Faces.Count > 0);
        Assert.True(selectedAsset!.Faces.Count > 0);
        Assert.True(selectedProfileAsset!.Faces.Count > 0);
        Assert.True(defaultAsset.WidthCubes > 0);
        Assert.True(selectedAsset.HeightCubes > 0);
    }

    [Fact]
    public void ImportedVoxelAssets_LoadBundledScenicProps()
    {
        var bamboo = ImportedVoxelAssets.GetProp("nature-bamboo");
        var obelisk = ImportedVoxelAssets.GetProp("nature-stone-obelisk");

        Assert.NotNull(bamboo);
        Assert.NotNull(obelisk);
        Assert.True(bamboo!.Faces.Count > 0);
        Assert.True(obelisk!.Faces.Count > 0);
    }

    [Fact]
    public void ChunkMeshBuilder_AppendsCuratedScenicPropFaces()
    {
        var content = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "Content"));
        var session = GameSession.CreateNew(content, _workspaceRoot, "chunk-props", 5150);
        session.TravelToPlanet(0);

        var builder = new ChunkMeshBuilder();
        ChunkColumnKey? targetKey = null;
        IReadOnlyList<ScenicPropInstance> chunkProps = [];

        for (var chunkX = -10; chunkX <= 10 && targetKey is null; chunkX++)
        {
            for (var chunkZ = -10; chunkZ <= 10; chunkZ++)
            {
                var props = session.GetScenicPropsForCurrentPlanet(chunkX, chunkZ);
                if (props.Count == 0)
                {
                    continue;
                }

                targetKey = new ChunkColumnKey(chunkX, chunkZ);
                chunkProps = props;
                break;
            }
        }

        Assert.True(targetKey.HasValue);

        var mesh = builder.Build(session, targetKey.Value);
        foreach (var prop in chunkProps)
        {
            Assert.Contains(
                mesh.Faces,
                face => string.Equals(face.MaterialId, prop.AssetId, StringComparison.OrdinalIgnoreCase)
                    && !face.CanTarget);
        }
    }

    [Fact]
    public void ChunkSliceVolume_SamplesChunkPrewarmDataFromBatchedWorldgenSlices()
    {
        var content = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "Content"));
        var session = GameSession.CreateNew(content, _workspaceRoot, "chunk-slices", 5151);
        session.TravelToPlanet(0);

        var key = new ChunkColumnKey(0, 0);
        var volume = ChunkSliceVolume.SampleCurrentPlanet(session, key);
        var chunkSize = session.ChunkSizeInVoxels;
        var expectedBatchCount = ChunkSliceVolume.GetSliceBatchCount(session);
        var sampleXs = new[]
        {
            (key.X * chunkSize) - 1,
            key.X * chunkSize,
            (key.X * chunkSize) + (chunkSize / 2),
            (key.X * chunkSize) + chunkSize - 1,
            (key.X * chunkSize) + chunkSize
        };
        var sampleZs = new[]
        {
            (key.Z * chunkSize) - 1,
            key.Z * chunkSize,
            (key.Z * chunkSize) + (chunkSize / 2),
            (key.Z * chunkSize) + chunkSize - 1,
            (key.Z * chunkSize) + chunkSize
        };

        Assert.Equal(expectedBatchCount, volume.SliceBatchCount);

        foreach (var x in sampleXs.Distinct())
        {
            foreach (var z in sampleZs.Distinct())
            {
                var surface = session.GetSurfaceHeight(x, z);
                var sampleYs = new[]
                {
                    session.MinWorldY,
                    Math.Clamp(surface - 1, session.MinWorldY, session.MaxWorldY),
                    Math.Clamp(surface, session.MinWorldY, session.MaxWorldY),
                    Math.Clamp(surface + 1, session.MinWorldY, session.MaxWorldY),
                    session.MaxWorldY
                };

                foreach (var y in sampleYs.Distinct())
                {
                    var position = new Int3(x, y, z);
                    var expected = session.InspectMaterialAtCurrentPlanet(position);
                    var sampled = volume.GetOrAir(position);

                    Assert.Equal(expected.Id, sampled.MaterialId);
                    Assert.Equal(expected.IsSolid, sampled.IsSolid);
                }
            }
        }

        Assert.True(volume.GetOrAir(new Int3((key.X * chunkSize) - 2, session.MinWorldY, key.Z * chunkSize)).IsAir);
    }

    [Fact]
    public void ChunkSliceVolume_ReportsSliceBatchProgressForLoadingUi()
    {
        var content = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "Content"));
        var session = GameSession.CreateNew(content, _workspaceRoot, "chunk-slice-progress", 5152);
        session.TravelToPlanet(0);

        var key = new ChunkColumnKey(1, -1);
        var progressEvents = new List<ChunkSliceBatchProgress>();

        _ = ChunkSliceVolume.SampleCurrentPlanet(session, key, progressEvents.Add);

        Assert.NotEmpty(progressEvents);
        Assert.Equal(ChunkSliceVolume.GetSliceBatchCount(session), progressEvents.Count);
        Assert.All(progressEvents, progress => Assert.Equal(key, progress.Key));
        Assert.Equal(1, progressEvents[0].CompletedBatches);
        Assert.Equal(progressEvents.Count, progressEvents[^1].CompletedBatches);
        Assert.All(progressEvents, progress => Assert.Equal(progressEvents.Count, progress.TotalBatches));
    }

    [Fact]
    public void PlayerFaces_RenderAtHumanScaleInNineBlocksPerMeterWorld()
    {
        var selection = new CharacterSelection("female", "parted");
        var faces = GeneratedVoxelAssets.BuildPlayerFaces(selection, Vector3.Zero, new Int3(0, 0, -1)).ToArray();
        var vertices = faces
            .SelectMany(face => new[] { face.A, face.B, face.C, face.D })
            .ToArray();

        var minY = vertices.Min(vertex => vertex.Y);
        var maxY = vertices.Max(vertex => vertex.Y);
        var height = maxY - minY;

        Assert.NotEmpty(faces);
        Assert.Equal(1.8f, height, precision: 2);
    }

    [Fact]
    public void ImportedActorFaces_ApplyCombatStanceOffsetsFromRenderProfile()
    {
        var profile = ActorRenderProfileCatalog.GetPlayerProfile(new CharacterSelection("female", "parted"));
        var asset = ImportedVoxelAssets.GetActor(profile);

        Assert.NotNull(asset);

        var idleFaces = GeneratedVoxelAssets.BuildImportedActorFaces(
            asset!,
            "player-profile-test",
            profile,
            Vector3.Zero,
            new Int3(0, 0, -1),
            useCombatStance: false).ToArray();
        var combatFaces = GeneratedVoxelAssets.BuildImportedActorFaces(
            asset,
            "player-profile-test",
            profile,
            Vector3.Zero,
            new Int3(0, 0, -1),
            useCombatStance: true).ToArray();

        var idleCenter = idleFaces.SelectMany(face => new[] { face.A, face.B, face.C, face.D }).Aggregate(Vector3.Zero, static (sum, vertex) => sum + vertex) / (idleFaces.Length * 4f);
        var combatCenter = combatFaces.SelectMany(face => new[] { face.A, face.B, face.C, face.D }).Aggregate(Vector3.Zero, static (sum, vertex) => sum + vertex) / (combatFaces.Length * 4f);
        var delta = combatCenter - idleCenter;
        var expectedOffset = profile.GetStanceOffset(useCombatStance: true) - profile.GetStanceOffset(useCombatStance: false);

        Assert.Equal(expectedOffset.X, delta.X, precision: 2);
        Assert.Equal(expectedOffset.Y, delta.Y, precision: 2);
        Assert.Equal(expectedOffset.Z, delta.Z, precision: 2);
    }

    [Fact]
    public void ActorScaleCatalog_ExposesExplicitProfilesForPlayerNpcEnemyAndBoss()
    {
        var content = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "Content"));

        var playerScale = ActorScaleCatalog.GetPlayerScale();
        var npcScale = ActorScaleCatalog.GetNpcScale("merchant");
        var enemyScale = ActorScaleCatalog.GetEnemyScale(content.GetEnemy("shardling"));
        var bossScale = ActorScaleCatalog.GetBossScale(content.GetBoss("void-wyrm"));

        Assert.Equal(1.8f, playerScale.HeightMeters, precision: 2);
        Assert.Equal(1.75f, npcScale.HeightMeters, precision: 2);
        Assert.Equal(2.35f, enemyScale.HeightMeters, precision: 2);
        Assert.Equal(4.8f, bossScale.HeightMeters, precision: 2);
        Assert.True(enemyScale.WidthMeters > 0f && enemyScale.DepthMeters > 0f);
        Assert.True(bossScale.WidthMeters > 0f && bossScale.DepthMeters > 0f);
    }

    [Fact]
    public void ActorRenderProfileCatalog_PairsScalePathsAndOffsetsForPlayerNpcEnemyAndBoss()
    {
        var content = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "Content"));

        var playerProfile = ActorRenderProfileCatalog.GetPlayerProfile(new CharacterSelection("female", "parted"));
        var npcProfile = ActorRenderProfileCatalog.GetNpcProfile("merchant");
        var enemyProfile = ActorRenderProfileCatalog.GetEnemyProfile(content.GetEnemy("shardling"));
        var bossProfile = ActorRenderProfileCatalog.GetBossProfile(content.GetBoss("void-wyrm"));

        Assert.Equal(1.8f, playerProfile.Scale.HeightMeters, precision: 2);
        Assert.Equal(1.75f, npcProfile.Scale.HeightMeters, precision: 2);
        Assert.Equal(2.35f, enemyProfile.Scale.HeightMeters, precision: 2);
        Assert.Equal(4.8f, bossProfile.Scale.HeightMeters, precision: 2);
        Assert.EndsWith(Path.Combine("Content", "Voxel", "Characters", "female-parted.vox"), playerProfile.RelativeAssetPath!, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("Content", "Voxel", "Npcs", "merchant.vox"), npcProfile.RelativeAssetPath!, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("Content", "Voxel", "Enemies", "shardling.vox"), enemyProfile.RelativeAssetPath!, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("Content", "Voxel", "Bosses", "void-wyrm.vox"), bossProfile.RelativeAssetPath!, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(playerProfile.GetStanceOffset(useCombatStance: false), playerProfile.GetStanceOffset(useCombatStance: true));
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspaceRoot))
        {
            Directory.Delete(_workspaceRoot, recursive: true);
        }
    }
}
