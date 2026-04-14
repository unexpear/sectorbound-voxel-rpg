using Sectorbound.Engine;

namespace Sectorbound.Client;

internal readonly record struct ChunkVoxelSample(string MaterialId, bool IsSolid)
{
    public bool IsAir => string.Equals(MaterialId, "air", StringComparison.OrdinalIgnoreCase);

    public static ChunkVoxelSample Air { get; } = new("air", false);
}

internal readonly record struct ChunkSliceBatchProgress(
    ChunkColumnKey Key,
    int CompletedBatches,
    int TotalBatches);

internal sealed class ChunkSliceVolume
{
    private const int NeighborMargin = 1;
    private const int MaxSliceBatchWidth = 5;
    private readonly ChunkVoxelSample[] _samples;

    private ChunkSliceVolume(
        ChunkColumnKey key,
        int startX,
        int startZ,
        int minY,
        int widthInVoxels,
        int heightInVoxels,
        int depthInVoxels,
        int sliceBatchCount)
    {
        Key = key;
        StartX = startX;
        StartZ = startZ;
        MinY = minY;
        WidthInVoxels = widthInVoxels;
        HeightInVoxels = heightInVoxels;
        DepthInVoxels = depthInVoxels;
        SliceBatchCount = sliceBatchCount;
        _samples = new ChunkVoxelSample[widthInVoxels * heightInVoxels * depthInVoxels];
    }

    public ChunkColumnKey Key { get; }
    public int StartX { get; }
    public int StartZ { get; }
    public int MinY { get; }
    public int WidthInVoxels { get; }
    public int HeightInVoxels { get; }
    public int DepthInVoxels { get; }
    public int SliceBatchCount { get; }

    public static int GetSliceBatchCount(GameSession session)
    {
        var chunkSize = session.ChunkSizeInVoxels;
        var width = chunkSize + (NeighborMargin * 2);
        return (width + MaxSliceBatchWidth - 1) / MaxSliceBatchWidth;
    }

    public static ChunkSliceVolume SampleCurrentPlanet(
        GameSession session,
        ChunkColumnKey key,
        Action<ChunkSliceBatchProgress>? progressCallback = null)
    {
        var chunkSize = session.ChunkSizeInVoxels;
        var startX = (key.X * chunkSize) - NeighborMargin;
        var startZ = (key.Z * chunkSize) - NeighborMargin;
        var minY = session.MinWorldY;
        var width = chunkSize + (NeighborMargin * 2);
        var depth = chunkSize + (NeighborMargin * 2);
        var height = session.MaxWorldY - session.MinWorldY + 1;
        var sliceBatchCount = GetSliceBatchCount(session);
        var volume = new ChunkSliceVolume(key, startX, startZ, minY, width, height, depth, sliceBatchCount);
        var completedBatches = 0;

        for (var xOffset = 0; xOffset < width; xOffset += MaxSliceBatchWidth)
        {
            var sliceCount = Math.Min(MaxSliceBatchWidth, width - xOffset);
            var origin = new Int3(startX + xOffset, minY, startZ);
            var slices = session.GenerateCurrentPlanetSlices(origin, sliceCount, height, depth);

            foreach (var slice in slices)
            {
                foreach (var block in slice.Blocks)
                {
                    volume.Set(block.Position, new ChunkVoxelSample(block.MaterialId, block.IsSolid));
                }
            }

            completedBatches++;
            progressCallback?.Invoke(new ChunkSliceBatchProgress(key, completedBatches, sliceBatchCount));
        }

        return volume;
    }

    public ChunkVoxelSample GetOrAir(Int3 position)
    {
        if (!Contains(position))
        {
            return ChunkVoxelSample.Air;
        }

        return _samples[IndexOf(position)];
    }

    private bool Contains(Int3 position) =>
        position.X >= StartX
        && position.X < StartX + WidthInVoxels
        && position.Y >= MinY
        && position.Y < MinY + HeightInVoxels
        && position.Z >= StartZ
        && position.Z < StartZ + DepthInVoxels;

    private int IndexOf(Int3 position)
    {
        var localX = position.X - StartX;
        var localY = position.Y - MinY;
        var localZ = position.Z - StartZ;
        return ((localX * DepthInVoxels) + localZ) * HeightInVoxels + localY;
    }

    private void Set(Int3 position, ChunkVoxelSample sample) => _samples[IndexOf(position)] = sample;
}
