namespace Sectorbound.Engine;

public sealed partial class GameSession
{
    public int MinWorldY => WorldFloor;
    public int MaxWorldY => WorldCeiling;
    public int ChunkSizeInVoxels => ChunkSize;
    public float ChunkSizeInOriginalBlocks => ChunkSize / (float)GameplayVoxelsPerOriginalBlock;
    public int VoxelsPerOriginalBlock => GameplayVoxelsPerOriginalBlock;
    public float VoxelRenderScale => 1.0f / GameplayVoxelsPerOriginalBlock;

    public IReadOnlyList<PlanetSummary> GetPlanetSummaries() =>
        Sector.LoadedPlanets
            .Select(x => new PlanetSummary(
                x.PlanetIndex,
                x.DisplayName,
                x.BiomeRules.DisplayName,
                x.SpawnTables,
                x.StructureGenerators,
                x.BiomeFeatureGenerators))
            .ToArray();

    public string GetMaterialIdAtCurrentPlanet(Int3 worldPosition) => ResolveMaterial(CurrentPlanet, worldPosition);

    public MaterialDef InspectMaterialAtCurrentPlanet(Int3 worldPosition) => _content.GetMaterial(GetMaterialIdAtCurrentPlanet(worldPosition));

    public bool IsSolidAtCurrentPlanet(Int3 worldPosition) => InspectMaterialAtCurrentPlanet(worldPosition).IsSolid;

    public ChunkCoordinate GetChunkCoordinateForWorldPosition(Int3 worldPosition) => ToChunkCoordinate(worldPosition);

    public int GetSurfaceHeight(int x, int z) => CalculateSurfaceHeight(CurrentPlanet, x, z);

    public BiomeFeatureSite? GetBiomeFeatureSiteAtCurrentPlanet(int x, int z) =>
        ResolveBiomeFeaturePlacement(CurrentPlanet, x, z);

    public string? GetBiomeFeatureIdAtCurrentPlanet(int x, int z) => ResolveBiomeFeaturePlacement(CurrentPlanet, x, z)?.FeatureId;

    public IReadOnlyList<ScenicPropInstance> GetScenicPropsForCurrentPlanet(int chunkX, int chunkZ) =>
        ResolveScenicPropsForChunk(CurrentPlanet, chunkX, chunkZ);

    public IReadOnlyList<WorldGenSlice> GenerateCurrentPlanetSlices(
        Int3 origin,
        int sliceCount,
        int sliceHeightVoxels,
        int sliceDepthVoxels) =>
        GenerateSlicesAlongX(CurrentPlanet, origin, sliceCount, sliceHeightVoxels, sliceDepthVoxels);

    public Int3 GetSurfaceVoxel(int x, int z)
    {
        var surfaceHeight = GetSurfaceHeight(x, z);
        return new Int3(x, surfaceHeight, z);
    }

    public int GetInventoryCount(string itemId) => Player.Inventory.TryGetValue(itemId, out var count) ? count : 0;

    public string DescribePerks() =>
        Player.PerkUnlocks.Count == 0
            ? "none"
            : string.Join(", ", Player.PerkUnlocks.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

    public bool CurrentPlanetHasPlacedBlock(string materialId) => PlanetHasPlacedBlock(CurrentPlanet, materialId);

    public SkillProgress GetSkillProgress(string skillId)
    {
        if (!Player.SkillLevels.TryGetValue(skillId, out var progress))
        {
            progress = new SkillProgress();
            Player.SkillLevels[skillId] = progress;
        }

        return progress;
    }

    public void TravelToPlanet(int planetIndex)
    {
        if (planetIndex < 0 || planetIndex >= Sector.LoadedPlanets.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(planetIndex), "Planets are addressed from 0 to 2.");
        }

        CurrentPlanetIndex = planetIndex;
    }

    public BlockActionResult Mine(Int3 worldPosition)
    {
        var materialId = ResolveMaterial(CurrentPlanet, worldPosition);
        var material = _content.GetMaterial(materialId);
        if (!material.IsSolid)
        {
            return new BlockActionResult(false, $"Nothing solid to mine at {worldPosition}.", materialId, string.Empty, 0);
        }

        var chunk = GetOrCreateChunk(CurrentPlanet, worldPosition);
        var localPosition = ToLocal(worldPosition);

        chunk.PlacedBlocks.Remove(localPosition);
        chunk.DeformationDeltas[localPosition] = AirMaterialId;
        chunk.VoxelMaterials[localPosition] = AirMaterialId;
        chunk.IsDirty = true;

        if (!string.IsNullOrWhiteSpace(material.DropItemId) && material.DropCount > 0)
        {
            AddInventory(material.DropItemId, material.DropCount);
        }

        Player.MinedMaterialIds.Add(material.Id);
        AwardSkillExperience("mining", 18 + (material.Hardness * 8));

        return new BlockActionResult(
            true,
            $"Mined {material.DisplayName} at {worldPosition}.",
            materialId,
            material.DropItemId,
            material.DropCount);
    }

