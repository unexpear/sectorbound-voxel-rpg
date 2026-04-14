using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using Sectorbound.Engine;

namespace Sectorbound.Client;

internal readonly record struct ImportedVoxelFaceTemplate(
    Vector3 A,
    Vector3 B,
    Vector3 C,
    Vector3 D,
    Int3 Normal,
    Color Color);

internal sealed class ImportedVoxelAssetDefinition
{
    public ImportedVoxelAssetDefinition(
        int widthCubes,
        int heightCubes,
        int depthCubes,
        IReadOnlyList<ImportedVoxelFaceTemplate> faces)
    {
        WidthCubes = widthCubes;
        HeightCubes = heightCubes;
        DepthCubes = depthCubes;
        Faces = faces;
    }

    public int WidthCubes { get; }
    public int HeightCubes { get; }
    public int DepthCubes { get; }
    public IReadOnlyList<ImportedVoxelFaceTemplate> Faces { get; }
}

internal static class ImportedVoxelAssets
{
    private const string PlayerDefaultRelativePath = "Content\\Voxel\\player-default.vox";

    private static readonly ConcurrentDictionary<string, Lazy<ImportedVoxelAssetDefinition?>> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly Lazy<ImportedVoxelAssetDefinition?> PlayerDefault = new(
        () => LoadRelative(PlayerDefaultRelativePath));

    public static ImportedVoxelAssetDefinition? GetPlayerDefault() => PlayerDefault.Value;

    public static ImportedVoxelAssetDefinition? GetCharacter(CharacterSelection selection)
    {
        var relativePath = CharacterCatalog.GetRelativeAssetPath(selection);
        return GetOrLoad(relativePath) ?? PlayerDefault.Value;
    }

