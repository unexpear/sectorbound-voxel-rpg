import argparse
import math
import re
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def parse_args():
    argv = sys.argv
    if "--" in argv:
        argv = argv[argv.index("--") + 1 :]
    else:
        argv = []

    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--name")
    parser.add_argument("--octree-depth", type=int, default=6)
    parser.add_argument("--texture-size", type=int, default=1024)
    parser.add_argument("--scale", type=float, default=0.9)
    return parser.parse_args(argv)


def clean_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in (
        bpy.data.meshes,
        bpy.data.materials,
        bpy.data.images,
        bpy.data.cameras,
        bpy.data.lights,
        bpy.data.armatures,
    ):
        for item in list(block):
            if item.users == 0:
                block.remove(item)


def import_asset(asset_path: Path):
    suffix = asset_path.suffix.lower()
    if suffix == ".gltf" or suffix == ".glb":
        bpy.ops.import_scene.gltf(filepath=str(asset_path))
    elif suffix == ".fbx":
        bpy.ops.import_scene.fbx(filepath=str(asset_path))
    else:
        raise ValueError(f"Unsupported asset type: {suffix}")


def candidate_image_names(name: str):
    names = {name}
    fixed = re.sub(r"_png(?=\.png$)", "", name, flags=re.IGNORECASE)
    names.add(fixed)
    names.add(re.sub(r"\.png\.png$", ".png", name, flags=re.IGNORECASE))
    return [n for n in names if n]


def relink_missing_images(asset_dir: Path):
    existing = {path.name.lower(): path for path in asset_dir.glob("*") if path.is_file()}
    for image in bpy.data.images:
        if image.source != "FILE":
            continue

        current = Path(bpy.path.abspath(image.filepath_raw or image.filepath))
        if current.exists():
            continue

        for candidate in candidate_image_names(current.name):
            match = existing.get(candidate.lower())
            if match:
                image.filepath = str(match)
                image.reload()
                break


def source_mesh_objects():
    meshes = []
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        if len(obj.data.polygons) == 0:
            continue
        if len(obj.material_slots) == 0:
            continue
        meshes.append(obj)
    return meshes


def duplicate_to_joined_mesh(objects, name: str):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.duplicate()
    bpy.ops.object.convert(target="MESH")
    bpy.ops.object.join()
    joined = bpy.context.view_layer.objects.active
    joined.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return joined


def build_voxel_object(source_obj, name: str, octree_depth: int, scale: float):
    bpy.ops.object.select_all(action="DESELECT")
    source_obj.select_set(True)
    bpy.context.view_layer.objects.active = source_obj
    bpy.ops.object.duplicate()
    voxel_obj = bpy.context.view_layer.objects.active
    voxel_obj.name = name

    modifier = voxel_obj.modifiers.new(name="Voxelize", type="REMESH")
    modifier.mode = "BLOCKS"
    modifier.octree_depth = octree_depth
    modifier.scale = scale
    modifier.use_remove_disconnected = False
    modifier.use_smooth_shade = False
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    bpy.ops.object.shade_flat()
    return voxel_obj


def smart_uv_unwrap(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=1.15192, island_margin=0.03)
    bpy.ops.object.mode_set(mode="OBJECT")


def create_bake_material(obj, name: str, image_size: int, texture_path: Path):
    image = bpy.data.images.new(name=f"{name}_Bake", width=image_size, height=image_size, alpha=False)
    image.filepath_raw = str(texture_path)
    image.file_format = "PNG"

    material = bpy.data.materials.new(name=f"{name}_Voxel")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links

    for node in list(nodes):
        nodes.remove(node)

    output = nodes.new(type="ShaderNodeOutputMaterial")
    output.location = (300, 0)

    shader = nodes.new(type="ShaderNodeBsdfPrincipled")
    shader.location = (0, 0)
    shader.inputs["Roughness"].default_value = 0.85

    tex = nodes.new(type="ShaderNodeTexImage")
    tex.location = (-320, 0)
    tex.image = image
    tex.interpolation = "Closest"

    links.new(tex.outputs["Color"], shader.inputs["Base Color"])
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])

    obj.data.materials.clear()
    obj.data.materials.append(material)

    for node in nodes:
        node.select = False
    tex.select = True
    nodes.active = tex

    return image