    public BlockActionResult PlaceBlock(Int3 worldPosition, string materialId)
    {
        var material = _content.GetMaterial(materialId);
        if (!material.IsSolid || string.Equals(material.Id, AirMaterialId, StringComparison.OrdinalIgnoreCase))
        {
            return new BlockActionResult(false, $"'{materialId}' is not a placeable block.", material.Id, string.Empty, 0);
        }

        if (!RemoveInventory(materialId, 1))
        {
            return new BlockActionResult(false, $"You do not have any '{materialId}' in your inventory.", material.Id, string.Empty, 0);
        }

        var chunk = GetOrCreateChunk(CurrentPlanet, worldPosition);
        var localPosition = ToLocal(worldPosition);
        chunk.PlacedBlocks[localPosition] = material.Id;
        chunk.DeformationDeltas.Remove(localPosition);
        chunk.VoxelMaterials[localPosition] = material.Id;
        chunk.IsDirty = true;

        Player.PlacedMaterialCounts[material.Id] = GetPlacedMaterialCount(material.Id) + 1;
        AwardSkillExperience("crafting", 8);
        return new BlockActionResult(true, $"Placed {material.DisplayName} at {worldPosition}.", material.Id, string.Empty, 0);
    }

    public CraftResult Craft(string recipeId)
    {
        var recipe = _content.GetRecipe(recipeId);
        if (!string.IsNullOrWhiteSpace(recipe.RequiredStationMaterialId)
            && !PlanetHasPlacedBlock(CurrentPlanet, recipe.RequiredStationMaterialId))
        {
            var stationMaterial = _content.GetMaterial(recipe.RequiredStationMaterialId);
            return new CraftResult(
                false,
                $"Place a {stationMaterial.DisplayName} on {CurrentPlanet.DisplayName} before crafting {recipe.DisplayName}.");
        }

        foreach (var requirement in recipe.Inputs)
        {
            if (GetInventoryCount(requirement.Key) < requirement.Value)
            {
                return new CraftResult(false, $"Missing {requirement.Value}x {requirement.Key} to craft {recipe.DisplayName}.");
            }
        }

        foreach (var requirement in recipe.Inputs)
        {
            RemoveInventory(requirement.Key, requirement.Value);
        }

        foreach (var output in recipe.Outputs)
        {
            AddInventory(output.Key, output.Value);
        }

        Player.CraftedRecipes.Add(recipe.Id);
        AwardSkillExperience(recipe.SkillId, recipe.SkillGain);
        return new CraftResult(true, $"Crafted {recipe.DisplayName}.");
    }

    public CombatResult FightEnemy(string enemyId, CombatApproach approach = CombatApproach.Blade)
    {
        var enemy = _content.GetEnemy(enemyId);
        var success = ResolveCombatSuccess(enemy.Danger, approach, isBoss: false);
        if (!success)
        {
            var deathPenalty = TriggerDeath();
            return new CombatResult(
                false,
                $"The {enemy.DisplayName} overwhelmed you. Lost {deathPenalty.LostItems.Values.Sum()} carried items.",
                enemy.Id,
                new Dictionary<string, int>());
        }

        AwardSkillExperience("combat", enemy.SkillGain);
        if (approach == CombatApproach.Sigil)
        {
            AwardSkillExperience("arcana", Math.Max(6, enemy.SkillGain / 2));
        }

        foreach (var lootDrop in enemy.Loot)
        {
            AddInventory(lootDrop.Key, lootDrop.Value);
        }

        Player.DefeatedEnemyFamilies.Add(enemy.Family);
        return new CombatResult(true, $"Defeated {enemy.DisplayName}.", enemy.Id, new Dictionary<string, int>(enemy.Loot));
    }

    public CombatResult FightSectorBoss(CombatApproach approach = CombatApproach.Sigil)
    {
        var boss = _content.GetBoss(Sector.SectorBossId);
        var success = ResolveCombatSuccess(boss.Danger, approach, isBoss: true);
        if (!success)
        {
            var deathPenalty = TriggerDeath();
            return new CombatResult(
                false,
                $"The {boss.DisplayName} drove you back. Lost {deathPenalty.LostItems.Values.Sum()} carried items.",
                boss.Id,
                new Dictionary<string, int>());
        }

        AwardSkillExperience("combat", 50);
        if (approach == CombatApproach.Sigil)
        {
            AwardSkillExperience("arcana", 28);
        }

        foreach (var reward in boss.Rewards)
        {
            AddInventory(reward.Key, reward.Value);
        }

        if (Player.BossBonuses.Add(boss.Id))
        {
            foreach (var bonus in boss.BonusXpBySkill)
            {
                AwardSkillExperience(bonus.Key, bonus.Value);
            }

            Player.PerkUnlocks.Add(boss.BonusPerkId);
        }

        return new CombatResult(true, $"Defeated sector boss {boss.DisplayName}.", boss.Id, new Dictionary<string, int>(boss.Rewards));
    }

