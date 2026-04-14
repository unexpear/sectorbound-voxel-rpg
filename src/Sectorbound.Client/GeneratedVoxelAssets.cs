using System.Numerics;
using Sectorbound.Engine;

namespace Sectorbound.Client;

internal static class GeneratedVoxelScale
{
    public const float AssetCubeSize = 0.05f;
    public const float ActorCubeSize = 0.05f;
    public static ActorScaleDef PlayerScale => ActorScaleCatalog.GetPlayerScale();
    public static int PlayerHeightCubes => (int)MathF.Round(PlayerScale.HeightMeters / ActorCubeSize);
    public static int PlayerWidthCubes => (int)MathF.Round(PlayerScale.WidthMeters / ActorCubeSize);
    public static int PlayerDepthCubes => (int)MathF.Round(PlayerScale.DepthMeters / ActorCubeSize);
}

internal readonly record struct VoxelCuboid(
    int X,
    int Y,
    int Z,
    int Width,
    int Height,
    int Depth,
    string PaletteId);

internal sealed class GeneratedVoxelAssetDefinition
{
    public GeneratedVoxelAssetDefinition(
        string id,
        int widthCubes,
        int heightCubes,
        int depthCubes,
        IReadOnlyList<VoxelCuboid> cuboids)
    {
        Id = id;
        WidthCubes = widthCubes;
        HeightCubes = heightCubes;
        DepthCubes = depthCubes;
        Cuboids = cuboids;
    }

    public string Id { get; }
    public int WidthCubes { get; }
    public int HeightCubes { get; }
    public int DepthCubes { get; }
    public IReadOnlyList<VoxelCuboid> Cuboids { get; }
}

internal static class GeneratedVoxelAssets
{
    private static readonly GeneratedVoxelAssetDefinition Workbench = new(
        "workbench-block",
        widthCubes: 20,
        heightCubes: 20,
        depthCubes: 20,
        cuboids:
        [
            new VoxelCuboid(1, 11, 4, 18, 3, 12, "bench-top"),
            new VoxelCuboid(3, 6, 6, 14, 2, 8, "bench-shelf"),
            new VoxelCuboid(15, 11, 4, 3, 8, 12, "bench-back"),
            new VoxelCuboid(2, 0, 5, 3, 11, 3, "bench-leg"),
            new VoxelCuboid(15, 0, 5, 3, 11, 3, "bench-leg"),
            new VoxelCuboid(2, 0, 12, 3, 11, 3, "bench-leg"),
            new VoxelCuboid(15, 0, 12, 3, 11, 3, "bench-leg"),
            new VoxelCuboid(4, 4, 5, 12, 1, 2, "bench-brace"),
            new VoxelCuboid(4, 4, 13, 12, 1, 2, "bench-brace"),
            new VoxelCuboid(4, 14, 5, 6, 2, 4, "bench-accent"),
            new VoxelCuboid(11, 14, 7, 4, 3, 4, "bench-metal"),
            new VoxelCuboid(7, 14, 11, 3, 3, 3, "bench-tool")
        ]);

    private static readonly GeneratedVoxelAssetDefinition Player = new(
        "player-proxy",
        widthCubes: GeneratedVoxelScale.PlayerWidthCubes,
        heightCubes: GeneratedVoxelScale.PlayerHeightCubes,
        depthCubes: GeneratedVoxelScale.PlayerDepthCubes,
        cuboids:
        [
            new VoxelCuboid(1, 0, 2, 3, 4, 3, "player-boot"),
            new VoxelCuboid(8, 0, 2, 3, 4, 3, "player-boot"),
            new VoxelCuboid(2, 4, 2, 3, 9, 3, "player-leg"),
            new VoxelCuboid(8, 4, 2, 3, 9, 3, "player-leg"),
            new VoxelCuboid(2, 13, 2, 9, 11, 4, "player-tunic"),
            new VoxelCuboid(2, 19, 1, 9, 1, 6, "player-belt"),
            new VoxelCuboid(1, 24, 2, 11, 2, 4, "player-trim"),
            new VoxelCuboid(0, 15, 2, 2, 8, 3, "player-sleeve"),
            new VoxelCuboid(11, 15, 2, 2, 8, 3, "player-sleeve"),
            new VoxelCuboid(1, 14, 7, 10, 10, 1, "player-cloak"),
            new VoxelCuboid(3, 26, 1, 7, 7, 6, "player-skin"),
            new VoxelCuboid(2, 33, 1, 8, 2, 7, "player-hair"),
            new VoxelCuboid(4, 29, 0, 4, 2, 1, "player-visor")
        ]);

