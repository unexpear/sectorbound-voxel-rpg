from __future__ import annotations

import argparse
import struct
from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image
import trimesh

DEFAULT_FACE_COLOR = np.array([200, 200, 200, 255], dtype=np.uint8)
SUPPORTED_SOURCE_SUFFIXES = {".gltf", ".glb"}


@dataclass
class VoxModel:
    size: tuple[int, int, int]
    voxels: np.ndarray  # columns: x, y, z, color_index
    palette: list[tuple[int, int, int, int]]


def iter_source_models(root: Path) -> list[Path]:
    return sorted(
        path
        for path in root.rglob("*")
        if path.is_file() and path.suffix.lower() in SUPPORTED_SOURCE_SUFFIXES
    )


def normalize_color_rows(colors: object, target_count: int) -> np.ndarray | None:
    if colors is None:
        return None

    array = np.asarray(colors)
    if array.size == 0:
        return None

    if array.ndim == 1:
        array = array.reshape(1, -1)

    if array.shape[-1] < 3:
        return None

    if array.shape[-1] == 3:
        alpha = np.full((len(array), 1), 255, dtype=array.dtype)
        array = np.concatenate([array, alpha], axis=1)
    else:
        array = array[:, :4]

    array = np.clip(array, 0, 255).astype(np.uint8, copy=False)
    if len(array) == 1 and target_count > 1:
        array = np.repeat(array, target_count, axis=0)

    if len(array) != target_count:
        return None

    return array


def resolve_face_colors(mesh: trimesh.Trimesh) -> np.ndarray:
    try:
        color_visual = mesh.visual.to_color()
    except Exception:
        color_visual = None

    if color_visual is not None:
        data = getattr(color_visual, "_data", None)
        if data is not None:
            raw_face_colors = data["face_colors"] if "face_colors" in data else None
            face_colors = normalize_color_rows(raw_face_colors, len(mesh.faces))
            if face_colors is not None:
                return face_colors

            raw_vertex_colors = data["vertex_colors"] if "vertex_colors" in data else None
            vertex_colors = normalize_color_rows(raw_vertex_colors, len(mesh.vertices))
            if vertex_colors is not None:
                return vertex_colors[mesh.faces].mean(axis=1).astype(np.uint8)

    material = getattr(mesh.visual, "material", None)
    if material is not None:
        for attribute in ("main_color", "baseColorFactor", "diffuse"):
            value = getattr(material, attribute, None)
            color = normalize_color_rows(value, 1)
            if color is not None:
                return np.repeat(color, len(mesh.faces), axis=0)

    return np.tile(DEFAULT_FACE_COLOR, (len(mesh.faces), 1))


def flatten_scene(scene: trimesh.Scene) -> list[trimesh.Trimesh]:
    meshes: list[trimesh.Trimesh] = []
    for node_name in scene.graph.nodes_geometry:
        transform, geometry_name = scene.graph[node_name]
        mesh = scene.geometry[geometry_name].copy()
        if mesh.is_empty or len(mesh.faces) == 0:
            continue
        mesh.apply_transform(transform)
        mesh.visual = trimesh.visual.ColorVisuals(mesh=mesh, face_colors=resolve_face_colors(mesh))

        meshes.append(mesh)

    return meshes


def grid_from_bounds(bounds: np.ndarray, resolution: int, padding: int) -> tuple[np.ndarray, float]:
    mins = bounds[0]
    maxs = bounds[1]
    extents = maxs - mins
    max_extent = float(extents.max())
    usable_resolution = max(1, resolution - (padding * 2))
    pitch = max_extent / usable_resolution if max_extent > 0 else 1.0
    origin = mins - (pitch * padding)
    return origin, pitch


def points_to_grid(points: np.ndarray, origin: np.ndarray, pitch: float, resolution: int) -> np.ndarray:
    coords = np.floor((points - origin) / pitch).astype(np.int32)
    return np.clip(coords, 0, resolution - 1)