    public DeathPenaltyResult TriggerDeath()
    {
        var difficulty = CurrentDifficulty;
        var lostItems = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var totalSkillExperienceLost = 0;

        foreach (var entry in Player.Inventory.OrderByDescending(x => x.Value).ToArray())
        {
            var lostAmount = CalculatePenaltyAmount(entry.Value, difficulty.InventoryDropPercent);
            if (lostAmount <= 0)
            {
                continue;
            }

            RemoveInventory(entry.Key, lostAmount);
            lostItems[entry.Key] = lostAmount;
        }

        foreach (var skill in Player.SkillLevels.Values)
        {
            var loss = CalculatePenaltyAmount(skill.Experience, difficulty.SkillPenaltyPercent);
            skill.Experience = Math.Max(0, skill.Experience - loss);
            totalSkillExperienceLost += loss;
        }

        return new DeathPenaltyResult(lostItems, totalSkillExperienceLost, difficulty.CorpseRun);
    }

    public void ChangeDifficulty(string difficultyId)
    {
        _ = _content.GetDifficulty(difficultyId);
        Player.DifficultyPresetId = difficultyId;
    }

    public SectorTransitionResult AdvanceToNextSector()
    {
        var fromSector = CurrentSectorIndex;
        PersistCurrentSector();

        Sector.TransitionState = new TransitionState(
            TransitionPhase.Generating,
            fromSector,
            fromSector + 1,
            "Warping into a newly generated sector...",
            DateTimeOffset.UtcNow);

        CurrentSectorIndex += 1;
        Sector = LoadOrGenerateSector(CurrentSectorIndex);
        CurrentPlanetIndex = 0;
        Player.SectorTransitionsCompleted += 1;
        Sector.TransitionState = new TransitionState(
            TransitionPhase.Activating,
            fromSector,
            CurrentSectorIndex,
            $"Sector {CurrentSectorIndex} ready. Three planets loaded.",
            DateTimeOffset.UtcNow);

        return new SectorTransitionResult(fromSector, CurrentSectorIndex, Sector.TransitionState.Message);
    }

    public IReadOnlyList<SliceGoalProgress> GetVerticalSliceProgress()
    {
        var shelterPlaced = GetPlacedMaterialCount("shelter-block");
        var workbenchPlaced = GetPlacedMaterialCount("workbench-block");
        var enemyFamilies = Player.DefeatedEnemyFamilies.Count;
        var bossDefeated = Player.BossBonuses.Count > 0;

        return
        [
            new SliceGoalProgress(
                "mine-anything",
                "Mine resources",
                GetSkillProgress("mining").LifetimeExperience > 0,
                GetSkillProgress("mining").LifetimeExperience > 0
                    ? $"{Player.MinedMaterialIds.Count} material types recovered."
                    : "Break terrain to start the progression loop."),
            new SliceGoalProgress(
                "craft-shelter",
                "Craft shelter blocks",
                Player.CraftedRecipes.Contains("shelter-block"),
                Player.CraftedRecipes.Contains("shelter-block")
                    ? "Shelter recipe crafted."
                    : "Place a workbench, then craft shelter blocks."),
            new SliceGoalProgress(
                "build-shelter",
                "Build a shelter",
                shelterPlaced >= 4,
                $"{shelterPlaced}/4 shelter blocks placed."),
            new SliceGoalProgress(
                "place-workbench",
                "Place a workbench",
                workbenchPlaced >= 1,
                workbenchPlaced >= 1
                    ? "Workbench placed on a planet."
                    : "Craft and place a workbench-block."),
            new SliceGoalProgress(
                "enemy-families",
                "Defeat three enemy families",
                enemyFamilies >= 3,
                $"{enemyFamilies}/3 families defeated."),
            new SliceGoalProgress(
                "boss-kill",
                "Defeat the sector boss",
                bossDefeated,
                bossDefeated
                    ? $"Boss bonuses: {string.Join(", ", Player.BossBonuses.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))}"
                    : "Use combat progression to challenge the sector boss."),
            new SliceGoalProgress(
                "next-sector",
                "Travel to the next sector",
                Player.SectorTransitionsCompleted > 0 || CurrentSectorIndex > 0,
                $"Sectors advanced: {Math.Max(Player.SectorTransitionsCompleted, CurrentSectorIndex)}")
        ];
    }

    private int GetPlacedMaterialCount(string materialId) =>
        Player.PlacedMaterialCounts.TryGetValue(materialId, out var count) ? count : 0;

    private static bool PlanetHasPlacedBlock(PlanetRuntime planet, string materialId) =>
        planet.ChunkRegistry.Values.Any(chunk =>
            chunk.PlacedBlocks.Values.Any(placed =>
                string.Equals(placed, materialId, StringComparison.OrdinalIgnoreCase)));
}