    public static bool TryAddBlockAsset(List<MeshFace> faces, Int3 blockPosition, string materialId, float renderScale)
    {
        GeneratedVoxelAssetDefinition? asset = materialId switch
        {
            "workbench-block" => Workbench,
            _ => null
        };

        if (asset is null)
        {
            return false;
        }

        AddAssetFaces(
            faces,
            asset,
            blockPosition,
            materialId,
            new Vector3(blockPosition.X * renderScale, blockPosition.Y * renderScale, blockPosition.Z * renderScale),
            GeneratedVoxelScale.AssetCubeSize * renderScale,
            canTarget: true);
        return true;
    }

    public static void AddScenicPropFaces(List<MeshFace> faces, GameSession session, IEnumerable<ScenicPropInstance> props)
    {
        var renderScale = session.VoxelRenderScale;
        foreach (var prop in props)
        {
            if (!ScenicPropCatalog.TryGetDefinition(prop.AssetId, out var definition) || definition is null)
            {
                continue;
            }

            var asset = ImportedVoxelAssets.GetProp(prop.AssetId);
            if (asset is null || asset.HeightCubes <= 0)
            {
                continue;
            }

            var cubeSize = definition.HeightOriginalBlocks / asset.HeightCubes;
            var widthMeters = asset.WidthCubes * cubeSize;
            var depthMeters = asset.DepthCubes * cubeSize;
            var worldAnchor = new Vector3(
                prop.AnchorVoxel.X * renderScale,
                prop.AnchorVoxel.Y * renderScale,
                prop.AnchorVoxel.Z * renderScale);
            var origin = new Vector3(
                worldAnchor.X - (widthMeters * 0.5f),
                worldAnchor.Y,
                worldAnchor.Z - (depthMeters * 0.5f));

            AddImportedAssetFaces(
                faces,
                asset,
                prop.AnchorVoxel,
                prop.AssetId,
                origin,
                new Vector3(cubeSize, cubeSize, cubeSize),
                canTarget: false,
                prop.FacingDirection);
        }
    }

    public static IEnumerable<MeshFace> BuildPlayerFaces(CharacterSelection selection, Vector3 worldFeetPosition, Int3 facingDirection)
    {
        var profile = ActorRenderProfileCatalog.GetPlayerProfile(selection);
        var imported = ImportedVoxelAssets.GetActor(profile) ?? ImportedVoxelAssets.GetPlayerDefault();
        if (imported is not null)
        {
            return BuildImportedActorFaces(imported, Player.Id, profile, worldFeetPosition, facingDirection);
        }

        var stanceOffset = GetFacingRelativeOffset(profile.GetStanceOffset(useCombatStance: false), facingDirection);
        var pivotOffset = GetFacingRelativeOffset(profile.PivotOffset, facingDirection);
        var anchor = new Int3(
            (int)MathF.Floor(worldFeetPosition.X),
            (int)MathF.Floor(worldFeetPosition.Y),
            (int)MathF.Floor(worldFeetPosition.Z));
        var widthMeters = profile.Scale.WidthMeters;
        var depthMeters = profile.Scale.DepthMeters;
        var origin = new Vector3(
            worldFeetPosition.X - (widthMeters * 0.5f) + stanceOffset.X,
            worldFeetPosition.Y - GeneratedVoxelScale.ActorCubeSize + stanceOffset.Y,
            worldFeetPosition.Z - (depthMeters * 0.5f) + stanceOffset.Z);

        var faces = new List<MeshFace>(Player.Cuboids.Count * 6);
        AddAssetFaces(
            faces,
            Player,
            anchor,
            Player.Id,
            origin,
            GeneratedVoxelScale.ActorCubeSize,
            canTarget: false,
            facingDirection,
            pivotOffset);
        return faces;
    }

