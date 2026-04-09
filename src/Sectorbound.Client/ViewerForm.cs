using System.Drawing.Drawing2D;
using Sectorbound.Engine;

namespace Sectorbound.Client;

public sealed class ViewerForm : Form
{
    private const string SaveSlotId = "sectorbound";
    private const int VisibleColumns = 32;
    private const int VisibleRows = 49;
    private const int MaxWorldY = 48;
    private const int CellSize = 16;
    private const int SidebarWidth = 360;

    private readonly ContentDefs _content;
    private readonly string _saveRoot;
    private readonly string _savePath;
    private readonly Label _titleLabel;
    private readonly Label _statusLabel;
    private readonly Label _hoverLabel;
    private readonly Label _controlsLabel;
    private readonly ListBox _eventLog;
    private readonly WorldViewport _viewport;

    private GameSession _session;
    private int _viewMinX = -8;
    private int _sliceZ;
    private Point? _hoveredCell;

    public ViewerForm()
    {
        var contentRoot = Path.Combine(AppContext.BaseDirectory, "Content");
        _content = ContentLoader.LoadFromDirectory(contentRoot);
        _saveRoot = SectorboundUserData.GetSaveRoot();
        _savePath = GameSession.GetSavePath(_saveRoot, SaveSlotId);
        _session = File.Exists(_savePath)
            ? GameSession.Load(_content, _saveRoot, SaveSlotId)
            : GameSession.CreateNew(_content, _saveRoot, SaveSlotId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        Text = "Sectorbound Viewer";
        ClientSize = new Size((VisibleColumns * CellSize) + SidebarWidth + 32, (VisibleRows * CellSize) + 32);
        MinimumSize = new Size(1180, 880);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(15, 19, 27);
        KeyPreview = true;

        _titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 70,
            ForeColor = Color.FromArgb(244, 236, 215),
            Font = new Font("Segoe UI Semibold", 18f),
            Padding = new Padding(16, 14, 16, 6),
            Text = "Sectorbound Debug Client"
        };

        _statusLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 235,
            ForeColor = Color.FromArgb(210, 220, 235),
            Font = new Font("Consolas", 10.5f),
            Padding = new Padding(16, 0, 16, 0)
        };