def collect_surface_voxels(
    meshes: list[trimesh.Trimesh],
    origin: np.ndarray,
    pitch: float,
    resolution: int,
) -> tuple[dict[tuple[int, int, int], np.ndarray], set[tuple[int, int, int]]]:
    color_accumulator: dict[tuple[int, int, int], np.ndarray] = {}
    occupied: set[tuple[int, int, int]] = set()

    for mesh in meshes:
        mesh_voxels = mesh.voxelized(pitch)
        mesh_coords = points_to_grid(mesh_voxels.points, origin, pitch, resolution)
        occupied.update(map(tuple, mesh_coords.tolist()))

        sample_count = max(len(mesh_voxels.points) * 24, len(mesh.faces) * 10, 50_000)
        sample_count = min(sample_count, 400_000)

        samples, face_index = mesh.sample(sample_count, return_index=True)
        sample_coords = points_to_grid(samples, origin, pitch, resolution)
        sample_colors = mesh.visual.face_colors[face_index][:, :4].astype(np.float64)

        for coord, color in zip(sample_coords.tolist(), sample_colors.tolist()):
            key = tuple(coord)
            if key not in color_accumulator:
                color_accumulator[key] = np.array([*color[:3], 1.0], dtype=np.float64)
            else:
                color_accumulator[key][:3] += color[:3]
                color_accumulator[key][3] += 1.0

    return color_accumulator, occupied


def fill_missing_colors(
    occupied: set[tuple[int, int, int]],
    color_accumulator: dict[tuple[int, int, int], np.ndarray],
) -> dict[tuple[int, int, int], np.ndarray]:
    colors = {
        coord: np.clip(acc[:3] / max(acc[3], 1.0), 0, 255).astype(np.uint8)
        for coord, acc in color_accumulator.items()
    }

    if not colors:
        return {}

    unresolved = [coord for coord in occupied if coord not in colors]
    for radius in (1, 2, 3):
        if not unresolved:
            break

        next_unresolved: list[tuple[int, int, int]] = []
        for coord in unresolved:
            neighbors = []
            x, y, z = coord
            for dx in range(-radius, radius + 1):
                for dy in range(-radius, radius + 1):
                    for dz in range(-radius, radius + 1):
                        if dx == dy == dz == 0:
                            continue
                        neighbor = (x + dx, y + dy, z + dz)
                        if neighbor in colors:
                            neighbors.append(colors[neighbor])

            if neighbors:
                colors[coord] = np.mean(np.array(neighbors, dtype=np.float64), axis=0).astype(np.uint8)
            else:
                next_unresolved.append(coord)

        unresolved = next_unresolved

    fallback_color = np.mean(np.array(list(colors.values()), dtype=np.float64), axis=0).astype(np.uint8)
    for coord in unresolved:
        colors[coord] = fallback_color

    return colors


def quantize_colors(colors: np.ndarray, max_colors: int = 255) -> tuple[np.ndarray, list[tuple[int, int, int, int]]]:
    if len(colors) == 0:
        return np.array([], dtype=np.uint8), [(0, 0, 0, 0)] * 255

    image = Image.fromarray(colors.reshape(1, len(colors), 3), mode="RGB")
    quantized = image.quantize(colors=min(max_colors, len(colors)), method=Image.Quantize.FASTOCTREE)
    indices = np.array(quantized, dtype=np.uint8).reshape(-1)

    palette_data = quantized.getpalette()[: max_colors * 3]
    palette: list[tuple[int, int, int, int]] = []
    for i in range(0, len(palette_data), 3):
        r, g, b = palette_data[i : i + 3]
        palette.append((r, g, b, 255))

    while len(palette) < 255:
        palette.append((0, 0, 0, 0))

    return indices, palette[:255]