    internal static IEnumerable<MeshFace> BuildImportedActorFaces(
        ImportedVoxelAssetDefinition asset,
        string materialId,
        ActorRenderProfile profile,
        Vector3 worldFeetPosition,
        Int3 facingDirection,
        bool useCombatStance = false)
    {
        var stanceOffset = GetFacingRelativeOffset(profile.GetStanceOffset(useCombatStance), facingDirection);
        var pivotOffset = GetFacingRelativeOffset(profile.PivotOffset, facingDirection);
        var anchor = new Int3(
            (int)MathF.Floor(worldFeetPosition.X),
            (int)MathF.Floor(worldFeetPosition.Y),
            (int)MathF.Floor(worldFeetPosition.Z));
        var cubeScale = GetActorCubeScale(asset, profile.Scale);
        var origin = new Vector3(
            worldFeetPosition.X - (profile.Scale.WidthMeters * 0.5f) + stanceOffset.X,
            worldFeetPosition.Y - cubeScale.Y + stanceOffset.Y,
            worldFeetPosition.Z - (profile.Scale.DepthMeters * 0.5f) + stanceOffset.Z);

        var faces = new List<MeshFace>(asset.Faces.Count);
        AddImportedAssetFaces(
            faces,
            asset,
            anchor,
            materialId,
            origin,
            cubeScale,
            canTarget: false,
            facingDirection,
            pivotOffset);
        return faces;
    }

    private static void AddAssetFaces(
        List<MeshFace> faces,
        GeneratedVoxelAssetDefinition asset,
        Int3 blockPosition,
        string materialId,
        Vector3 origin,
        float cubeSize,
        bool canTarget,
        Int3? facingDirection = null,
        Vector3? pivotOffset = null)
    {
        var yaw = GetYawForFacingDirection(facingDirection ?? new Int3(0, 0, -1));
        var pivot = origin + new Vector3(
            asset.WidthCubes * cubeSize * 0.5f,
            0f,
            asset.DepthCubes * cubeSize * 0.5f)
            + (pivotOffset ?? Vector3.Zero);

        foreach (var cuboid in asset.Cuboids)
        {
            var min = origin + ToWorld(cuboid.X, cuboid.Y, cuboid.Z, cubeSize);
            var max = origin + ToWorld(cuboid.X + cuboid.Width, cuboid.Y + cuboid.Height, cuboid.Z + cuboid.Depth, cubeSize);
            AddCuboidFaces(faces, blockPosition, materialId, cuboid.PaletteId, min, max, canTarget, yaw, pivot);
        }
    }

    private static void AddImportedAssetFaces(
        List<MeshFace> faces,
        ImportedVoxelAssetDefinition asset,
        Int3 anchor,
        string materialId,
        Vector3 origin,
        Vector3 cubeScale,
        bool canTarget,
        Int3 facingDirection,
        Vector3? pivotOffset = null)
    {
        var yaw = GetYawForFacingDirection(facingDirection);
        var pivot = origin + new Vector3(
            asset.WidthCubes * cubeScale.X * 0.5f,
            0f,
            asset.DepthCubes * cubeScale.Z * 0.5f)
            + (pivotOffset ?? Vector3.Zero);

        foreach (var face in asset.Faces)
        {
            AddFace(
                faces,
                new MeshFace(
                    origin + Multiply(face.A, cubeScale),
                    origin + Multiply(face.B, cubeScale),
                    origin + Multiply(face.C, cubeScale),
                    origin + Multiply(face.D, cubeScale),
                    anchor,
                    face.Normal,
                    materialId,
                    CanTarget: canTarget,
                    OverrideColor: face.Color),
                yaw,
                pivot);
        }
    }

    private static Vector3 GetActorCubeScale(ImportedVoxelAssetDefinition asset, ActorScaleDef scale) =>
        new(
            scale.WidthMeters / Math.Max(1, asset.WidthCubes),
            scale.HeightMeters / Math.Max(1, asset.HeightCubes),
            scale.DepthMeters / Math.Max(1, asset.DepthCubes));

    private static Vector3 GetFacingRelativeOffset(Vector3 localOffset, Int3 facingDirection)
    {
        if (localOffset.LengthSquared() < 0.000001f)
        {
            return Vector3.Zero;
        }

        var yaw = GetYawForFacingDirection(facingDirection);
        if (MathF.Abs(yaw) < 0.001f)
        {
            return localOffset;
        }

        return Vector3.Transform(localOffset, Matrix4x4.CreateRotationY(yaw));
    }

    private static Vector3 Multiply(Vector3 value, Vector3 scale) =>
        new(value.X * scale.X, value.Y * scale.Y, value.Z * scale.Z);

    private static Vector3 ToWorld(int x, int y, int z, float cubeSize) =>
        new(
            x * cubeSize,
            y * cubeSize,
            z * cubeSize);

