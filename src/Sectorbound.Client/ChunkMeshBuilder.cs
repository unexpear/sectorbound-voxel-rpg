using System.Numerics;
using Sectorbound.Engine;

namespace Sectorbound.Client;

internal sealed class ChunkMeshBuilder
{
    public ChunkRenderMesh Build(
        GameSession session,
        ChunkColumnKey key,
        Action<ChunkSliceBatchProgress>? sliceBatchProgress = null)
    {
        var scale = session.VoxelRenderScale;
        var chunkSize = session.ChunkSizeInVoxels;
        var faces = new List<MeshFace>(chunkSize * chunkSize * 16);
        var startX = key.X * chunkSize;
        var startZ = key.Z * chunkSize;
        var sampledVolume = ChunkSliceVolume.SampleCurrentPlanet(session, key, sliceBatchProgress);

        for (var x = startX; x < startX + chunkSize; x++)
        {
            for (var z = startZ; z < startZ + chunkSize; z++)
            {
                for (var y = session.MinWorldY; y <= session.MaxWorldY; y++)
                {
                    var blockPosition = new Int3(x, y, z);
                    var material = sampledVolume.GetOrAir(blockPosition);
                    if (material.IsAir)
                    {
                        continue;
                    }

                    if (GeneratedVoxelAssets.TryAddBlockAsset(faces, blockPosition, material.MaterialId, scale))
                    {
                        continue;
                    }

                    AddFaceIfExposed(sampledVolume, faces, blockPosition, material, new Int3(0, 1, 0), scale);
                    AddFaceIfExposed(sampledVolume, faces, blockPosition, material, new Int3(1, 0, 0), scale);
                    AddFaceIfExposed(sampledVolume, faces, blockPosition, material, new Int3(-1, 0, 0), scale);
                    AddFaceIfExposed(sampledVolume, faces, blockPosition, material, new Int3(0, 0, 1), scale);
                    AddFaceIfExposed(sampledVolume, faces, blockPosition, material, new Int3(0, 0, -1), scale);
                }
            }
        }

        GeneratedVoxelAssets.AddScenicPropFaces(faces, session, session.GetScenicPropsForCurrentPlanet(key.X, key.Z));
        return new ChunkRenderMesh(key, faces);
    }

    private static void AddFaceIfExposed(
        ChunkSliceVolume sampledVolume,
        List<MeshFace> faces,
        Int3 blockPosition,
        ChunkVoxelSample material,
        Int3 normal,
        float scale)
    {
        var neighbor = new Int3(blockPosition.X + normal.X, blockPosition.Y + normal.Y, blockPosition.Z + normal.Z);
        var neighborMaterial = sampledVolume.GetOrAir(neighbor);
        if (IsFaceOccluded(material, neighborMaterial))
        {
            return;
        }

        faces.Add(CreateFace(blockPosition, normal, material.MaterialId, scale, material.IsSolid));
    }

    private static bool IsFaceOccluded(ChunkVoxelSample material, ChunkVoxelSample neighborMaterial)
    {
        if (neighborMaterial.IsAir)
        {
            return false;
        }

        if (material.IsSolid)
        {
            return neighborMaterial.IsSolid;
        }

        return neighborMaterial.IsSolid
            || string.Equals(neighborMaterial.MaterialId, material.MaterialId, StringComparison.OrdinalIgnoreCase);
    }

    private static MeshFace CreateFace(Int3 blockPosition, Int3 normal, string materialId, float scale, bool canTarget)
    {
        var x = blockPosition.X * scale;
        var y = blockPosition.Y * scale;
        var z = blockPosition.Z * scale;

        return normal switch
        {
            { X: 0, Y: 1, Z: 0 } => new MeshFace(
                new Vector3(x, y + scale, z),
                new Vector3(x + scale, y + scale, z),
                new Vector3(x + scale, y + scale, z + scale),
                new Vector3(x, y + scale, z + scale),
                blockPosition,
                normal,
                materialId,
                CanTarget: canTarget),
            { X: 1, Y: 0, Z: 0 } => new MeshFace(
                new Vector3(x + scale, y, z),
                new Vector3(x + scale, y + scale, z),
                new Vector3(x + scale, y + scale, z + scale),
                new Vector3(x + scale, y, z + scale),
                blockPosition,
                normal,
                materialId,
                CanTarget: canTarget),
            { X: -1, Y: 0, Z: 0 } => new MeshFace(
                new Vector3(x, y, z + scale),
                new Vector3(x, y + scale, z + scale),
                new Vector3(x, y + scale, z),
                new Vector3(x, y, z),
                blockPosition,
                normal,
                materialId,
                CanTarget: canTarget),
            { X: 0, Y: 0, Z: 1 } => new MeshFace(
                new Vector3(x + scale, y, z + scale),
                new Vector3(x + scale, y + scale, z + scale),
                new Vector3(x, y + scale, z + scale),
                new Vector3(x, y, z + scale),
                blockPosition,
                normal,
                materialId,
                CanTarget: canTarget),
            { X: 0, Y: 0, Z: -1 } => new MeshFace(
                new Vector3(x, y, z),
                new Vector3(x, y + scale, z),
                new Vector3(x + scale, y + scale, z),
                new Vector3(x + scale, y, z),
                blockPosition,
                normal,
                materialId,
                CanTarget: canTarget),
            _ => throw new InvalidOperationException($"Unsupported face normal {normal}.")
        };
    }
}
