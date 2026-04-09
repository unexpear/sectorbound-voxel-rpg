using System.Numerics;
using Sectorbound.Engine;

namespace Sectorbound.Client;

internal readonly record struct ChunkColumnKey(int X, int Z);

internal readonly record struct MeshFace(
    Vector3 A,
    Vector3 B,
    Vector3 C,
    Vector3 D,
    Int3 BlockPosition,
    Int3 Normal,
    string MaterialId,
    bool CanTarget,
    string? PaletteId = null);

internal sealed class ChunkRenderMesh
{
    public ChunkRenderMesh(ChunkColumnKey key, IReadOnlyList<MeshFace> faces)
    {
        Key = key;
        Faces = faces;
    }

    public ChunkColumnKey Key { get; }
    public IReadOnlyList<MeshFace> Faces { get; }
}

internal sealed class ProjectedFace
{
    public ProjectedFace(
        MeshFace face,
        PointF[] screenPoints,
        RectangleF bounds,
        float depth,
        Color baseColor)
    {
        Face = face;
        ScreenPoints = screenPoints;
        Bounds = bounds;
        Depth = depth;
        BaseColor = baseColor;
    }

    public MeshFace Face { get; }
    public PointF[] ScreenPoints { get; }
    public RectangleF Bounds { get; }
    public float Depth { get; }
    public Color BaseColor { get; }

    public bool Contains(Point point)
    {
        if (!Bounds.Contains(point))
        {
            return false;
        }

        return PointInConvexPolygon(point, ScreenPoints);
    }

    private static bool PointInConvexPolygon(Point point, PointF[] polygon)
    {
        var hasPositive = false;
        var hasNegative = false;
        for (var i = 0; i < polygon.Length; i++)
        {
            var current = polygon[i];
            var next = polygon[(i + 1) % polygon.Length];
            var cross = ((next.X - current.X) * (point.Y - current.Y))
                - ((next.Y - current.Y) * (point.X - current.X));

            if (cross > 0.01f)
            {
                hasPositive = true;
            }
            else if (cross < -0.01f)
            {
                hasNegative = true;
            }

            if (hasPositive && hasNegative)
            {
                return false;
            }
        }

        return true;
    }
}
