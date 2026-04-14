using System.Drawing;
using System.Text;

namespace Sectorbound.Client;

internal readonly record struct VoxVoxel(byte X, byte Y, byte Z, byte ColorIndex);

internal sealed class VoxModel
{
    public VoxModel(int width, int height, int depth, IReadOnlyList<VoxVoxel> voxels, IReadOnlyList<Color> palette)
    {
        Width = width;
        Height = height;
        Depth = depth;
        Voxels = voxels;
        Palette = palette;
    }

    public int Width { get; }
    public int Height { get; }
    public int Depth { get; }
    public IReadOnlyList<VoxVoxel> Voxels { get; }
    public IReadOnlyList<Color> Palette { get; }

    public Color ResolveColor(byte paletteIndex)
    {
        if (paletteIndex == 0 || Palette.Count == 0)
        {
            return Color.Transparent;
        }

        var paletteSlot = Math.Clamp(paletteIndex - 1, 0, Palette.Count - 1);
        return Palette[paletteSlot];
    }
}

internal static class VoxFileReader
{
    public static VoxModel Load(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: false);

        var magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (!string.Equals(magic, "VOX ", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"'{path}' is not a VOX file.");
        }

        _ = reader.ReadInt32(); // version
        var mainId = Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (!string.Equals(mainId, "MAIN", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"'{path}' is missing the MAIN chunk.");
        }

        var mainContentBytes = reader.ReadInt32();
        var mainChildrenBytes = reader.ReadInt32();
        if (mainContentBytes > 0)
        {
            reader.BaseStream.Seek(mainContentBytes, SeekOrigin.Current);
        }

        var size = (Width: 0, Height: 0, Depth: 0);
        var voxels = new List<VoxVoxel>();
        var palette = new List<Color>();
        var childrenEnd = reader.BaseStream.Position + mainChildrenBytes;
        ReadChunks(reader, childrenEnd, ref size, voxels, palette);

        if (size.Width <= 0 || size.Height <= 0 || size.Depth <= 0)
        {
            throw new InvalidDataException($"'{path}' did not contain a valid SIZE chunk.");
        }

        if (palette.Count == 0)
        {
            palette.AddRange(Enumerable.Repeat(Color.White, 255));
        }

        return new VoxModel(size.Width, size.Height, size.Depth, voxels, palette);
    }

    private static void ReadChunks(
        BinaryReader reader,
        long endPosition,
        ref (int Width, int Height, int Depth) size,
        List<VoxVoxel> voxels,
        List<Color> palette)
    {
        while (reader.BaseStream.Position < endPosition)
        {
            var chunkId = Encoding.ASCII.GetString(reader.ReadBytes(4));
            var contentBytes = reader.ReadInt32();
            var childrenBytes = reader.ReadInt32();
            var contentStart = reader.BaseStream.Position;
            var contentEnd = contentStart + contentBytes;
            var childrenEnd = contentEnd + childrenBytes;

            switch (chunkId)
            {
                case "SIZE":
                    size = (reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                    break;
                case "XYZI":
                {
                    var count = reader.ReadInt32();
                    voxels.Capacity = Math.Max(voxels.Capacity, count);
                    for (var index = 0; index < count; index++)
                    {
                        voxels.Add(new VoxVoxel(
                            reader.ReadByte(),
                            reader.ReadByte(),
                            reader.ReadByte(),
                            reader.ReadByte()));
                    }

                    break;
                }
                case "RGBA":
                    palette.Clear();
                    for (var index = 0; index < 256; index++)
                    {
                        var red = reader.ReadByte();
                        var green = reader.ReadByte();
                        var blue = reader.ReadByte();
                        var alpha = reader.ReadByte();
                        if (index < 255)
                        {
                            palette.Add(Color.FromArgb(alpha, red, green, blue));
                        }
                    }

                    break;
            }

            if (reader.BaseStream.Position != contentEnd)
            {
                reader.BaseStream.Position = contentEnd;
            }

            if (childrenBytes > 0)
            {
                ReadChunks(reader, childrenEnd, ref size, voxels, palette);
            }

            if (reader.BaseStream.Position != childrenEnd)
            {
                reader.BaseStream.Position = childrenEnd;
            }
        }
    }
}
