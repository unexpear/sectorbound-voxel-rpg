# Sectorbound Voxel RPG Prototype

This workspace contains a C# prototype for the Sectorbound plan: a deterministic sector-based voxel sandbox RPG slice with mining, block placement, crafting, combat, boss bonuses, difficulty presets, sector transitions, save/load persistence, and a windowed runtime client that renders cached voxel chunk meshes through a continuous software render loop.

## Projects

- `src/Sectorbound.Engine`: core runtime, content loading, procedural planet generation, progression, and persistence.
- `src/Sectorbound.Game`: console prototype and external content definitions, including workbench-gated crafting for shelter and advanced recipes.
- `src/Sectorbound.Client`: Windows Forms runtime client with a low-level UI-thread render loop, cached chunk-face meshing, grounded movement, in-world face picking, and world interaction over the same engine and shared save data.
- `tests/Sectorbound.Engine.Tests`: automated verification for saves, progression, sector travel, building freedom, and difficulty penalties.

## Workspace Boundaries

The live project is the `Sectorbound.sln` solution plus `src/`, `tests/`, `scripts/`, `docs/`, and `artifacts/`.

`game makeing/` is currently tracked as reference/archive material, but no project file or build script points at it. Treat it as a manual asset source until something is explicitly imported into the live project.

For a quicker orientation pass, see `docs/workspace-map.md` and `docs/gameplay-loop.md`.

## Run It

```powershell
dotnet run --project .\src\Sectorbound.Game
```

Use `--demo` to play through the scripted vertical-slice loop:

```powershell
dotnet run --project .\src\Sectorbound.Game -- --demo
```

Run the windowed runtime client:

```powershell
dotnet run --project .\src\Sectorbound.Client
```

Both apps share a save directory under `%LOCALAPPDATA%\Sectorbound\Saves`. Legacy `%LOCALAPPDATA%\SectorboundPrototype` data is migrated forward automatically when possible.

## Helpful Commands

- `status`
- `planets`
- `travel 0`
- `surface 0 0`
- `mine-surface 0 0`
- `mine 0 22 0`
- `craft shelter-block`
- `fight`
- `boss sigil`
- `next-sector`
- `save`
- `load`

Shelter Blocks and Rift Sigil Core now require a placed `workbench-block` on the current planet before crafting succeeds.

## Runtime Client Controls

- `WASD` or arrow keys: move the player across the terrain
- `Mouse`: aim visible voxel faces
- `Left click` or `Space`: mine the targeted block face
- `Right click` or `Enter`: place the selected build block against the targeted face
- `Mouse wheel` or `Q / E`: zoom the chase camera
- `I`: toggle invert Y look and persist the preference locally
- `[ / ]`: decrease / increase mouse look speed and persist the preference locally
- `Shift`: sprint
- `1 / 2 / 3`: switch planets
- `Z / X`: cycle the selected build block
- `T`: cycle the selected recipe
- `R`: craft the selected recipe (shelter and advanced recipes need a placed workbench)
- `C`: cycle combat approach (`Blade`, `Bow`, `Sigil`)
- `F`: fight the local enemy with the selected approach
- `B`: fight the sector boss with the selected approach
- `N`: advance to the next sector
- `S`: save
- `L`: reload
- `Tab`: toggle the in-world overlay

The runtime sidebar now also tracks the vertical-slice checklist so you can see progress toward mining, crafting, shelter building, workbench placement, enemy-family clears, boss victory, and sector travel.

## Verify

```powershell
dotnet test .\Sectorbound.sln
```

## Steam Base

The current Steam target is the windowed client in `src/Sectorbound.Client`, and the currently supported store platform is `Windows` only.

Build a Steam-ready Windows package with:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-steam.ps1
```

That script publishes a self-contained `win-x64` build to `artifacts\steam\win-x64`, which avoids depending on a preinstalled .NET runtime on the target machine.

Steam readiness notes and official Valve doc links live in `docs/steam-readiness.md`.
