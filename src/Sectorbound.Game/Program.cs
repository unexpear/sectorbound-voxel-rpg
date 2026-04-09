using Sectorbound.Engine;

namespace Sectorbound.Game;

internal static class Program
{
    private const string SaveSlotId = "sectorbound";

    private static void Main(string[] args)
    {
        var contentRoot = Path.Combine(AppContext.BaseDirectory, "Content");
        var saveRoot = SectorboundUserData.GetSaveRoot();
        var content = ContentLoader.LoadFromDirectory(contentRoot);

        var loadExisting = !args.Contains("--new", StringComparer.OrdinalIgnoreCase)
            && File.Exists(GameSession.GetSavePath(saveRoot, SaveSlotId));

        var session = loadExisting
            ? GameSession.Load(content, saveRoot, SaveSlotId)
            : GameSession.CreateNew(
                content,
                saveRoot,
                SaveSlotId,
                worldSeed: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                difficultyPresetId: "frontier");

        if (args.Contains("--demo", StringComparer.OrdinalIgnoreCase))
        {
            RunVerticalSliceDemo(session);
            session.Save();
            return;
        }

        PrintBanner();
        Console.WriteLine(loadExisting
            ? $"Loaded save slot '{SaveSlotId}'."
            : $"Created a new save slot '{SaveSlotId}'.");
        Console.WriteLine(session.DescribeStatus());
        Console.WriteLine();
        Console.WriteLine("Type 'help' to see commands.");

        while (true)
        {
            Console.WriteLine();
            Console.Write("> ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var command = parts[0].ToLowerInvariant();

            try
            {
                switch (command)
                {
                    case "help":
                        PrintHelp();
                        break;
                    case "status":
                        Console.WriteLine(session.DescribeStatus());
                        break;
                    case "planets":
                        foreach (var planet in session.GetPlanetSummaries())
                        {
                            Console.WriteLine(
                                $"{planet.PlanetIndex}: {planet.Name} | {planet.BiomeName} | enemies: {string.Join(", ", planet.SpawnTable)}");
                        }
                        break;
                    case "travel":
                        RequireArgCount(parts, 2, "travel <0|1|2>");
                        session.TravelToPlanet(int.Parse(parts[1]));
                        Console.WriteLine($"Travelled to planet {session.CurrentPlanetIndex}: {session.CurrentPlanet.DisplayName}");
                        break;
                    case "surface":
                        RequireArgCount(parts, 3, "surface <x> <z>");
                        {
                            var x = int.Parse(parts[1]);
                            var z = int.Parse(parts[2]);
                            var surface = session.GetSurfaceVoxel(x, z);
                            var material = session.InspectMaterialAtCurrentPlanet(surface);
                            Console.WriteLine($"Surface at ({x}, {z}) is {surface} -> {material.DisplayName}");
                        }
                        break;
                    case "mine":
                        RequireArgCount(parts, 4, "mine <x> <y> <z>");
                        PrintResult(session.Mine(new Int3(int.Parse(parts[1]), int.Parse(parts[2]), int.Parse(parts[3]))));
                        break;
                    case "mine-surface":
                        RequireArgCount(parts, 3, "mine-surface <x> <z>");
                        {
                            var x = int.Parse(parts[1]);
                            var z = int.Parse(parts[2]);
                            var surface = session.GetSurfaceVoxel(x, z);
                            PrintResult(session.Mine(surface));
                        }
                        break;
                    case "place":
                        RequireArgCount(parts, 5, "place <x> <y> <z> <materialId>");
                        PrintResult(session.PlaceBlock(
                            new Int3(int.Parse(parts[1]), int.Parse(parts[2]), int.Parse(parts[3])),
                            parts[4]));
                        break;
                    case "craft":
                        RequireArgCount(parts, 2, "craft <recipeId>");
                        PrintResult(session.Craft(parts[1]));
                        break;
                    case "fight":
                        {
                            var enemyId = parts.Length >= 2 ? parts[1] : session.CurrentPlanet.SpawnTables.First();
                            var approach = parts.Length >= 3 ? ParseApproach(parts[2]) : CombatApproach.Blade;
                            PrintResult(session.FightEnemy(enemyId, approach));
                        }
                        break;
                    case "boss":
                        {
                            var approach = parts.Length >= 2 ? ParseApproach(parts[1]) : CombatApproach.Sigil;
                            PrintResult(session.FightSectorBoss(approach));
                        }
                        break;
                    case "inventory":
                        Console.WriteLine(session.DescribeInventory());
                        break;
                    case "skills":
                        Console.WriteLine(session.DescribeSkills());
                        break;
                    case "difficulty":
                        RequireArgCount(parts, 2, "difficulty <wayfarer|frontier|hardcore>");
                        session.ChangeDifficulty(parts[1]);
                        Console.WriteLine($"Difficulty set to {session.CurrentDifficulty.DisplayName}.");
                        break;
                    case "death":
                        {
                            var penalty = session.TriggerDeath();
                            Console.WriteLine(
                                $"Death penalty applied. Lost: {FormatLoot(penalty.LostItems)} | Skill XP lost: {penalty.TotalSkillExperienceLost} | Corpse run: {penalty.CorpseRunEnabled}");
                        }
                        break;
                    case "next-sector":
                        {
                            var transition = session.AdvanceToNextSector();
                            Console.WriteLine($"{transition.Message} Current sector: {transition.ToSectorIndex}.");
                        }
                        break;
                    case "save":
                        session.Save();
                        Console.WriteLine($"Saved to {GameSession.GetSavePath(saveRoot, SaveSlotId)}");
                        break;
                    case "load":
                        session = GameSession.Load(content, saveRoot, SaveSlotId);
                        Console.WriteLine("Reloaded the current save.");
                        Console.WriteLine(session.DescribeStatus());
                        break;
                    case "content":
                        Console.WriteLine($"Recipes: {string.Join(", ", content.Recipes.Keys.OrderBy(x => x))}");
                        Console.WriteLine($"Enemies: {string.Join(", ", content.Enemies.Keys.OrderBy(x => x))}");
                        Console.WriteLine($"Difficulties: {string.Join(", ", content.Difficulties.Keys.OrderBy(x => x))}");
                        break;
                    case "demo":
                        RunVerticalSliceDemo(session);
                        break;
                    case "quit":
                    case "exit":
                        return;
                    default:
                        Console.WriteLine("Unknown command. Type 'help' for the available commands.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }

    private static void RunVerticalSliceDemo(GameSession session)
    {
        Console.WriteLine("Sectorbound vertical slice demo");
        Console.WriteLine("------------------------------");

        session.TravelToPlanet(0);
        Console.WriteLine($"Landing on {session.CurrentPlanet.DisplayName}.");

        MineForShelterStone(session, 0, 0);
        MineForShelterStone(session, 1, 0);

        PrintResult(session.Craft("workbench-block"));

        var buildY = session.GetSurfaceHeight(0, 1) + 1;
        PrintResult(session.PlaceBlock(new Int3(0, buildY, 1), "workbench-block"));
        PrintResult(session.Craft("shelter-block"));
        PrintResult(session.PlaceBlock(new Int3(1, buildY, 1), "shelter-block"));
        PrintResult(session.PlaceBlock(new Int3(2, buildY, 1), "shelter-block"));
        PrintResult(session.PlaceBlock(new Int3(1, buildY + 1, 1), "shelter-block"));

        PrintResult(session.FightEnemy(session.CurrentPlanet.SpawnTables.First(), CombatApproach.Blade));
        session.TravelToPlanet(1);
        Console.WriteLine($"Jumping to {session.CurrentPlanet.DisplayName}.");
        PrintResult(session.FightEnemy(session.CurrentPlanet.SpawnTables.First(), CombatApproach.Bow));
        session.TravelToPlanet(2);
        Console.WriteLine($"Jumping to {session.CurrentPlanet.DisplayName}.");
        PrintResult(session.FightEnemy(session.CurrentPlanet.SpawnTables.First(), CombatApproach.Sigil));
        PrintResult(session.FightSectorBoss(CombatApproach.Sigil));

        session.Save();
        Console.WriteLine("Saved progress.");
        var transition = session.AdvanceToNextSector();
        Console.WriteLine(transition.Message);
        Console.WriteLine(session.DescribeStatus());
    }

    private static void MineForShelterStone(GameSession session, int x, int z)
    {
        var surface = session.GetSurfaceVoxel(x, z);
        PrintResult(session.Mine(surface));
        PrintResult(session.Mine(new Int3(x, surface.Y - 1, z)));
        PrintResult(session.Mine(new Int3(x, surface.Y - 2, z)));
    }

    private static void RequireArgCount(string[] parts, int minimumCount, string usage)
    {
        if (parts.Length < minimumCount)
        {
            throw new InvalidOperationException($"Usage: {usage}");
        }
    }

    private static CombatApproach ParseApproach(string value) =>
        value.ToLowerInvariant() switch
        {
            "blade" or "melee" => CombatApproach.Blade,
            "bow" or "ranged" => CombatApproach.Bow,
            "sigil" or "magic" => CombatApproach.Sigil,
            _ => throw new InvalidOperationException("Approach must be blade, bow, or sigil.")
        };

    private static void PrintBanner()
    {
        Console.WriteLine("Sectorbound Voxel RPG Prototype");
        Console.WriteLine("--------------------------------");
    }

    private static void PrintHelp()
    {
        Console.WriteLine("status");
        Console.WriteLine("planets");
        Console.WriteLine("travel <0|1|2>");
        Console.WriteLine("surface <x> <z>");
        Console.WriteLine("mine <x> <y> <z>");
        Console.WriteLine("mine-surface <x> <z>");
        Console.WriteLine("place <x> <y> <z> <materialId>");
        Console.WriteLine("craft <recipeId>");
        Console.WriteLine("fight [enemyId] [blade|bow|sigil]");
        Console.WriteLine("boss [blade|bow|sigil]");
        Console.WriteLine("inventory");
        Console.WriteLine("skills");
        Console.WriteLine("difficulty <wayfarer|frontier|hardcore>");
        Console.WriteLine("death");
        Console.WriteLine("next-sector");
        Console.WriteLine("save");
        Console.WriteLine("load");
        Console.WriteLine("content");
        Console.WriteLine("demo");
        Console.WriteLine("quit");
    }

    private static void PrintResult(BlockActionResult result) => Console.WriteLine(result.Message);

    private static void PrintResult(CraftResult result) => Console.WriteLine(result.Message);

    private static void PrintResult(CombatResult result) =>
        Console.WriteLine($"{result.Message} Loot: {FormatLoot(result.Loot)}");

    private static string FormatLoot(IReadOnlyDictionary<string, int> loot) =>
        loot.Count == 0
            ? "none"
            : string.Join(", ", loot.OrderByDescending(x => x.Value).ThenBy(x => x.Key).Select(x => $"{x.Key} x{x.Value}"));
}
