using System.Drawing.Drawing2D;
using System.Numerics;
using Sectorbound.Engine;

namespace Sectorbound.Client;

public sealed class RuntimeForm : Form
{
    private const string SaveSlotId = "sectorbound";
    private const int SidebarWidth = 360;
    private const int VisibleChunkRadius = 1;

    private readonly ContentDefs _content;
    private readonly string _saveRoot;
    private readonly string _savePath;
    private readonly InputTracker _input = new();
    private readonly RenderLoop _renderLoop;
    private readonly ChunkMeshCache _meshCache = new();
    private readonly RuntimeCamera _camera = new();
    private readonly PlayerController _player = new();
    private readonly RuntimeViewport _viewport;
    private readonly Label _titleLabel;
    private readonly Label _statusLabel;
    private readonly Label _progressLabel;
    private readonly Label _targetLabel;
    private readonly Label _controlsLabel;
    private readonly ListBox _eventLog;
    private readonly Font _overlayFont = new("Consolas", 10f, FontStyle.Regular, GraphicsUnit.Point);
    private readonly Font _overlayTitleFont = new("Segoe UI Semibold", 16f, FontStyle.Regular, GraphicsUnit.Point);
    private readonly List<ProjectedFace> _projectedFaces = [];
    private readonly IReadOnlyList<string> _buildPalette;
    private readonly IReadOnlyList<string> _recipePalette;

    private GameSession _session;
    private Bitmap? _backBuffer;
    private Size _backBufferSize;
    private bool _mouseInsideViewport;
    private Point _mouseViewportPosition;
    private float _runtimeTime;
    private float _fps;
    private float _fpsAccumulator;
    private int _framesThisSecond;
    private float _statusAccumulator;
    private bool _showOverlay = true;
    private ProjectedFace? _targetedFace;
    private int _selectedBuildIndex;
    private int _selectedRecipeIndex;
    private CombatApproach _selectedCombatApproach = CombatApproach.Blade;
    private Point? _lastMousePosition = null;

    public RuntimeForm()
    {
        var contentRoot = Path.Combine(AppContext.BaseDirectory, "Content");
        _content = ContentLoader.LoadFromDirectory(contentRoot);
        _saveRoot = SectorboundUserData.GetSaveRoot();
        _savePath = GameSession.GetSavePath(_saveRoot, SaveSlotId);
        _session = File.Exists(_savePath)
            ? GameSession.Load(_content, _saveRoot, SaveSlotId)
            : GameSession.CreateNew(_content, _saveRoot, SaveSlotId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        _buildPalette = BuildPlaceablePalette();
        _recipePalette = _content.Recipes.Values
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Id)
            .ToArray();

        Text = "Sectorbound";
        ClientSize = new Size(1440, 940);
        MinimumSize = new Size(1280, 840);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(13, 16, 24);
        KeyPreview = true;

        _titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 74,
            ForeColor = Color.FromArgb(245, 238, 219),
            Font = new Font("Segoe UI Semibold", 18f),
            Padding = new Padding(16, 14, 16, 6),
            Text = "Sectorbound"
        };

