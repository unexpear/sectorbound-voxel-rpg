# Sectorbound Voxel RPG Prototype

This workspace contains a C# prototype for the Sectorbound plan: a deterministic sector-based voxel sandbox RPG slice with mining, block placement, crafting, combat, boss bonuses, difficulty presets, sector transitions, and save/load persistence.

## Projects

- `src/Sectorbound.Engine`: core runtime, content loading, procedural planet generation, progression, and persistence.
- `src/Sectorbound.Game`: console prototype and external content definitions.
- `tests/Sectorbound.Engine.Tests`: automated verification for saves, progression, sector travel, building freedom, and difficulty penalties.

## Run It

```powershell
dotnet run --project .\src\Sectorbound.Game
```

Use `--demo` to play through the scripted vertical-slice loop:

```powershell
dotnet run --project .\src\Sectorbound.Game -- --demo
```

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

## Verify

```powershell
dotnet test .\Sectorbound.sln
```