def bake_diffuse(source_obj, target_obj, bake_image):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 1
    scene.render.bake.use_selected_to_active = True
    scene.render.bake.use_pass_direct = False
    scene.render.bake.use_pass_indirect = False
    scene.render.bake.use_pass_color = True
    scene.render.bake.margin = 6
    scene.render.bake.target = "IMAGE_TEXTURES"
    scene.render.bake.max_ray_distance = max(target_obj.dimensions) * 0.08

    bpy.ops.object.select_all(action="DESELECT")
    source_obj.select_set(True)
    target_obj.select_set(True)
    bpy.context.view_layer.objects.active = target_obj
    bpy.ops.object.bake(type="DIFFUSE")
    bake_image.save()


def look_at_rotation(from_point: Vector, to_point: Vector):
    direction = to_point - from_point
    quat = direction.to_track_quat("-Z", "Y")
    return quat.to_euler()


def add_camera_and_lights(target_obj):
    bbox_world = [target_obj.matrix_world @ Vector(corner) for corner in target_obj.bound_box]
    center = sum(bbox_world, Vector()) / 8.0
    size = max(target_obj.dimensions)

    camera_data = bpy.data.cameras.new("RenderCamera")
    camera = bpy.data.objects.new("RenderCamera", camera_data)
    bpy.context.scene.collection.objects.link(camera)
    camera.location = center + Vector((size * 1.65, -size * 2.1, size * 1.2))
    camera.rotation_euler = look_at_rotation(camera.location, center + Vector((0.0, 0.0, size * 0.15)))
    camera_data.lens = 55
    bpy.context.scene.camera = camera

    key_data = bpy.data.lights.new(name="KeyLight", type="SUN")
    key_data.energy = 2.6
    key = bpy.data.objects.new(name="KeyLight", object_data=key_data)
    bpy.context.scene.collection.objects.link(key)
    key.rotation_euler = (math.radians(45), math.radians(0), math.radians(35))

    fill_data = bpy.data.lights.new(name="FillLight", type="AREA")
    fill_data.energy = 1600
    fill_data.shape = "RECTANGLE"
    fill = bpy.data.objects.new(name="FillLight", object_data=fill_data)
    bpy.context.scene.collection.objects.link(fill)
    fill.location = center + Vector((-size * 1.8, -size * 1.1, size * 1.6))
    fill.rotation_euler = look_at_rotation(fill.location, center)

    bpy.context.scene.render.film_transparent = True
    bpy.context.scene.world.color = (0.94, 0.95, 0.98)


def render_preview(output_path: Path):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(output_path)
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024
    bpy.ops.render.render(write_still=True)


def export_glb(target_obj, output_path: Path):
    bpy.ops.object.select_all(action="DESELECT")
    target_obj.select_set(True)
    bpy.context.view_layer.objects.active = target_obj
    bpy.ops.export_scene.gltf(
        filepath=str(output_path),
        export_format="GLB",
        use_selection=True,
        export_materials="EXPORT",
    )


def save_blend(output_path: Path):
    bpy.ops.wm.save_as_mainfile(filepath=str(output_path))


def main():
    args = parse_args()
    input_path = Path(args.input).resolve()
    output_dir = Path(args.output_dir).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)

    base_name = args.name or input_path.stem
    clean_scene()
    import_asset(input_path)
    relink_missing_images(input_path.parent)

    meshes = source_mesh_objects()
    if not meshes:
        raise RuntimeError("No textured mesh objects were found after import.")

    source_obj = duplicate_to_joined_mesh(meshes, f"{base_name}_Source")
    voxel_obj = build_voxel_object(source_obj, f"{base_name}_Voxel", args.octree_depth, args.scale)
    smart_uv_unwrap(voxel_obj)

    texture_path = output_dir / f"{base_name}_voxel_albedo.png"
    bake_image = create_bake_material(voxel_obj, base_name, args.texture_size, texture_path)
    bake_diffuse(source_obj, voxel_obj, bake_image)

    source_obj.hide_render = True
    source_obj.hide_set(True)

    add_camera_and_lights(voxel_obj)

    preview_path = output_dir / f"{base_name}_voxel_preview.png"
    render_preview(preview_path)

    glb_path = output_dir / f"{base_name}_voxel.glb"
    export_glb(voxel_obj, glb_path)

    blend_path = output_dir / f"{base_name}_voxel.blend"
    save_blend(blend_path)

    print(f"Generated: {glb_path}")
    print(f"Generated: {texture_path}")
    print(f"Generated: {preview_path}")
    print(f"Generated: {blend_path}")


if __name__ == "__main__":
    main()
