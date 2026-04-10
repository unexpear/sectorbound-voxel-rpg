# Engine To Client Gameplay Loop

Updated: April 10, 2026

## 1. Boot And Shared Data

- `src/Sectorbound.Client/Program.cs` installs diagnostics and launches `RuntimeForm`.
- `RuntimeForm` loads content from its output `Content` folder, which is populated from `src/Sectorbound.Game/Content/content.json` by a linked item in `Sectorbound.Client.csproj`.
- Shared local data lives under `%LOCALAPPDATA%\\Sectorbound` through `SectorboundUserData`, which currently provides save, log, and client-settings roots.
- The client then loads or creates a `GameSession`, so both desktop runtime and console prototype operate on the same world/session contract.

## 2. Frame Ownership

- `RenderLoop` drives `RuntimeForm.TickFrame` on the UI thread at roughly 60 FPS.
- `TickFrame` splits responsibility into `UpdateSimulation` and `RenderFrame`.
- `UpdateSimulation` reads keyboard state, lets `PlayerController` resolve movement/grounding, and updates `RuntimeCamera` around the player.
- `RenderFrame` queries world data from the session, uses `ChunkMeshCache` and `ChunkMeshBuilder` to rebuild dirty chunk faces, then draws the viewport and overlay.

## 3. Client To Engine Handoff

- The client owns input gathering, targeting, and presentation.
- The engine owns the actual game rules.
- `RuntimeForm` converts player intent into `GameSession` calls such as `Mine`, `PlaceBlock`, `Craft`, `FightEnemy`, `FightSectorBoss`, `TravelToPlanet`, `AdvanceToNextSector`, `Save`, and `Load`.
- After each action, the client either invalidates nearby chunk meshes or fully resets local runtime state so the next frame reflects the authoritative engine state.

## 4. Engine Ownership

- `GameSession.Actions.cs` handles voxel edits, inventory changes, recipe gates, combat resolution, difficulty penalties, and sector transitions.
- `GameSession.Generation.cs` and the saved-sector registry keep sector and planet state deterministic from seeds.
- `GameSession.Persistence.cs` serializes player progress plus changed sector/chunk data for round-trip save/load.
- `tests/Sectorbound.Engine.Tests` validate that rule changes stay correct without depending on the WinForms client.

## 5. Why `Game` Still Matters

- `src/Sectorbound.Game/Program.cs` is a console harness over the same `GameSession` used by the client.
- That makes it a fast regression surface for rules work and a useful place to demo the vertical slice loop without the desktop renderer.
- It also remains the current owner of external content definitions, even though `src/Sectorbound.Client` is the shipping desktop runtime and Steam publish target.

## 6. Rule Of Thumb For Future Changes

- Put world rules, persistence, generation, combat math, and progression in `Sectorbound.Engine`.
- Put desktop-only UX, render, input, and camera behavior in `Sectorbound.Client`.
- Put content authoring and lightweight harness work in `Sectorbound.Game` until a separate content project exists.