    public static ImportedVoxelAssetDefinition? GetActor(ActorRenderProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.RelativeAssetPath))
        {
            return null;
        }

        return GetOrLoad(profile.RelativeAssetPath);
    }

    public static ImportedVoxelAssetDefinition? GetProp(string assetId)
    {
        if (!ScenicPropCatalog.TryGetDefinition(assetId, out var definition) || definition is null)
        {
            return null;
        }

        return GetOrLoad(definition.RelativePath);
    }

    private static ImportedVoxelAssetDefinition? GetOrLoad(string relativePath) =>
        Cache.GetOrAdd(
            relativePath,
            static path => new Lazy<ImportedVoxelAssetDefinition?>(() => LoadRelative(path))).Value;

    private static ImportedVoxelAssetDefinition? LoadRelative(string relativePath)
    {
        var path = Path.Combine(AppContext.BaseDirectory, relativePath);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return BuildDefinition(VoxFileReader.Load(path));
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Failed to load voxel asset '{path}': {exception}");
            return null;
        }
    }

    private static ImportedVoxelAssetDefinition BuildDefinition(VoxModel model)
    {
        if (model.Voxels.Count == 0)
        {
            throw new InvalidDataException("Voxel asset contains no voxels.");
        }

        var minX = model.Voxels.Min(v => (int)v.X);
        var maxX = model.Voxels.Max(v => (int)v.X);
        var minY = model.Voxels.Min(v => (int)v.Y);
        var maxY = model.Voxels.Max(v => (int)v.Y);
        var minZ = model.Voxels.Min(v => (int)v.Z);
        var maxZ = model.Voxels.Max(v => (int)v.Z);

        var width = (maxX - minX) + 1;
        var height = (maxY - minY) + 1;
        var depth = (maxZ - minZ) + 1;
        var colors = new int[width, height, depth];

        foreach (var voxel in model.Voxels)
        {
            var color = model.ResolveColor(voxel.ColorIndex);
            if (color.A <= 0)
            {
                continue;
            }

            colors[voxel.X - minX, voxel.Y - minY, voxel.Z - minZ] = color.ToArgb();
        }

        var faces = BuildSurfaceFaces(colors);
        return new ImportedVoxelAssetDefinition(width, height, depth, faces);
    }

    private static List<ImportedVoxelFaceTemplate> BuildSurfaceFaces(int[,,] colors)
    {
        var width = colors.GetLength(0);
        var height = colors.GetLength(1);
        var depth = colors.GetLength(2);
        var faces = new List<ImportedVoxelFaceTemplate>();

        for (var x = 0; x < width; x++)
        {
            var negativeMask = new int[height, depth];
            var positiveMask = new int[height, depth];
            for (var y = 0; y < height; y++)
            {
                for (var z = 0; z < depth; z++)
                {
                    var color = colors[x, y, z];
                    if (color == 0)
                    {
                        continue;
                    }

                    if (x == 0 || colors[x - 1, y, z] == 0)
                    {
                        negativeMask[y, z] = color;
                    }

                    if (x == width - 1 || colors[x + 1, y, z] == 0)
                    {
                        positiveMask[y, z] = color;
                    }
                }
            }

            AppendMaskFaces(
                negativeMask,
                faces,
                (y, z, heightSpan, widthSpan, argb) => new ImportedVoxelFaceTemplate(
                    new Vector3(x, y, z + widthSpan),
                    new Vector3(x, y + heightSpan, z + widthSpan),
                    new Vector3(x, y + heightSpan, z),
                    new Vector3(x, y, z),
                    new Int3(-1, 0, 0),
                    Color.FromArgb(argb)));

            AppendMaskFaces(
                positiveMask,
                faces,
                (y, z, heightSpan, widthSpan, argb) => new ImportedVoxelFaceTemplate(
                    new Vector3(x + 1, y, z),
                    new Vector3(x + 1, y + heightSpan, z),
                    new Vector3(x + 1, y + heightSpan, z + widthSpan),
                    new Vector3(x + 1, y, z + widthSpan),
                    new Int3(1, 0, 0),
                    Color.FromArgb(argb)));
        }

        for (var y = 0; y < height; y++)
        {
            var negativeMask = new int[width, depth];
            var positiveMask = new int[width, depth];
            for (var x = 0; x < width; x++)
            {
                for (var z = 0; z < depth; z++)
                {
                    var color = colors[x, y, z];
                    if (color == 0)
                    {
                        continue;
                    }

                    if (y == 0 || colors[x, y - 1, z] == 0)
                    {
                        negativeMask[x, z] = color;
                    }

                    if (y == height - 1 || colors[x, y + 1, z] == 0)
                    {
                        positiveMask[x, z] = color;
                    }
                }
            }

            AppendMaskFaces(
                negativeMask,
                faces,
                (x, z, widthSpan, depthSpan, argb) => new ImportedVoxelFaceTemplate(
                    new Vector3(x, y, z + depthSpan),
                    new Vector3(x + widthSpan, y, z + depthSpan),
                    new Vector3(x + widthSpan, y, z),
                    new Vector3(x, y, z),
                    new Int3(0, -1, 0),
                    Color.FromArgb(argb)));

            AppendMaskFaces(
                positiveMask,
                faces,
                (x, z, widthSpan, depthSpan, argb) => new ImportedVoxelFaceTemplate(
                    new Vector3(x, y + 1, z),
                    new Vector3(x + widthSpan, y + 1, z),
                    new Vector3(x + widthSpan, y + 1, z + depthSpan),
                    new Vector3(x, y + 1, z + depthSpan),
                    new Int3(0, 1, 0),
                    Color.FromArgb(argb)));
        }

        for (var z = 0; z < depth; z++)
        {
            var negativeMask = new int[width, height];
            var positiveMask = new int[width, height];
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    var color = colors[x, y, z];
                    if (color == 0)
                    {
                        continue;
                    }

                    if (z == 0 || colors[x, y, z - 1] == 0)
                    {
                        negativeMask[x, y] = color;
                    }

                    if (z == depth - 1 || colors[x, y, z + 1] == 0)
                    {
                        positiveMask[x, y] = color;
                    }
                }
            }

            AppendMaskFaces(
                negativeMask,
                faces,
                (x, y, widthSpan, heightSpan, argb) => new ImportedVoxelFaceTemplate(
                    new Vector3(x, y, z),
                    new Vector3(x, y + heightSpan, z),
                    new Vector3(x + widthSpan, y + heightSpan, z),
                    new Vector3(x + widthSpan, y, z),
                    new Int3(0, 0, -1),
                    Color.FromArgb(argb)));

            AppendMaskFaces(
                positiveMask,
                faces,
                (x, y, widthSpan, heightSpan, argb) => new ImportedVoxelFaceTemplate(
                    new Vector3(x + widthSpan, y, z + 1),
                    new Vector3(x + widthSpan, y + heightSpan, z + 1),
                    new Vector3(x, y + heightSpan, z + 1),
                    new Vector3(x, y, z + 1),
                    new Int3(0, 0, 1),
                    Color.FromArgb(argb)));
        }

        return faces;
    }

    private static void AppendMaskFaces(
        int[,] mask,
        List<ImportedVoxelFaceTemplate> faces,
        Func<int, int, int, int, int, ImportedVoxelFaceTemplate> createFace)
    {
        var firstLength = mask.GetLength(0);
        var secondLength = mask.GetLength(1);
        var consumed = new bool[firstLength, secondLength];

        for (var first = 0; first < firstLength; first++)
        {
            for (var second = 0; second < secondLength; second++)
            {
                var color = mask[first, second];
                if (color == 0 || consumed[first, second])
                {
                    continue;
                }

                var spanSecond = 1;
                while (second + spanSecond < secondLength
                    && !consumed[first, second + spanSecond]
                    && mask[first, second + spanSecond] == color)
                {
                    spanSecond++;
                }

                var spanFirst = 1;
                while (first + spanFirst < firstLength)
                {
                    var canExtend = true;
                    for (var offsetSecond = 0; offsetSecond < spanSecond; offsetSecond++)
                    {
                        if (consumed[first + spanFirst, second + offsetSecond]
                            || mask[first + spanFirst, second + offsetSecond] != color)
                        {
                            canExtend = false;
                            break;
                        }
                    }

                    if (!canExtend)
                    {
                        break;
                    }

                    spanFirst++;
                }

                for (var offsetFirst = 0; offsetFirst < spanFirst; offsetFirst++)
                {
                    for (var offsetSecond = 0; offsetSecond < spanSecond; offsetSecond++)
                    {
                        consumed[first + offsetFirst, second + offsetSecond] = true;
                    }
                }

                faces.Add(createFace(first, second, spanFirst, spanSecond, color));
            }
        }
    }
}