        _statusLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 260,
            ForeColor = Color.FromArgb(212, 221, 236),
            Font = new Font("Consolas", 10.1f),
            Padding = new Padding(16, 0, 16, 0)
        };

        _progressLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 180,
            ForeColor = Color.FromArgb(188, 224, 197),
            Font = new Font("Consolas", 9.7f),
            Padding = new Padding(16, 0, 16, 6)
        };

        _targetLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 112,
            ForeColor = Color.FromArgb(255, 222, 150),
            Font = new Font("Segoe UI", 10f),
            Padding = new Padding(16, 0, 16, 6)
        };

        _controlsLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 225,
            ForeColor = Color.FromArgb(174, 186, 205),
            Font = new Font("Segoe UI", 9.5f),
            Padding = new Padding(16, 0, 16, 6),
            Text = string.Join(Environment.NewLine, [
                "Controls",
                "WASD / Arrow Keys: move relative to camera",
                "Shift: sprint",
                "Mouse move: steer third-person camera",
                "Left click / Space: mine targeted block face",
                "Right click / Enter: place the selected build block",
                "Mouse wheel or Q / E: zoom camera distance",
                "1 / 2 / 3: switch planet",
                "Z / X: cycle build block",
                "T: cycle recipe",
                "R: craft selected recipe",
                "C: cycle combat approach",
                "F: fight local enemy with selected approach",
                "B: fight sector boss with selected approach",
                "N: next sector",
                "S: save",
                "L: reload",
                "Tab: toggle overlay"
            ])
        };

        _eventLog = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(21, 27, 39),
            ForeColor = Color.FromArgb(226, 233, 241),
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 10f),
            IntegralHeight = false
        };

        var sidebar = new Panel
        {
            Dock = DockStyle.Right,
            Width = SidebarWidth,
            BackColor = Color.FromArgb(23, 29, 42)
        };
        sidebar.Controls.Add(_eventLog);
        sidebar.Controls.Add(_controlsLabel);
        sidebar.Controls.Add(_targetLabel);
        sidebar.Controls.Add(_progressLabel);
        sidebar.Controls.Add(_statusLabel);
        sidebar.Controls.Add(_titleLabel);

        _viewport = new RuntimeViewport
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(8, 10, 17),
            TabStop = true
        };
        _viewport.MouseMove += OnViewportMouseMove;
        _viewport.MouseLeave += (_, _) =>
        {
            _mouseInsideViewport = false;
            _lastMousePosition = null;
            UpdateTargetLabel();
        };
        _viewport.MouseDown += OnViewportMouseDown;
        _viewport.MouseUp += OnViewportMouseUp;
        _viewport.MouseWheel += OnViewportMouseWheel;
        _viewport.Resize += (_, _) => EnsureBackBuffer();

        Controls.Add(_viewport);
        Controls.Add(sidebar);

        KeyDown += OnRuntimeKeyDown;
        KeyUp += OnRuntimeKeyUp;

        RespawnPlayer();
        UpdateStatusText(force: true);
        UpdateTargetLabel();
        Log(File.Exists(_savePath) ? "Loaded existing local save." : "Created new local save.");

        _renderLoop = new RenderLoop(TickFrame);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _viewport.Focus();
        _renderLoop.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _renderLoop.Dispose();
        _backBuffer?.Dispose();
        _overlayFont.Dispose();
        _overlayTitleFont.Dispose();
        base.OnFormClosed(e);
    }

    private void TickFrame(TimeSpan delta)
    {
        var deltaSeconds = Math.Max(0.001f, (float)delta.TotalSeconds);
        _runtimeTime += deltaSeconds;
        _fpsAccumulator += deltaSeconds;
        _framesThisSecond += 1;
        if (_fpsAccumulator >= 1f)
        {
            _fps = _framesThisSecond / _fpsAccumulator;
            _fpsAccumulator = 0f;
            _framesThisSecond = 0;
        }

        UpdateSimulation(deltaSeconds);
        RenderFrame();

        _statusAccumulator += deltaSeconds;
        if (_statusAccumulator >= 0.15f)
        {
            UpdateStatusText(force: false);
            _statusAccumulator = 0f;
        }
    }

    private void UpdateSimulation(float deltaSeconds)
    {
        if (_input.IsDown(Keys.Q))
        {
            _camera.AdjustDistance(8f * deltaSeconds);
        }

        if (_input.IsDown(Keys.E))
        {
            _camera.AdjustDistance(-8f * deltaSeconds);
        }

        var moveAxis = _input.ReadMovement();
        var facingAxis = _input.ReadFacingMovement();
        var worldMove = (-moveAxis.Y * _camera.ForwardOnGround) + (moveAxis.X * _camera.RightOnGround);
        _player.Update(
            _session,
            worldMove,
            facingAxis,
            _camera.ForwardOnGround,
            _camera.RightOnGround,
            _input.IsDown(Keys.ShiftKey),
            deltaSeconds);

        var viewportSize = _backBufferSize.Width > 0 ? _backBufferSize : _viewport.ClientSize;
        _camera.Update(_player.Position, viewportSize, deltaSeconds);
    }

    private void RenderFrame()
    {
        EnsureBackBuffer();
        if (_backBuffer is null || _backBufferSize.Width <= 0 || _backBufferSize.Height <= 0)
        {
            return;
        }

        using var graphics = Graphics.FromImage(_backBuffer);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.Clear(Color.FromArgb(8, 10, 16));

        DrawSky(graphics);
        BuildProjectedFaces();
        DrawProjectedFaces(graphics);
        DrawTargetHighlight(graphics);
        if (_showOverlay)
        {
            DrawOverlay(graphics);
        }

        _viewport.Present(_backBuffer);
    }

    private void BuildProjectedFaces()
    {
        _projectedFaces.Clear();
        _targetedFace = null;

        var standingVoxel = _player.GetStandingVoxel(_session);
        var centerChunk = _session.GetChunkCoordinateForWorldPosition(standingVoxel);
        for (var chunkZ = centerChunk.Z - VisibleChunkRadius; chunkZ <= centerChunk.Z + VisibleChunkRadius; chunkZ++)
        {
            for (var chunkX = centerChunk.X - VisibleChunkRadius; chunkX <= centerChunk.X + VisibleChunkRadius; chunkX++)
            {
                var mesh = _meshCache.GetOrBuild(_session, new ChunkColumnKey(chunkX, chunkZ));
                foreach (var face in mesh.Faces)
                {
                    TryAddProjectedFace(face);
                }
            }
        }

        foreach (var face in BuildPlayerFaces())
        {
            TryAddProjectedFace(face);
        }

        _projectedFaces.Sort((left, right) => right.Depth.CompareTo(left.Depth));
        _targetedFace = PickTargetedFace();
        UpdateTargetLabel();
    }

    private void TryAddProjectedFace(MeshFace face)
    {
        var normal = Vector3.Normalize(new Vector3(face.Normal.X, face.Normal.Y, face.Normal.Z));
        var center = (face.A + face.B + face.C + face.D) / 4f;
        var toCamera = _camera.Position - center;
        if (Vector3.Dot(normal, toCamera) <= 0f)
        {
            return;
        }

        var screenPoints = new PointF[4];
        if (!TryProject(face.A, out screenPoints[0])
            || !TryProject(face.B, out screenPoints[1])
            || !TryProject(face.C, out screenPoints[2])
            || !TryProject(face.D, out screenPoints[3]))
        {
            return;
        }

        var bounds = GetBounds(screenPoints);
        if (!bounds.IntersectsWith(new RectangleF(0, 0, _backBufferSize.Width, _backBufferSize.Height)))
        {
            return;
        }

        var depth = Vector3.DistanceSquared(center, _camera.Position);
        _projectedFaces.Add(new ProjectedFace(face, screenPoints, bounds, depth, GetMaterialColor(face)));
    }

    private bool TryProject(Vector3 world, out PointF screen)
    {
        var clip = Vector4.Transform(new Vector4(world, 1f), _camera.ViewProjection);
        if (clip.W <= 0.01f)
        {
            screen = PointF.Empty;
            return false;
        }

        var invW = 1f / clip.W;
        var ndcX = clip.X * invW;
        var ndcY = clip.Y * invW;
        var ndcZ = clip.Z * invW;

        if (ndcZ < -0.25f || ndcZ > 1.25f)
        {
            screen = PointF.Empty;
            return false;
        }

        screen = new PointF(
            ((ndcX * 0.5f) + 0.5f) * _backBufferSize.Width,
            ((-ndcY * 0.5f) + 0.5f) * _backBufferSize.Height);
        return true;
    }

    private void DrawProjectedFaces(Graphics graphics)
    {
        foreach (var projected in _projectedFaces)
        {
            var color = ApplyLighting(projected);
            using var brush = new SolidBrush(color);
            graphics.FillPolygon(brush, projected.ScreenPoints);

            using var outlinePen = new Pen(Darken(color, 0.38f), 1f);
            graphics.DrawPolygon(outlinePen, projected.ScreenPoints);
        }
    }

    private void DrawTargetHighlight(Graphics graphics)
    {
        if (_targetedFace is null)
        {
            return;
        }

        using var outerPen = new Pen(Color.FromArgb(255, 255, 228, 149), 3f);
        using var innerPen = new Pen(Color.FromArgb(255, 255, 252, 235), 1.25f);
        graphics.DrawPolygon(outerPen, _targetedFace.ScreenPoints);
        graphics.DrawPolygon(innerPen, _targetedFace.ScreenPoints);
    }

    private void DrawSky(Graphics graphics)
    {
        using var gradient = new LinearGradientBrush(
            new Rectangle(Point.Empty, _backBufferSize),
            Color.FromArgb(16, 24, 42),
            Color.FromArgb(65, 84, 115),
            LinearGradientMode.Vertical);
        graphics.FillRectangle(gradient, 0, 0, _backBufferSize.Width, _backBufferSize.Height);

        var hazeY = (_backBufferSize.Height * 0.54f) + (MathF.Sin(_runtimeTime * 0.4f) * 8f);
        using var hazeBrush = new SolidBrush(Color.FromArgb(75, 126, 155, 186));
        graphics.FillRectangle(hazeBrush, 0, hazeY, _backBufferSize.Width, _backBufferSize.Height - hazeY);

        using var starBrush = new SolidBrush(Color.FromArgb(168, 246, 238, 216));
        for (var i = 0; i < 26; i++)
        {
            var x = PositiveWrap(((i * 131) + (_runtimeTime * (8f + (i % 4)))) % _backBufferSize.Width, _backBufferSize.Width);
            var y = 24f + ((i * 29) % 170);
            graphics.FillEllipse(starBrush, x, y, 2, 2);
        }
    }

    private void DrawOverlay(Graphics graphics)
    {
        using var panelBrush = new SolidBrush(Color.FromArgb(132, 9, 12, 18));
        graphics.FillRectangle(panelBrush, 16, 16, 364, 168);

        using var titleBrush = new SolidBrush(Color.FromArgb(245, 238, 219));
        graphics.DrawString("Sectorbound Pre-Alpha", _overlayTitleFont, titleBrush, new PointF(28, 24));

        var standingVoxel = _player.GetStandingVoxel(_session);
        var sliceProgress = _session.GetVerticalSliceProgress();
        var overlayLines = string.Join(Environment.NewLine, [
            $"FPS: {_fps:0.0}",
            $"Sector: {_session.CurrentSectorIndex} | Seed: {_session.Sector.ActiveSectorSeed}",
            $"Planet: {_session.CurrentPlanetIndex} - {_session.CurrentPlanet.DisplayName} ({_session.CurrentPlanet.BiomeRules.DisplayName})",
            $"Difficulty: {_session.CurrentDifficulty.DisplayName}",
            $"Workbench: {DescribeWorkbenchStatus()}",
            $"Player: ({_player.Position.X:0.0}, {_player.Position.Y:0.0}, {_player.Position.Z:0.0})",
            $"Standing voxel: {standingVoxel}",
            $"Skills: {_session.DescribeSkills()}",
            $"Approach: {FormatCombatApproach(_selectedCombatApproach)}",
            $"Build: {DescribeSelectedBuild()}",
            $"Recipe: {GetSelectedRecipe().DisplayName}",
            $"Slice goals: {sliceProgress.Count(x => x.IsComplete)}/{sliceProgress.Count}",
            $"Target: {DescribeCurrentTarget()}",
            $"Inventory: {_session.DescribeInventory()}"
        ]);

        using var textBrush = new SolidBrush(Color.FromArgb(220, 228, 236));
        graphics.DrawString(overlayLines, _overlayFont, textBrush, new RectangleF(28, 58, 336, 148));
    }

    private void OnViewportMouseMove(object? sender, MouseEventArgs e)
    {
        _mouseInsideViewport = true;
        _mouseViewportPosition = e.Location;
        if (_lastMousePosition is Point last)
        {
            var dx = e.Location.X - last.X;
            var dy = e.Location.Y - last.Y;
            float sensitivity = 0.012f;
            _camera.AdjustYawPitch(dx * sensitivity, -dy * sensitivity);
        }
        _lastMousePosition = e.Location;
    }

    private void OnViewportMouseDown(object? sender, MouseEventArgs e)
    {
        Focus();
        _viewport.Focus();
        if (e.Button == MouseButtons.Left)
        {
            MineTargetFace();
        }
        else if (e.Button == MouseButtons.Right)
        {
            PlaceOnTargetFace();
        }
    }

    private void OnViewportMouseUp(object? sender, MouseEventArgs e) { }


    private void OnViewportMouseWheel(object? sender, MouseEventArgs e)
    {
        _camera.AdjustDistance(Math.Sign(-e.Delta) * 1.5f);
    }

    private void OnRuntimeKeyDown(object? sender, KeyEventArgs e)
    {
        var isNewPress = _input.SetKeyDown(e.KeyCode);
        if (!isNewPress)
        {
            return;
        }

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
            case Keys.Space:
                MineTargetFace();
                break;
            case Keys.Enter:
                PlaceOnTargetFace();
                break;
            case Keys.Z:
                CycleBuildSelection(-1);
                break;
            case Keys.X:
                CycleBuildSelection(1);
                break;
            case Keys.T:
                CycleRecipeSelection(1);
                break;
            case Keys.R:
                CraftSelectedRecipe();
                break;
            case Keys.C:
                CycleCombatApproach();
                break;
            case Keys.F:
                FightCurrentEnemy();
                break;
            case Keys.B:
                FightSectorBoss();
                break;
            case Keys.N:
                Log(_session.AdvanceToNextSector().Message);
                ResetRuntimeState();
                break;
            case Keys.S:
                _session.Save();
                Log($"Saved to {_savePath}");
                break;
            case Keys.L:
                _session = GameSession.Load(_content, _saveRoot, SaveSlotId);
                ResetRuntimeState();
                Log("Reloaded local save.");
                break;
            case Keys.Tab:
                _showOverlay = !_showOverlay;
                Log(_showOverlay ? "Overlay enabled." : "Overlay hidden.");
                break;
            default:
                return;
        }

        UpdateStatusText(force: true);
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private void OnRuntimeKeyUp(object? sender, KeyEventArgs e) => _input.SetKeyUp(e.KeyCode);

    private void MineTargetFace()
    {
        if (_targetedFace is null)
        {
            return;
        }

        var affected = _targetedFace.Face.BlockPosition;
        Log(_session.Mine(affected).Message);
        _meshCache.InvalidateAround(_session, affected);
        UpdateStatusText(force: true);
    }

    private void PlaceOnTargetFace()
    {
        if (_targetedFace is null)
        {
            return;
        }

        var normal = _targetedFace.Face.Normal;
        var target = _targetedFace.Face.BlockPosition;
        var placement = new Int3(target.X + normal.X, target.Y + normal.Y, target.Z + normal.Z);
        Log(_session.PlaceBlock(placement, GetSelectedBuildMaterialId()).Message);
        _meshCache.InvalidateAround(_session, placement);
        UpdateStatusText(force: true);
    }

    private void SwitchPlanet(int planetIndex)
    {
        _session.TravelToPlanet(planetIndex);
        ResetRuntimeState();
        Log($"Travelled to {_session.CurrentPlanet.DisplayName}.");
    }

    private void ResetRuntimeState()
    {
        _meshCache.Clear();
        RespawnPlayer();
        UpdateStatusText(force: true);
        UpdateTargetLabel();
    }

    private void RespawnPlayer()
    {
        _player.Spawn(_session, new Vector2(0.5f, 0.5f));
        _camera.Update(_player.Position, _viewport.ClientSize, 1f / 60f);
    }

    private ProjectedFace? PickTargetedFace()
    {
        var point = _mouseInsideViewport
            ? _mouseViewportPosition
            : new Point(_backBufferSize.Width / 2, _backBufferSize.Height / 2);

        for (var index = _projectedFaces.Count - 1; index >= 0; index--)
        {
            var projected = _projectedFaces[index];
            if (!projected.Face.CanTarget)
            {
                continue;
            }

            if (projected.Contains(point))
            {
                return projected;
            }
        }

        return null;
    }

    private void UpdateStatusText(bool force)
    {
        if (!force && !Visible)
        {
            return;
        }

        var standingVoxel = _player.GetStandingVoxel(_session);
        var planets = string.Join(
            Environment.NewLine,
            _session.GetPlanetSummaries().Select(x => $"[{x.PlanetIndex}] {x.Name} ({x.BiomeName})"));
        var localEnemyId = _session.CurrentPlanet.SpawnTables.FirstOrDefault() ?? string.Empty;
        var localEnemyName = string.IsNullOrWhiteSpace(localEnemyId)
            ? "none"
            : _content.GetEnemy(localEnemyId).DisplayName;

        _statusLabel.Text = string.Join(Environment.NewLine, [
            "Runtime",
            $"Sector: {_session.CurrentSectorIndex}",
            $"Planet: {_session.CurrentPlanetIndex} | {_session.CurrentPlanet.DisplayName}",
            $"Encounter: {localEnemyName}",
            $"Approach: {FormatCombatApproach(_selectedCombatApproach)}",
            $"Build: {DescribeSelectedBuild()}",
            $"Recipe: {DescribeSelectedRecipe()}",
            $"Workbench: {DescribeWorkbenchStatus()}",
            $"Player voxel: {standingVoxel}",
            $"Player world: ({_player.Position.X:0.00}, {_player.Position.Y:0.00}, {_player.Position.Z:0.00})",
            $"Camera: ({_camera.Position.X:0.0}, {_camera.Position.Y:0.0}, {_camera.Position.Z:0.0})",
            $"Difficulty: {_session.CurrentDifficulty.DisplayName}",
            string.Empty,
            "Planets",
            planets,
            string.Empty,
            $"Skills: {_session.DescribeSkills()}",
            $"Perks: {_session.DescribePerks()}"
        ]);

        _progressLabel.Text = string.Join(
            Environment.NewLine,
            new[] { "Vertical Slice" }
                .Concat(_session.GetVerticalSliceProgress().Select(progress =>
                    $"{(progress.IsComplete ? "[x]" : "[ ]")} {progress.DisplayName}: {progress.Detail}")));
    }

    private void UpdateTargetLabel()
    {
        _targetLabel.Text = string.Join(Environment.NewLine, [
            _mouseInsideViewport ? "Mouse target is active." : "Targeting from screen center.",
            DescribeCurrentTarget(),
            $"Placement: {DescribeSelectedBuild()}",
            $"Recipe focus: {DescribeSelectedRecipe()}",
            $"Save slot: {_savePath}"
        ]);
    }

    private string DescribeCurrentTarget()
    {
        if (_targetedFace is null)
        {
            return "Target: none";
        }

        var block = _targetedFace.Face.BlockPosition;
        var normal = _targetedFace.Face.Normal;
        var material = _content.GetMaterial(_targetedFace.Face.MaterialId);
        var placement = new Int3(block.X + normal.X, block.Y + normal.Y, block.Z + normal.Z);
        return $"Target: {block} {material.DisplayName} | place -> {placement}";
    }

    private IReadOnlyList<string> BuildPlaceablePalette()
    {
        var recipeOutputs = _content.Recipes.Values
            .SelectMany(x => x.Outputs.Keys)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return _content.Materials.Values
            .Where(material =>
                material.IsSolid
                && !string.Equals(material.Id, "air", StringComparison.OrdinalIgnoreCase)
                && recipeOutputs.Contains(material.Id))
            .OrderBy(material => material.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(material => material.Id)
            .ToArray();
    }

    private string GetSelectedBuildMaterialId() =>
        _buildPalette.Count == 0 ? "shelter-block" : _buildPalette[_selectedBuildIndex];

    private MaterialDef GetSelectedBuildMaterial() => _content.GetMaterial(GetSelectedBuildMaterialId());

    private RecipeDef GetSelectedRecipe() =>
        _recipePalette.Count == 0
            ? _content.Recipes.Values.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase).First()
            : _content.GetRecipe(_recipePalette[_selectedRecipeIndex]);

    private void CycleBuildSelection(int delta)
    {
        if (_buildPalette.Count == 0)
        {
            return;
        }

        _selectedBuildIndex = WrapIndex(_selectedBuildIndex + delta, _buildPalette.Count);
        Log($"Selected build block: {GetSelectedBuildMaterial().DisplayName}.");
        UpdateStatusText(force: true);
        UpdateTargetLabel();
    }

    private void CycleRecipeSelection(int delta)
    {
        if (_recipePalette.Count == 0)
        {
            return;
        }

        _selectedRecipeIndex = WrapIndex(_selectedRecipeIndex + delta, _recipePalette.Count);
        Log($"Selected recipe: {GetSelectedRecipe().DisplayName}.");
        UpdateStatusText(force: true);
        UpdateTargetLabel();
    }

    private void CycleCombatApproach()
    {
        _selectedCombatApproach = _selectedCombatApproach switch
        {
            CombatApproach.Blade => CombatApproach.Bow,
            CombatApproach.Bow => CombatApproach.Sigil,
            _ => CombatApproach.Blade
        };

        Log($"Combat approach set to {FormatCombatApproach(_selectedCombatApproach)}.");
        UpdateStatusText(force: true);
    }

    private void CraftSelectedRecipe()
    {
        var recipe = GetSelectedRecipe();
        Log(_session.Craft(recipe.Id).Message);
        UpdateStatusText(force: true);
        UpdateTargetLabel();
    }

    private void FightCurrentEnemy()
    {
        var enemyId = _session.CurrentPlanet.SpawnTables.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(enemyId))
        {
            Log("No local encounter is available on this planet.");
            return;
        }

        Log(_session.FightEnemy(enemyId, _selectedCombatApproach).Message);
        UpdateStatusText(force: true);
    }

    private void FightSectorBoss()
    {
        Log(_session.FightSectorBoss(_selectedCombatApproach).Message);
        UpdateStatusText(force: true);
    }

    private string DescribeSelectedBuild()
    {
        var material = GetSelectedBuildMaterial();
        return $"{material.DisplayName} x{_session.GetInventoryCount(material.Id)}";
    }

    private string DescribeSelectedRecipe()
    {
        var recipe = GetSelectedRecipe();
        var requirements = recipe.Inputs.Count == 0
            ? "free"
            : string.Join(", ", recipe.Inputs.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).Select(x => $"{x.Value}x {x.Key}"));
        if (string.IsNullOrWhiteSpace(recipe.RequiredStationMaterialId))
        {
            return $"{recipe.DisplayName} ({requirements})";
        }

        var station = _content.GetMaterial(recipe.RequiredStationMaterialId);
        var stationStatus = _session.CurrentPlanetHasPlacedBlock(recipe.RequiredStationMaterialId) ? "ready" : "missing";
        return $"{recipe.DisplayName} ({requirements}) @ {station.DisplayName} [{stationStatus}]";
    }

    private string DescribeWorkbenchStatus() =>
        _session.CurrentPlanetHasPlacedBlock("workbench-block") ? "ready" : "missing";

    private static string FormatCombatApproach(CombatApproach approach) =>
        approach switch
        {
            CombatApproach.Blade => "Blade",
            CombatApproach.Bow => "Bow",
            CombatApproach.Sigil => "Sigil",
            _ => approach.ToString()
        };

    private IEnumerable<MeshFace> BuildPlayerFaces()
    {
        foreach (var face in GeneratedVoxelAssets.BuildPlayerFaces(_player.Position, _player.FacingDirection))
        {
            yield return face;
        }
    }

    private static RectangleF GetBounds(PointF[] points)
    {
        var minX = points.Min(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxX = points.Max(p => p.X);
        var maxY = points.Max(p => p.Y);
        return RectangleF.FromLTRB(minX, minY, maxX, maxY);
    }

    private static Color ApplyLighting(ProjectedFace projected)
    {
        var normal = Vector3.Normalize(new Vector3(
            projected.Face.Normal.X,
            projected.Face.Normal.Y,
            projected.Face.Normal.Z));
        var sunDirection = Vector3.Normalize(new Vector3(-0.55f, 1f, -0.35f));
        var diffuse = MathF.Max(0f, Vector3.Dot(normal, sunDirection));
        var ambient = 0.38f;
        var brightness = ambient + (diffuse * 0.46f);
        if (projected.Face.Normal.Y > 0)
        {
            brightness += 0.14f;
        }

        brightness = Math.Clamp(brightness, 0.15f, 1.15f);
        return Multiply(projected.BaseColor, brightness);
    }

    private static Color Multiply(Color color, float brightness) =>
        Color.FromArgb(
            color.A,
            Math.Clamp((int)(color.R * brightness), 0, 255),
            Math.Clamp((int)(color.G * brightness), 0, 255),
            Math.Clamp((int)(color.B * brightness), 0, 255));

    private static Color Darken(Color color, float amount) =>
        Color.FromArgb(
            color.A,
            Math.Clamp((int)(color.R * (1f - amount)), 0, 255),
            Math.Clamp((int)(color.G * (1f - amount)), 0, 255),
            Math.Clamp((int)(color.B * (1f - amount)), 0, 255));

    private static Color GetMaterialColor(MeshFace face)
    {
        if (!string.IsNullOrWhiteSpace(face.PaletteId))
        {
            return GetPaletteColor(face);
        }

        return face.MaterialId switch
        {
            "air" => Color.FromArgb(12, 16, 24),
            "rootsoil" => GetTerrainColor(
                face,
                top: Color.FromArgb(120, 111, 73),
                side: Color.FromArgb(98, 78, 56),
                accent: Color.FromArgb(78, 101, 69),
                variationStrength: 11),
            "loomstone" => GetTerrainColor(
                face,
                top: Color.FromArgb(151, 160, 170),
                side: Color.FromArgb(120, 128, 141),
                accent: Color.FromArgb(173, 181, 192),
                variationStrength: 9),
            "cinderclay" => GetTerrainColor(
                face,
                top: Color.FromArgb(187, 111, 76),
                side: Color.FromArgb(150, 85, 59),
                accent: Color.FromArgb(208, 141, 88),
                variationStrength: 10),
            "emberstone" => GetTerrainColor(
                face,
                top: Color.FromArgb(232, 150, 89),
                side: Color.FromArgb(201, 107, 62),
                accent: Color.FromArgb(255, 190, 116),
                variationStrength: 14),
            "crystal-silt" => GetTerrainColor(
                face,
                top: Color.FromArgb(124, 164, 191),
                side: Color.FromArgb(94, 129, 163),
                accent: Color.FromArgb(186, 220, 232),
                variationStrength: 10),
            "shardstone" => GetTerrainColor(
                face,
                top: Color.FromArgb(193, 232, 247),
                side: Color.FromArgb(146, 192, 222),
                accent: Color.FromArgb(226, 248, 255),
                variationStrength: 12),
            "shelter-block" => GetTerrainColor(
                face,
                top: Color.FromArgb(226, 205, 158),
                side: Color.FromArgb(204, 176, 128),
                accent: Color.FromArgb(241, 225, 187),
                variationStrength: 8),
            "workbench-block" => Color.FromArgb(145, 105, 72),
            _ => Color.Magenta
        };
    }

    private static Color GetTerrainColor(
        MeshFace face,
        Color top,
        Color side,
        Color accent,
        int variationStrength)
    {
        var baseColor = face.Normal.Y > 0 ? top : side;
        var noise = SampleColorNoise(face.BlockPosition, variationStrength);
        var color = Shift(baseColor, noise, noise / 2, noise);
        return ((face.BlockPosition.X + face.BlockPosition.Z + face.BlockPosition.Y) & 3) == 0
            ? Lerp(color, accent, 0.14f)
            : color;
    }

    private static Color GetPaletteColor(MeshFace face)
    {
        var baseColor = face.PaletteId switch
        {
            "bench-top" => Color.FromArgb(170, 123, 80),
            "bench-shelf" => Color.FromArgb(133, 95, 64),
            "bench-back" => Color.FromArgb(120, 88, 60),
            "bench-leg" => Color.FromArgb(100, 72, 48),
            "bench-brace" => Color.FromArgb(92, 66, 44),
            "bench-accent" => Color.FromArgb(206, 156, 93),
            "bench-metal" => Color.FromArgb(132, 141, 152),
            "bench-tool" => Color.FromArgb(84, 120, 96),
            "player-boot" => Color.FromArgb(89, 63, 47),
            "player-leg" => Color.FromArgb(86, 96, 116),
            "player-tunic" => Color.FromArgb(154, 179, 154),
            "player-belt" => Color.FromArgb(147, 102, 66),
            "player-sleeve" => Color.FromArgb(132, 159, 140),
            "player-cloak" => Color.FromArgb(108, 122, 146),
            "player-skin" => Color.FromArgb(221, 194, 166),
            "player-trim" => Color.FromArgb(196, 208, 186),
            "player-hair" => Color.FromArgb(92, 74, 52),
            "player-visor" => Color.FromArgb(72, 106, 118),
            _ => Color.Magenta
        };

        var noise = SampleColorNoise(face.BlockPosition, 8);
        return Shift(baseColor, noise, noise / 2, noise / 3);
    }

    private static int SampleColorNoise(Int3 blockPosition, int amplitude)
    {
        unchecked
        {
            var hash = (blockPosition.X * 73_856_093)
                ^ (blockPosition.Y * 19_349_663)
                ^ (blockPosition.Z * 83_492_791);
            var raw = (hash & 15) - 7;
            return (raw * amplitude) / 14;
        }
    }

    private static Color Shift(Color color, int redDelta, int greenDelta, int blueDelta) =>
        Color.FromArgb(
            color.A,
            Math.Clamp(color.R + redDelta, 0, 255),
            Math.Clamp(color.G + greenDelta, 0, 255),
            Math.Clamp(color.B + blueDelta, 0, 255));

    private static Color Lerp(Color from, Color to, float amount) =>
        Color.FromArgb(
            255,
            (int)(from.R + ((to.R - from.R) * amount)),
            (int)(from.G + ((to.G - from.G) * amount)),
            (int)(from.B + ((to.B - from.B) * amount)));

    private void EnsureBackBuffer()
    {
        var size = _viewport.ClientSize;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        if (_backBuffer is not null && _backBufferSize == size)
        {
            return;
        }

        _backBuffer?.Dispose();
        _backBuffer = new Bitmap(size.Width, size.Height);
        _backBufferSize = size;
    }

    private void Log(string message)
    {
        _eventLog.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
        while (_eventLog.Items.Count > 15)
        {
            _eventLog.Items.RemoveAt(_eventLog.Items.Count - 1);
        }
    }

    private static float PositiveWrap(float value, int modulo)
    {
        var wrapped = value % modulo;
        return wrapped < 0 ? wrapped + modulo : wrapped;
    }

    private static int WrapIndex(int value, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        var wrapped = value % count;
        return wrapped < 0 ? wrapped + count : wrapped;
    }

    private sealed class RuntimeViewport : Control
    {
        private Bitmap? _currentFrame;

        public RuntimeViewport()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            DoubleBuffered = true;
        }

        public void Present(Bitmap frame)
        {
            _currentFrame = frame;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_currentFrame is null)
            {
                base.OnPaint(e);
                return;
            }

            e.Graphics.DrawImageUnscaled(_currentFrame, 0, 0);
        }
    }
}
