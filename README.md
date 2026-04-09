# Sectorbound Voxel RPG Prototype

This workspace contains a C# prototype for the Sectorbound plan: a deterministic sector-based voxel sandbox RPG slice with mining, block placement, crafting, combat, boss bonuses, difficulty presets, sector transitions, save/load persistence, and a new windowed debug client.

## Projects

- `src/Sectorbound.Engine`: core runtime, content loading, procedural planet generation, progression, and persistence.
- `src/Sectorbound.Game`: console prototype and external content definitions.
- `src/Sectorbound.Client`: Windows Forms debug viewer for the same engine and shared save data.
- `tests/Sectorbound.Engine.Tests`: automated verification for saves, progression, sector travel, building freedom, and difficulty penalties.

## Run It

```powershell
dotnet run --project .\src\Sectorbound.Game
```

Use `--demo` to play through the scripted vertical-slice loop:

```powershell
dotnet run --project .\src\Sectorbound.Game -- --demo
```

Run the windowed viewer:

```powershell
dotnet run --project .\src\Sectorbound.Client
```

Both apps now share a save directory under `%LOCALAPPDATA%\SectorboundPrototype\Saves`.

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

## Viewer Controls

- `Left click`: mine a block
- `Right click`: place a `shelter-block`
- `1 / 2 / 3`: switch planets
- `Q / E`: move the current Z slice
- `Left / Right`: pan the visible X range
- `R`: craft `shelter-block`
- `T`: craft `workbench-block`
- `F`: fight the local enemy
- `B`: fight the sector boss
- `N`: advance to the next sector
- `S`: save
- `L`: reload

## Verify

```powershell
dotnet test .\Sectorbound.sln
```
