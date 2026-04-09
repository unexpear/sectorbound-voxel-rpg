# Sectorbound Voxel RPG Plan

## Summary

- Build a solo-friendly but ambitious single-player game: a third-person, stylized voxel fantasy sandbox RPG inspired by Terraria and Starbound.
- Core fantasy: travel through procedural sectors, explore three planets per sector, mine and build anywhere, fight through mixed-weapon combat, and grow a classless character through use-based skills plus boss milestone XP.
- Pillars are intentionally equal: exploration, building/crafting, and boss progression should all be first-class reasons to play.
- The project should use a custom C# game engine built on a low-level framework, but the first major milestone is still a playable vertical slice, not an engine-only tech demo.

## Key Changes

### Game Structure

- World structure: each sector contains exactly 3 resident planets loaded as a unit; crossing into a new sector triggers a procedural generation/loading screen and swaps the sector runtime.
- Building: fully deformable voxel terrain plus block-based construction; players can build anywhere equally, with no privileged “main base” location in v1.
- Progression: classless, use-based skill tracks for combat, gathering, and crafting; bosses grant bonus XP/perk progress to create milestone spikes without replacing use-based growth.
- Tone: pure sandbox, no major authored questline in v1; progression is driven by survival, discovery, recipes, skill growth, and boss gates.

### Engine and System Architecture

- Use C# with a low-level framework and treat the engine as a custom stack. Default foundation: window/input/render loop, asset loading, and debug UI from libraries; all world, save, streaming, combat, and progression systems are custom.
- Define these core runtime/data contracts up front:
  - `SectorRuntime`: active sector seed, 3 loaded planets, transition state, streaming budget.
  - `PlanetRuntime`: planet seed, biome rules, chunk registry, spawn tables, structure generators.
  - `ChunkData`: voxel materials, deformation deltas, placed blocks, dirty/persisted state.
  - `PlayerProfile`: inventory, equipped loadout, skill levels, perk unlocks, boss bonuses, difficulty preset.
  - `ContentDefs`: materials, recipes, enemies, bosses, perks, skills, and difficulty settings in external data files.
- Keep generation deterministic from seeds so planets/sectors can unload and reload without losing authored or player-made changes.
- Keep death handling data-driven through 3 difficulty presets rather than hardcoding one penalty model.

### First Playable Vertical Slice

- Ship one sector with 3 playable planets and full round-trip persistence.
- Required loop in the slice:
  - Land on a planet, gather resources, deform terrain, place crafted blocks, and build a functional shelter/work area.
  - Fight at least 3 enemy families using mixed weapon/ability combat.
  - Earn skill progress from use, unlock perks, and receive a boss bonus after defeating 1 sector boss.
  - Save, leave the planet, reload the sector, and confirm world edits/builds/progression persist.
  - Cross into a new sector through the loading transition and arrive in a newly generated 3-planet set.
- Intentionally defer multiplayer, large story systems, advanced settlement AI, and seamless universe travel until after the slice proves the loop.

### Production Phasing

- Phase 1: engine foundation for chunked voxel terrain, deterministic generation, save/load, third-person controller, combat sandbox, and data-driven content.
- Phase 2: vertical slice content for one sector, 3 planet archetypes, progression/perks, crafting stations, boss fight, and difficulty presets.
- Phase 3: expand biome variety, deeper crafting chains, more bosses, more planet rules, and long-tail progression once the slice is genuinely fun.

## Test Plan

- Chunk edits persist after unload/reload and after leaving/re-entering a sector.
- Sector transitions always swap from one 3-planet runtime to another without corrupting saves or duplicating world state.
- Mining, combat, and crafting each advance the intended skill tracks; boss bonuses award once and survive reload.
- Building is allowed on every planet with no hidden “base-only” assumptions.
- Difficulty presets correctly change death penalties without breaking saves or progression.
- Vertical slice acceptance: a player can play 20-30 minutes and successfully experience gather -> build -> fight -> level -> boss -> travel -> persistence.

## Assumptions And Defaults

- Target platform is Windows PC first, keyboard/mouse first, controller support later.
- Visual target is stylized voxel fantasy, not realistic fidelity.
- Single-player only in the initial plan.
- No heavy narrative campaign in v1; lore can be environmental and optional.
- “Custom engine” means custom game/engine architecture on top of low-level libraries, not a fully from-scratch renderer/platform stack.