def build_vox_model(
    colors_by_coord: dict[tuple[int, int, int], np.ndarray],
    resolution: int,
) -> VoxModel:
    if not colors_by_coord:
        return VoxModel(
            size=(resolution, resolution, resolution),
            voxels=np.zeros((0, 4), dtype=np.uint8),
            palette=[(0, 0, 0, 0)] * 255,
        )

    items = sorted(colors_by_coord.items(), key=lambda item: (item[0][2], item[0][1], item[0][0]))
    coords = np.array([coord for coord, _ in items], dtype=np.uint8)
    colors = np.array([color for _, color in items], dtype=np.uint8)

    palette_indices, palette = quantize_colors(colors)
    voxels = np.concatenate([coords, (palette_indices + 1).reshape(-1, 1)], axis=1)
    return VoxModel(size=(resolution, resolution, resolution), voxels=voxels, palette=palette)


def write_chunk(chunk_id: bytes, content: bytes, children: bytes = b"") -> bytes:
    return chunk_id + struct.pack("<II", len(content), len(children)) + content + children


def write_vox(path: Path, model: VoxModel) -> None:
    size_chunk = write_chunk(b"SIZE", struct.pack("<III", *model.size))

    xyzi_payload = struct.pack("<I", len(model.voxels))
    if len(model.voxels):
        xyzi_payload += model.voxels.astype(np.uint8).tobytes()
    xyzi_chunk = write_chunk(b"XYZI", xyzi_payload)

    palette = list(model.palette[:255])
    while len(palette) < 255:
        palette.append((0, 0, 0, 0))

    rgba_payload = b"".join(struct.pack("<BBBB", *rgba) for rgba in palette)
    rgba_payload += struct.pack("<BBBB", 0, 0, 0, 0)
    rgba_chunk = write_chunk(b"RGBA", rgba_payload)

    children = size_chunk + xyzi_chunk + rgba_chunk
    main_chunk = write_chunk(b"MAIN", b"", children)
    payload = b"VOX " + struct.pack("<I", 150) + main_chunk
    path.write_bytes(payload)


def convert_file(input_path: Path, output_path: Path, resolution: int, padding: int) -> tuple[int, int]:
    scene = trimesh.load(input_path, force="scene", process=False)
    meshes = flatten_scene(scene)
    if not meshes:
        raise RuntimeError("No renderable meshes found")

    bounds = np.array(
        [
            np.min(np.vstack([mesh.bounds[0] for mesh in meshes]), axis=0),
            np.max(np.vstack([mesh.bounds[1] for mesh in meshes]), axis=0),
        ],
        dtype=np.float64,
    )
    origin, pitch = grid_from_bounds(bounds, resolution, padding)
    color_accumulator, occupied = collect_surface_voxels(meshes, origin, pitch, resolution)
    colors_by_coord = fill_missing_colors(occupied, color_accumulator)
    model = build_vox_model(colors_by_coord, resolution)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    write_vox(output_path, model)
    return len(meshes), len(model.voxels)


def main() -> None:
    parser = argparse.ArgumentParser(description="Convert textured glTF or GLB files into MagicaVoxel .vox models.")
    parser.add_argument("--input", required=True, type=Path, help="Folder to search for glTF/GLB files.")
    parser.add_argument("--output", required=True, type=Path, help="Folder where .vox files will be written.")
    parser.add_argument("--resolution", type=int, default=64, help="Target voxel resolution per axis.")
    parser.add_argument("--padding", type=int, default=1, help="Empty voxel border around the model.")
    args = parser.parse_args()

    sources = iter_source_models(args.input)
    if not sources:
        raise SystemExit(f"No .gltf or .glb files found under {args.input}")

    print(f"Found {len(sources)} glTF/GLB files")
    converted = 0
    for source in sources:
        relative = source.relative_to(args.input)
        target = (args.output / relative).with_suffix(".vox")
        try:
            mesh_count, voxel_count = convert_file(source, target, args.resolution, args.padding)
            converted += 1
            print(f"[ok] {relative} -> {target.name} | meshes={mesh_count} voxels={voxel_count}")
        except Exception as exc:
            print(f"[fail] {relative}: {exc}")

    print(f"Converted {converted}/{len(sources)} files")


if __name__ == "__main__":
    main()
