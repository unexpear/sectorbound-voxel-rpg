using System.Numerics;
using Sectorbound.Engine;

namespace Sectorbound.Client;

internal static class GeneratedVoxelScale
{
    public const float CubeSizeMeters = 0.05f;
    public const int CubesPerMeter = 20;
    public const float PlayerHeightMeters = 2.0f;
    public const int PlayerHeightCubes = (int)(PlayerHeightMeters * CubesPerMeter);
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
        widthCubes: 12,
        heightCubes: GeneratedVoxelScale.PlayerHeightCubes,
        depthCubes: 8,
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

    public static bool TryAddBlockAsset(List<MeshFace> faces, Int3 blockPosition, string materialId)
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
            new Vector3(blockPosition.X, blockPosition.Y, blockPosition.Z),
            canTarget: true);
        return true;
    }

    public static IEnumerable<MeshFace> BuildPlayerFaces(Vector3 worldFeetPosition, Int3 facingDirection)
    {
        var anchor = new Int3(
            (int)MathF.Floor(worldFeetPosition.X),
            (int)MathF.Floor(worldFeetPosition.Y),
            (int)MathF.Floor(worldFeetPosition.Z));
        var widthMeters = Player.WidthCubes * GeneratedVoxelScale.CubeSizeMeters;
        var depthMeters = Player.DepthCubes * GeneratedVoxelScale.CubeSizeMeters;
        var origin = new Vector3(
            worldFeetPosition.X - (widthMeters * 0.5f),
            worldFeetPosition.Y - 0.05f,
            worldFeetPosition.Z - (depthMeters * 0.5f));

        var faces = new List<MeshFace>(Player.Cuboids.Count * 6);
        AddAssetFaces(faces, Player, anchor, Player.Id, origin, canTarget: false, facingDirection);
        return faces;
    }

    private static void AddAssetFaces(
        List<MeshFace> faces,
        GeneratedVoxelAssetDefinition asset,
        Int3 blockPosition,
        string materialId,
        Vector3 origin,
        bool canTarget,
        Int3? facingDirection = null)
    {
        var yaw = GetYawForFacingDirection(facingDirection ?? new Int3(0, 0, -1));
        var pivot = origin + new Vector3(
            asset.WidthCubes * GeneratedVoxelScale.CubeSizeMeters * 0.5f,
            0f,
            asset.DepthCubes * GeneratedVoxelScale.CubeSizeMeters * 0.5f);

        foreach (var cuboid in asset.Cuboids)
        {
            var min = origin + ToWorld(cuboid.X, cuboid.Y, cuboid.Z);
            var max = origin + ToWorld(cuboid.X + cuboid.Width, cuboid.Y + cuboid.Height, cuboid.Z + cuboid.Depth);
            AddCuboidFaces(faces, blockPosition, materialId, cuboid.PaletteId, min, max, canTarget, yaw, pivot);
        }
    }

    private static Vector3 ToWorld(int x, int y, int z) =>
        new(
            x * GeneratedVoxelScale.CubeSizeMeters,
            y * GeneratedVoxelScale.CubeSizeMeters,
            z * GeneratedVoxelScale.CubeSizeMeters);

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
            face.PaletteId));
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
