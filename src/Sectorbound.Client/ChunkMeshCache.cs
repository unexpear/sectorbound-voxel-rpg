using Sectorbound.Engine;

namespace Sectorbound.Client;

internal sealed class ChunkMeshCache
{
    private readonly ChunkMeshBuilder _builder = new();
    private readonly Dictionary<ChunkColumnKey, ChunkRenderMesh> _meshes = [];

    public ChunkRenderMesh GetOrBuild(GameSession session, ChunkColumnKey key)
    {
        if (_meshes.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var mesh = _builder.Build(session, key);
        _meshes[key] = mesh;
        return mesh;
    }

    public void InvalidateAround(GameSession session, Int3 worldPosition)
    {
        var chunk = session.GetChunkCoordinateForWorldPosition(worldPosition);
        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dz = -1; dz <= 1; dz++)
            {
                _meshes.Remove(new ChunkColumnKey(chunk.X + dx, chunk.Z + dz));
            }
        }
    }

    public void Clear() => _meshes.Clear();
}
