# Workspace Map

Updated: April 10, 2026

## Live Project Lanes

- `Sectorbound.sln` is the single active solution entrypoint.
- `src/Sectorbound.Engine` owns deterministic world generation, session state, save/load, progression, crafting/combat rules, and sector travel.
- `src/Sectorbound.Game` is the console prototype harness and the current home of shared content definitions.
- `src/Sectorbound.Client` is the desktop runtime: WinForms shell, input, camera, render loop, face picking, and mesh caching.
- `tests/Sectorbound.Engine.Tests` is the automated regression layer for the rules and persistence core.
- `scripts/publish-steam.ps1` is the current packaging path for the Windows desktop build.
- `artifacts/steam/win-x64` is the published Windows output lane produced by the Steam packaging script.

## Reference And Archive Material

- `game makeing/` is tracked in git, but it is not referenced by the solution, any current `.csproj`, or the Steam packaging script.
- `docs/asset-shortlist.md` and `docs/generated-voxel-assets.md` are intake/reference notes, not runtime inputs.
- Root-level shortcuts and launcher links are workspace convenience items, not part of the live solution shape.

## Practical Guardrails

- Put new gameplay code, engine systems, and client behavior in `src/`.
- Put rule coverage and persistence regressions in `tests/`.
- Put build and publishing automation in `scripts/`.
- Treat `game makeing/` as a pull-from reference library, not a place the runtime reads from directly.
- When an asset graduates from reference to live content, copy/import it into an explicit project-owned location and wire it in through a project or build step.
