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

        Assert.Equal(16, session.ChunkSizeInVoxels);
        Assert.Equal(0, session.MinWorldY);
        Assert.Equal(48, session.MaxWorldY);
        Assert.Equal(new ChunkCoordinate(0, 0, 0), session.GetChunkCoordinateForWorldPosition(new Int3(0, 0, 0)));
        Assert.Equal(new ChunkCoordinate(0, 0, 0), session.GetChunkCoordinateForWorldPosition(new Int3(15, 15, 15)));
        Assert.Equal(new ChunkCoordinate(-1, 0, -1), session.GetChunkCoordinateForWorldPosition(new Int3(-1, 4, -1)));
        Assert.Equal(new ChunkCoordinate(1, 0, 1), session.GetChunkCoordinateForWorldPosition(new Int3(16, 2, 16)));
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