    private static void AddCuboidFaces(
        List<MeshFace> faces,
        Int3 blockPosition,
        string materialId,
        string paletteId,
        Vector3 min,
        Vector3 max,
        bool canTarget,
        float yaw,
        Vector3 pivot)
    {
        AddFace(faces, new MeshFace(
            new Vector3(min.X, max.Y, min.Z),
            new Vector3(max.X, max.Y, min.Z),
            new Vector3(max.X, max.Y, max.Z),
            new Vector3(min.X, max.Y, max.Z),
            blockPosition,
            new Int3(0, 1, 0),
            materialId,
            CanTarget: canTarget,
            PaletteId: paletteId), yaw, pivot);

        AddFace(faces, new MeshFace(
            new Vector3(min.X, min.Y, max.Z),
            new Vector3(max.X, min.Y, max.Z),
            new Vector3(max.X, min.Y, min.Z),
            new Vector3(min.X, min.Y, min.Z),
            blockPosition,
            new Int3(0, -1, 0),
            materialId,
            CanTarget: canTarget,
            PaletteId: paletteId), yaw, pivot);

        AddFace(faces, new MeshFace(
            new Vector3(max.X, min.Y, min.Z),
            new Vector3(max.X, max.Y, min.Z),
            new Vector3(max.X, max.Y, max.Z),
            new Vector3(max.X, min.Y, max.Z),
            blockPosition,
            new Int3(1, 0, 0),
            materialId,
            CanTarget: canTarget,
            PaletteId: paletteId), yaw, pivot);

        AddFace(faces, new MeshFace(
            new Vector3(min.X, min.Y, max.Z),
            new Vector3(min.X, max.Y, max.Z),
            new Vector3(min.X, max.Y, min.Z),
            new Vector3(min.X, min.Y, min.Z),
            blockPosition,
            new Int3(-1, 0, 0),
            materialId,
            CanTarget: canTarget,
            PaletteId: paletteId), yaw, pivot);

        AddFace(faces, new MeshFace(
            new Vector3(max.X, min.Y, max.Z),
            new Vector3(max.X, max.Y, max.Z),
            new Vector3(min.X, max.Y, max.Z),
            new Vector3(min.X, min.Y, max.Z),
            blockPosition,
            new Int3(0, 0, 1),
            materialId,
            CanTarget: canTarget,
            PaletteId: paletteId), yaw, pivot);

        AddFace(faces, new MeshFace(
            new Vector3(min.X, min.Y, min.Z),
            new Vector3(min.X, max.Y, min.Z),
            new Vector3(max.X, max.Y, min.Z),
            new Vector3(max.X, min.Y, min.Z),
            blockPosition,
            new Int3(0, 0, -1),
            materialId,
            CanTarget: canTarget,
            PaletteId: paletteId), yaw, pivot);
    }

    private static void AddFace(List<MeshFace> faces, MeshFace face, float yaw, Vector3 pivot)
    {
        if (MathF.Abs(yaw) < 0.001f)
        {
            faces.Add(face);
            return;
        }

        var rotation = Matrix4x4.CreateRotationY(yaw, pivot);
        faces.Add(new MeshFace(
            Vector3.Transform(face.A, rotation),
            Vector3.Transform(face.B, rotation),
            Vector3.Transform(face.C, rotation),
            Vector3.Transform(face.D, rotation),
            face.BlockPosition,
            RotateNormal(face.Normal, yaw),
            face.MaterialId,
            face.CanTarget,
            face.PaletteId,
            face.OverrideColor));
    }

    private static Int3 RotateNormal(Int3 normal, float yaw)
    {
        if (normal.Y != 0)
        {
            return normal;
        }

        return yaw switch
        {
            > -0.01f and < 0.01f => normal,
            < -1.4f and > -1.8f => new Int3(-normal.Z, 0, normal.X),
            > 1.4f and < 1.8f => new Int3(normal.Z, 0, -normal.X),
            _ => new Int3(-normal.X, 0, -normal.Z)
        };
    }

    private static float GetYawForFacingDirection(Int3 facingDirection) =>
        facingDirection switch
        {
            { X: 0, Z: -1 } => 0f,
            { X: 1, Z: 0 } => -MathF.PI * 0.5f,
            { X: -1, Z: 0 } => MathF.PI * 0.5f,
            { X: 0, Z: 1 } => MathF.PI,
            _ => 0f
        };
}
