using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Numerics;

namespace Sectorbound.Client;

internal sealed class CharacterPreviewControl : Control
{
    private ImportedVoxelAssetDefinition? _asset;

    public CharacterPreviewControl()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.FromArgb(16, 21, 31);
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ImportedVoxelAssetDefinition? Asset
    {
        get => _asset;
        set
        {
            _asset = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        e.Graphics.Clear(BackColor);

        using var borderPen = new Pen(Color.FromArgb(44, 58, 82));
        e.Graphics.DrawRectangle(borderPen, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));

        if (_asset is null)
        {
            using var textBrush = new SolidBrush(Color.FromArgb(156, 171, 196));
            using var font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point);
            var text = "Preview unavailable";
            var textSize = e.Graphics.MeasureString(text, font);
            e.Graphics.DrawString(
                text,
                font,
                textBrush,
                (Width - textSize.Width) * 0.5f,
                (Height - textSize.Height) * 0.5f);
            return;
        }

        DrawGrid(e.Graphics);
        DrawAsset(e.Graphics, _asset);
    }

    private void DrawGrid(Graphics graphics)
    {
        using var gridPen = new Pen(Color.FromArgb(24, 34, 52));
        const int spacing = 28;
        for (var x = spacing; x < Width; x += spacing)
        {
            graphics.DrawLine(gridPen, x, 0, x, Height);
        }

        for (var y = spacing; y < Height; y += spacing)
        {
            graphics.DrawLine(gridPen, 0, y, Width, y);
        }
    }

    private void DrawAsset(Graphics graphics, ImportedVoxelAssetDefinition asset)
    {
        var center = new Vector3(asset.WidthCubes * 0.5f, asset.HeightCubes * 0.5f, asset.DepthCubes * 0.5f);
        var rotation = Matrix4x4.CreateRotationY(-0.72f) * Matrix4x4.CreateRotationX(0.28f);
        var transformedVertices = new List<Vector3>(asset.Faces.Count * 4);

        foreach (var face in asset.Faces)
        {
            transformedVertices.Add(Vector3.Transform(face.A - center, rotation));
            transformedVertices.Add(Vector3.Transform(face.B - center, rotation));
            transformedVertices.Add(Vector3.Transform(face.C - center, rotation));
            transformedVertices.Add(Vector3.Transform(face.D - center, rotation));
        }

        var minX = transformedVertices.Min(vertex => vertex.X);
        var maxX = transformedVertices.Max(vertex => vertex.X);
        var minY = transformedVertices.Min(vertex => vertex.Y);
        var maxY = transformedVertices.Max(vertex => vertex.Y);
        var width = Math.Max(1f, maxX - minX);
        var height = Math.Max(1f, maxY - minY);
        var scale = MathF.Min((ClientSize.Width - 56f) / width, (ClientSize.Height - 56f) / height);
        var screenCenter = new PointF(ClientSize.Width * 0.5f, ClientSize.Height * 0.56f);

        var projected = new List<(PointF[] Points, float Depth, Color Color)>(asset.Faces.Count);
        foreach (var face in asset.Faces)
        {
            var transformedA = Vector3.Transform(face.A - center, rotation);
            var transformedB = Vector3.Transform(face.B - center, rotation);
            var transformedC = Vector3.Transform(face.C - center, rotation);
            var transformedD = Vector3.Transform(face.D - center, rotation);
            var transformedNormal = Vector3.Normalize(Vector3.TransformNormal(new Vector3(face.Normal.X, face.Normal.Y, face.Normal.Z), rotation));
            var color = ApplyLighting(face.Color, transformedNormal);

            projected.Add((
                [
                    ToScreen(transformedA, scale, screenCenter),
                    ToScreen(transformedB, scale, screenCenter),
                    ToScreen(transformedC, scale, screenCenter),
                    ToScreen(transformedD, scale, screenCenter)
                ],
                (transformedA.Z + transformedB.Z + transformedC.Z + transformedD.Z) * 0.25f,
                color));
        }

        foreach (var polygon in projected.OrderBy(item => item.Depth))
        {
            using var brush = new SolidBrush(polygon.Color);
            graphics.FillPolygon(brush, polygon.Points);
        }
    }

    private static PointF ToScreen(Vector3 point, float scale, PointF center) =>
        new(
            center.X + (point.X * scale),
            center.Y - (point.Y * scale));

    private static Color ApplyLighting(Color color, Vector3 normal)
    {
        var lightDirection = Vector3.Normalize(new Vector3(-0.45f, 0.85f, -0.25f));
        var diffuse = MathF.Max(0f, Vector3.Dot(normal, lightDirection));
        var brightness = Math.Clamp(0.45f + (diffuse * 0.5f), 0.2f, 1.15f);
        return Color.FromArgb(
            color.A,
            Math.Clamp((int)(color.R * brightness), 0, 255),
            Math.Clamp((int)(color.G * brightness), 0, 255),
            Math.Clamp((int)(color.B * brightness), 0, 255));
    }
}
