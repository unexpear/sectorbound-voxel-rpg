# Generated Voxel Assets

Updated: April 9, 2026

## Style Rule

All generated assets should be made from block-sized cubes or smaller cube aggregates only.

No smooth sculpting, no borrowed meshes, and no sloped wedge pieces.

## Source Scale

- Generated detail cube size: `5 cm`
- Target player height: `2.0 m`
- Player source height: `40 cubes`

This keeps the generated assets in the same detail neighborhood as the blocky animal reference and the chunky readability you called out from Voxile, while keeping the shapes and palettes original.

## Current Generated Assets

- `workbench-block`
  - Built as a cube-only prop from legs, shelf, top, backboard, and tool blocks.
- `player-proxy`
  - Built as a cube-only humanoid from boots, legs, tunic, sleeves, cloak, head, hair, and visor blocks.

## Implementation

The current generated asset source lives in `src/Sectorbound.Client/GeneratedVoxelAssets.cs`.
