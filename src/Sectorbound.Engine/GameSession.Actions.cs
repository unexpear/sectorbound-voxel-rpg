namespace Sectorbound.Engine;

public sealed partial class GameSession
{
    public IReadOnlyList<PlanetSummary> GetPlanetSummaries() =>
        Sector.LoadedPlanets
            .Select(x => new PlanetSummary(x.PlanetIndex, x.DisplayName, x.BiomeRules.DisplayName, x.SpawnTables, x.StructureGenerators))
            .ToArray();

    public string GetMaterialIdAtCurrentPlanet(Int3 worldPosition) => ResolveMaterial(CurrentPlanet, worldPosition);

    public MaterialDef InspectMaterialAtCurrentPlanet(Int3 worldPosition) => _content.GetMaterial(GetMaterialIdAtCurrentPlanet(worldPosition));

    public int GetSurfaceHeight(int x, int z) => CalculateSurfaceHeight(CurrentPlanet, x, z);

    public Int3 GetSurfaceVoxel(int x, int z)
    {
        var surfaceHeight = GetSurfaceHeight(x, z);
        return new Int3(x, surfaceHeight, z);
    }

    public int GetInventoryCount(string itemId) => Player.Inventory.TryGetValue(itemId, out var count) ? count : 0;

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

        AwardSkillExperience("crafting", 8);
        return new BlockActionResult(true, $"Placed {material.DisplayName} at {worldPosition}.", material.Id, string.Empty, 0);
    }

    public CraftResult Craft(string recipeId)
    {
        var recipe = _content.GetRecipe(recipeId);
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
        Sector.TransitionState = new TransitionState(
            TransitionPhase.Activating,
            fromSector,
            CurrentSectorIndex,
            $"Sector {CurrentSectorIndex} ready. Three planets loaded.",
            DateTimeOffset.UtcNow);

        return new SectorTransitionResult(fromSector, CurrentSectorIndex, Sector.TransitionState.Message);
    }
}