        _hoverLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 52,
            ForeColor = Color.FromArgb(255, 218, 133),
            Font = new Font("Segoe UI", 10f),
            Padding = new Padding(16, 0, 16, 4)
        };

        _controlsLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 180,
            ForeColor = Color.FromArgb(170, 184, 205),
            Font = new Font("Segoe UI", 9.5f),
            Padding = new Padding(16, 0, 16, 6),
            Text = string.Join(Environment.NewLine, [
                "Controls",
                "Left click: mine block",
                "Right click: place shelter block",
                "1 / 2 / 3: switch planet",
                "Q / E: move slice Z",
                "Left / Right: pan X",
                "R: craft shelter blocks",
                "T: craft workbench",
                "F: fight local enemy",
                "B: fight sector boss",
                "N: next sector",
                "S: save, L: reload save"
            ])
        };

        _eventLog = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(23, 29, 40),
            ForeColor = Color.FromArgb(224, 231, 240),
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 10f),
            IntegralHeight = false
        };

        var sidebar = new Panel
        {
            Dock = DockStyle.Right,
            Width = SidebarWidth,
            BackColor = Color.FromArgb(24, 30, 43)
        };
        sidebar.Controls.Add(_eventLog);
        sidebar.Controls.Add(_controlsLabel);
        sidebar.Controls.Add(_hoverLabel);
        sidebar.Controls.Add(_statusLabel);
        sidebar.Controls.Add(_titleLabel);

        _viewport = new WorldViewport
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(10, 13, 20)
        };
        _viewport.Paint += OnViewportPaint;
        _viewport.MouseMove += OnViewportMouseMove;
        _viewport.MouseLeave += (_, _) =>
        {
            _hoveredCell = null;
            UpdateHoverText();
            _viewport.Invalidate();
        };
        _viewport.MouseDown += OnViewportMouseDown;

        Controls.Add(_viewport);
        Controls.Add(sidebar);
        KeyDown += OnViewerKeyDown;

        Log(File.Exists(_savePath) ? "Loaded existing save slot." : "Created new shared save slot.");
        UpdateStatusText();
    }

    private void OnViewportPaint(object? sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.None;
        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;

        using var backgroundBrush = new SolidBrush(Color.FromArgb(8, 11, 18));
        e.Graphics.FillRectangle(backgroundBrush, _viewport.ClientRectangle);

        for (var row = 0; row < VisibleRows; row++)
        {
            var worldY = MaxWorldY - row;
            for (var column = 0; column < VisibleColumns; column++)
            {
                var worldX = _viewMinX + column;
                var worldPosition = new Int3(worldX, worldY, _sliceZ);
                var materialId = _session.GetMaterialIdAtCurrentPlanet(worldPosition);
                using var brush = new SolidBrush(GetMaterialColor(materialId));
                e.Graphics.FillRectangle(brush, column * CellSize, row * CellSize, CellSize - 1, CellSize - 1);
            }
        }

        DrawSurfaceContour(e.Graphics);
        DrawHoveredCell(e.Graphics);
    }

    private void DrawSurfaceContour(Graphics graphics)
    {
        using var pen = new Pen(Color.FromArgb(255, 244, 236, 215), 2f);
        for (var column = 0; column < VisibleColumns; column++)
        {
            var worldX = _viewMinX + column;
            var surfaceY = _session.GetSurfaceHeight(worldX, _sliceZ);
            var surfaceRow = MaxWorldY - surfaceY;
            graphics.DrawRectangle(pen, column * CellSize, surfaceRow * CellSize, CellSize - 1, CellSize - 1);
        }
    }

    private void DrawHoveredCell(Graphics graphics)
    {
        if (_hoveredCell is null)
        {
            return;
        }

        using var pen = new Pen(Color.FromArgb(255, 255, 218, 133), 2f);
        graphics.DrawRectangle(
            pen,
            _hoveredCell.Value.X * CellSize,
            _hoveredCell.Value.Y * CellSize,
            CellSize - 1,
            CellSize - 1);
    }

    private void OnViewportMouseMove(object? sender, MouseEventArgs e)
    {
        var column = e.X / CellSize;
        var row = e.Y / CellSize;
        if (column < 0 || column >= VisibleColumns || row < 0 || row >= VisibleRows)
        {
            _hoveredCell = null;
        }
        else
        {
            _hoveredCell = new Point(column, row);
        }

        UpdateHoverText();
        _viewport.Invalidate();
    }

    private void OnViewportMouseDown(object? sender, MouseEventArgs e)
    {
        if (_hoveredCell is null)
        {
            return;
        }

        var worldPosition = ToWorldPosition(_hoveredCell.Value);
        if (e.Button == MouseButtons.Left)
        {
            Log(_session.Mine(worldPosition).Message);
        }
        else if (e.Button == MouseButtons.Right)
        {
            Log(_session.PlaceBlock(worldPosition, "shelter-block").Message);
        }

        UpdateStatusText();
        _viewport.Invalidate();
    }

    private void OnViewerKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.D1:
            case Keys.NumPad1:
                SwitchPlanet(0);
                break;
            case Keys.D2:
            case Keys.NumPad2:
                SwitchPlanet(1);
                break;
            case Keys.D3:
            case Keys.NumPad3:
                SwitchPlanet(2);
                break;
            case Keys.Left:
                _viewMinX -= 2;
                break;
            case Keys.Right:
                _viewMinX += 2;
                break;
            case Keys.Q:
                _sliceZ -= 1;
                break;
            case Keys.E:
                _sliceZ += 1;
                break;
            case Keys.R:
                Log(_session.Craft("shelter-block").Message);
                break;
            case Keys.T:
                Log(_session.Craft("workbench-block").Message);
                break;
            case Keys.F:
                Log(_session.FightEnemy(_session.CurrentPlanet.SpawnTables.First(), CombatApproach.Blade).Message);
                break;
            case Keys.B:
                Log(_session.FightSectorBoss(CombatApproach.Sigil).Message);
                break;
            case Keys.N:
                Log(_session.AdvanceToNextSector().Message);
                _sliceZ = 0;
                _viewMinX = -8;
                break;
            case Keys.S:
                _session.Save();
                Log($"Saved to {_savePath}");
                break;
            case Keys.L:
                _session = GameSession.Load(_content, _saveRoot, SaveSlotId);
                Log("Reloaded shared save.");
                break;
            default:
                return;
        }

        ClampViewer();
        UpdateStatusText();
        UpdateHoverText();
        _viewport.Invalidate();
    }

    private void SwitchPlanet(int planetIndex)
    {
        _session.TravelToPlanet(planetIndex);
        Log($"Travelled to {_session.CurrentPlanet.DisplayName}.");
    }

    private void ClampViewer()
    {
        _sliceZ = Math.Clamp(_sliceZ, -64, 64);
        _viewMinX = Math.Clamp(_viewMinX, -64, 96);
    }

    private void UpdateStatusText()
    {
        var planets = string.Join(
            Environment.NewLine,
            _session.GetPlanetSummaries().Select(x => $"[{x.PlanetIndex}] {x.Name} ({x.BiomeName})"));

        _statusLabel.Text = string.Join(Environment.NewLine, [
            $"Sector: {_session.CurrentSectorIndex}",
            $"Planet: {_session.CurrentPlanetIndex} | {_session.CurrentPlanet.DisplayName}",
            $"Slice Z: {_sliceZ} | View X: {_viewMinX}..{_viewMinX + VisibleColumns - 1}",
            $"Difficulty: {_session.CurrentDifficulty.DisplayName}",
            string.Empty,
            "Planets",
            planets,
            string.Empty,
            $"Inventory: {_session.DescribeInventory()}",
            $"Skills: {_session.DescribeSkills()}"
        ]);
    }

    private void UpdateHoverText()
    {
        if (_hoveredCell is null)
        {
            _hoverLabel.Text = "Hover a block to inspect it.";
            return;
        }

        var world = ToWorldPosition(_hoveredCell.Value);
        var material = _content.GetMaterial(_session.GetMaterialIdAtCurrentPlanet(world));
        _hoverLabel.Text = $"Hover: {world} -> {material.DisplayName} ({material.Id})";
    }

    private Int3 ToWorldPosition(Point cell) => new(_viewMinX + cell.X, MaxWorldY - cell.Y, _sliceZ);

    private void Log(string message)
    {
        _eventLog.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
        while (_eventLog.Items.Count > 14)
        {
            _eventLog.Items.RemoveAt(_eventLog.Items.Count - 1);
        }
    }

    private static Color GetMaterialColor(string materialId) =>
        materialId switch
        {
            "air" => Color.FromArgb(13, 17, 26),
            "rootsoil" => Color.FromArgb(101, 78, 49),
            "loomstone" => Color.FromArgb(127, 132, 146),
            "cinderclay" => Color.FromArgb(156, 83, 55),
            "emberstone" => Color.FromArgb(223, 120, 72),
            "crystal-silt" => Color.FromArgb(112, 139, 173),
            "shardstone" => Color.FromArgb(167, 210, 238),
            "shelter-block" => Color.FromArgb(214, 183, 132),
            "workbench-block" => Color.FromArgb(132, 104, 71),
            _ => Color.Magenta
        };

    private sealed class WorldViewport : Panel
    {
        public WorldViewport()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }
}
