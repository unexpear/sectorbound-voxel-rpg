using System.Numerics;
using Sectorbound.Engine;

namespace Sectorbound.Client;

internal sealed class ChunkMeshBuilder
{
    public ChunkRenderMesh Build(GameSession session, ChunkColumnKey key)
    {
        var faces = new List<MeshFace>(2048);
        var chunkSize = session.ChunkSizeInVoxels;
        var startX = key.X * chunkSize;
        var startZ = key.Z * chunkSize;

        for (var x = startX; x < startX + chunkSize; x++)
        {
            for (var z = startZ; z < startZ + chunkSize; z++)
            {
                for (var y = session.MinWorldY; y <= session.MaxWorldY; y++)
                {
                    var blockPosition = new Int3(x, y, z);
                    if (!session.IsSolidAtCurrentPlanet(blockPosition))
                    {
                        continue;
                    }

                    var materialId = session.GetMaterialIdAtCurrentPlanet(blockPosition);
                    if (GeneratedVoxelAssets.TryAddBlockAsset(faces, blockPosition, materialId))
                    {
                        continue;
                    }

                    AddFaceIfExposed(session, faces, blockPosition, materialId, new Int3(0, 1, 0));
                    AddFaceIfExposed(session, faces, blockPosition, materialId, new Int3(1, 0, 0));
                    AddFaceIfExposed(session, faces, blockPosition, materialId, new Int3(-1, 0, 0));
                    AddFaceIfExposed(session, faces, blockPosition, materialId, new Int3(0, 0, 1));
                    AddFaceIfExposed(session, faces, blockPosition, materialId, new Int3(0, 0, -1));
                }
            }
        }

        return new ChunkRenderMesh(key, faces);
    }

    private static void AddFaceIfExposed(
        GameSession session,
        List<MeshFace> faces,
        Int3 blockPosition,
        string materialId,
        Int3 normal)
    {
        var neighbor = new Int3(blockPosition.X + normal.X, blockPosition.Y + normal.Y, blockPosition.Z + normal.Z);
        if (session.IsSolidAtCurrentPlanet(neighbor))
        {
            return;
        }

        faces.Add(CreateFace(blockPosition, normal, materialId));
    }

    private static MeshFace CreateFace(Int3 blockPosition, Int3 normal, string materialId)
    {
        var x = blockPosition.X;
        var y = blockPosition.Y;
        var z = blockPosition.Z;

        return normal switch
        {
            { X: 0, Y: 1, Z: 0 } => new MeshFace(
                new Vector3(x, y + 1, z),
                new Vector3(x + 1, y + 1, z),
                new Vector3(x + 1, y + 1, z + 1),
                new Vector3(x, y + 1, z + 1),
                blockPosition,
                normal,
                materialId,
                CanTarget: true),
            { X: 1, Y: 0, Z: 0 } => new MeshFace(
                new Vector3(x + 1, y, z),
                new Vector3(x + 1, y + 1, z),
                new Vector3(x + 1, y + 1, z + 1),
                new Vector3(x + 1, y, z + 1),
                blockPosition,
                normal,
                materialId,
                CanTarget: true),
            { X: -1, Y: 0, Z: 0 } => new MeshFace(
                new Vector3(x, y, z + 1),
                new Vector3(x, y + 1, z + 1),
                new Vector3(x, y + 1, z),
                new Vector3(x, y, z),
                blockPosition,
                normal,
                materialId,
                CanTarget: true),
            { X: 0, Y: 0, Z: 1 } => new MeshFace(
                new Vector3(x + 1, y, z + 1),
                new Vector3(x + 1, y + 1, z + 1),
                new Vector3(x, y + 1, z + 1),
                new Vector3(x, y, z + 1),
                blockPosition,
                normal,
                materialId,
                CanTarget: true),
            { X: 0, Y: 0, Z: -1 } => new MeshFace(
                new Vector3(x, y, z),
                new Vector3(x, y + 1, z),
                new Vector3(x + 1, y + 1, z),
                new Vector3(x + 1, y, z),
                blockPosition,
                normal,
                materialId,
                CanTarget: true),
            _ => throw new InvalidOperationException($"Unsupported face normal {normal}.")
        };
    }
}
