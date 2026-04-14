using System.Drawing.Drawing2D;
using System.Numerics;
using Sectorbound.Engine;

namespace Sectorbound.Client;

public sealed partial class RuntimeForm : Form
{
    private const string SaveSlotId = "sectorbound";
    private const int SidebarWidth = 360;

    private int VisibleChunkRadius =>
        (int)Math.Ceiling(_settings.ViewDistanceBlocks / _session.ChunkSizeInOriginalBlocks);

    private readonly ContentDefs _content;
    private readonly string _saveRoot;
    private readonly string _savePath;
    private readonly ClientSettings _settings;
    private readonly string _settingsPath;
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
    private readonly List<RuntimeParticle> _particles = [];
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
    private List<ChunkColumnKey> _prewarmQueue = [];
    private int _prewarmIndex;
    private bool _isPrewarming;
    private bool _isBuildingPrewarmChunk;
    private ChunkColumnKey? _prewarmActiveChunk;
    private int _prewarmActiveChunkIndex = -1;
    private int _prewarmActiveSliceBatchIndex;
    private int _prewarmActiveSliceBatchTotal;
    private bool _wasPlayerInLiquid;
    private bool _wasPlayerSubmerged;
    private string _lastPlayerMediumId = "air";
    private float _liquidWakeAccumulator;
    private const int PrewarmChunksPerFrame = 4;
    private const int MaxParticles = 320;

    public RuntimeForm(bool newGame = false, string? newGameDifficultyPresetId = null)
    {
        var contentRoot = Path.Combine(AppContext.BaseDirectory, "Content");
        _content = ContentLoader.LoadFromDirectory(contentRoot);
        _saveRoot = SectorboundUserData.GetSaveRoot();
        _savePath = GameSession.GetSavePath(_saveRoot, SaveSlotId);
        _settingsPath = ClientSettings.GetDefaultPath();
        _settings = ClientSettings.Load(_settingsPath);
        if (!File.Exists(_settingsPath))
        {
            _settings.TrySave(_settingsPath);
        }
        bool loadSave = !newGame && File.Exists(_savePath);
        var difficultyPresetId = ClientSettings.NormalizeDifficultyPresetId(newGameDifficultyPresetId);
        _session = loadSave
            ? GameSession.Load(_content, _saveRoot, SaveSlotId)
            : GameSession.CreateNew(
                _content,
                _saveRoot,
                SaveSlotId,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                difficultyPresetId);
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
            Height = 245,
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
                "I: toggle invert Y look",
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
        StartPrewarm();
        UpdateStatusText(force: true);
        UpdateTargetLabel();
        Log(loadSave ? "Loaded existing local save." : "Created new local save.");
        Log($"Mouse look Y: {DescribeMouseLookMode()}. Press I to toggle.");

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
        if (_isPrewarming)
        {
            TickPrewarm();
            return;
        }

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
        var previousPlayerWorldPosition = _player.Position * _session.VoxelRenderScale;
        _player.Update(
            _session,
            worldMove,
            facingAxis,
            _camera.ForwardOnGround,
            _camera.RightOnGround,
            _input.IsDown(Keys.ShiftKey),
            deltaSeconds);
        UpdatePlayerEffects(previousPlayerWorldPosition, deltaSeconds);
        UpdateParticles(deltaSeconds);

        var viewportSize = _backBufferSize.Width > 0 ? _backBufferSize : _viewport.ClientSize;
        _camera.Update(_player.Position * _session.VoxelRenderScale, viewportSize, deltaSeconds);
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
        DrawParticles(graphics);
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

    private void UpdatePlayerEffects(Vector3 previousPlayerWorldPosition, float deltaSeconds)
    {
        var currentPlayerWorldPosition = _player.Position * _session.VoxelRenderScale;
        if (!_wasPlayerInLiquid && _player.IsInLiquid)
        {
            SpawnLiquidBurst(currentPlayerWorldPosition + new Vector3(0f, 0.08f, 0f), _player.CurrentMediumId, 1f + _player.LiquidImmersion);
        }
        else if (_wasPlayerInLiquid && !_player.IsInLiquid)
        {
            SpawnLiquidBurst(currentPlayerWorldPosition + new Vector3(0f, 0.06f, 0f), _lastPlayerMediumId, 0.8f);
        }

        var horizontalDistance = new Vector2(
            currentPlayerWorldPosition.X - previousPlayerWorldPosition.X,
            currentPlayerWorldPosition.Z - previousPlayerWorldPosition.Z).Length();
        if (_player.IsInLiquid)
        {
            _liquidWakeAccumulator += horizontalDistance * (0.35f + (_player.LiquidImmersion * 0.8f));
            if (_liquidWakeAccumulator >= 0.10f)
            {
                SpawnLiquidWake(currentPlayerWorldPosition, _player.CurrentMediumId, _player.LiquidImmersion);
                _liquidWakeAccumulator = 0f;
            }

            if (_player.IsSubmerged && !_wasPlayerSubmerged)
            {
                SpawnBubbleTrail(currentPlayerWorldPosition, _player.CurrentMediumId, 4);
            }
        }
        else
        {
            _liquidWakeAccumulator = 0f;
        }

        _wasPlayerInLiquid = _player.IsInLiquid;
        _wasPlayerSubmerged = _player.IsSubmerged;
        _lastPlayerMediumId = _player.CurrentMediumId;
    }

    private void UpdateParticles(float deltaSeconds)
    {
        for (var index = _particles.Count - 1; index >= 0; index--)
        {
            var particle = _particles[index];
            particle.Age += deltaSeconds;
            if (particle.Age >= particle.Lifetime)
            {
                _particles.RemoveAt(index);
                continue;
            }

            particle.Velocity.Y -= particle.Gravity * deltaSeconds;
            var drag = Math.Clamp(1f - (particle.Drag * deltaSeconds), 0.15f, 1f);
            particle.Velocity *= drag;
            particle.Position += particle.Velocity * deltaSeconds;

            var particleVoxel = ToVoxelPosition(particle.Position);
            var material = _session.InspectMaterialAtCurrentPlanet(particleVoxel);
            if (material.IsSolid)
            {
                _particles.RemoveAt(index);
                continue;
            }

            if (IsLiquidMaterial(material))
            {
                particle.Velocity *= 0.88f;
            }

            _particles[index] = particle;
        }
    }

    private void DrawParticles(Graphics graphics)
    {
        if (_particles.Count == 0)
        {
            return;
        }

        foreach (var particle in _particles.OrderByDescending(x => Vector3.DistanceSquared(x.Position, _camera.Position)))
        {
            if (!TryProject(particle.Position, out var screen))
            {
                continue;
            }

            var lifeT = particle.Age / particle.Lifetime;
            var fade = MathF.Sin((1f - lifeT) * MathF.PI * 0.5f);
            var depth = MathF.Sqrt(Vector3.DistanceSquared(particle.Position, _camera.Position));
            var radius = Math.Clamp((particle.Size / MathF.Max(0.35f, depth)) * 105f, 1.5f, 16f);
            var color = Color.FromArgb(
                Math.Clamp((int)(particle.Color.A * fade), 0, 255),
                particle.Color.R,
                particle.Color.G,
                particle.Color.B);
            using var brush = new SolidBrush(color);
            graphics.FillEllipse(brush, screen.X - radius, screen.Y - radius, radius * 2f, radius * 2f);

            using var highlightBrush = new SolidBrush(Color.FromArgb(Math.Clamp(color.A / 2, 0, 255), 255, 255, 255));
            graphics.FillEllipse(highlightBrush, screen.X - (radius * 0.35f), screen.Y - (radius * 0.35f), radius * 0.7f, radius * 0.7f);
        }
    }

    private void SpawnLiquidBurst(Vector3 worldPosition, string materialId, float intensity)
    {
        var color = GetParticleColor(materialId);
        var count = 6 + (int)MathF.Round(intensity * 4f);
        for (var index = 0; index < count; index++)
        {
            var angle = Random.Shared.NextSingle() * MathF.PI * 2f;
            var speed = 0.35f + (Random.Shared.NextSingle() * 0.45f * intensity);
            AddParticle(
                worldPosition + new Vector3((Random.Shared.NextSingle() - 0.5f) * 0.18f, 0f, (Random.Shared.NextSingle() - 0.5f) * 0.18f),
                new Vector3(MathF.Cos(angle) * speed, 0.35f + (Random.Shared.NextSingle() * 0.4f * intensity), MathF.Sin(angle) * speed),
                color,
                size: 0.06f + (Random.Shared.NextSingle() * 0.03f),
                lifetime: 0.45f + (Random.Shared.NextSingle() * 0.20f),
                drag: 1.6f,
                gravity: 1.8f);
        }
    }

    private void SpawnLiquidWake(Vector3 worldPosition, string materialId, float immersion)
    {
        var color = GetParticleColor(materialId);
        var count = immersion >= 0.66f ? 3 : 2;
        for (var index = 0; index < count; index++)
        {
            var angle = Random.Shared.NextSingle() * MathF.PI * 2f;
            var speed = 0.08f + (Random.Shared.NextSingle() * 0.12f);
            AddParticle(
                worldPosition + new Vector3((Random.Shared.NextSingle() - 0.5f) * 0.22f, 0.02f, (Random.Shared.NextSingle() - 0.5f) * 0.22f),
                new Vector3(MathF.Cos(angle) * speed, 0.06f + (Random.Shared.NextSingle() * 0.08f), MathF.Sin(angle) * speed),
                color,
                size: 0.05f + (Random.Shared.NextSingle() * 0.02f),
                lifetime: 0.28f + (Random.Shared.NextSingle() * 0.18f),
                drag: 2.8f,
                gravity: 0.9f);
        }
    }

    private void SpawnBubbleTrail(Vector3 worldPosition, string materialId, int count)
    {
        var color = Lighten(GetParticleColor(materialId), 0.30f);
        for (var index = 0; index < count; index++)
        {
            AddParticle(
                worldPosition + new Vector3((Random.Shared.NextSingle() - 0.5f) * 0.14f, -0.12f + (index * 0.03f), (Random.Shared.NextSingle() - 0.5f) * 0.14f),
                new Vector3((Random.Shared.NextSingle() - 0.5f) * 0.04f, 0.10f + (Random.Shared.NextSingle() * 0.10f), (Random.Shared.NextSingle() - 0.5f) * 0.04f),
                color,
                size: 0.035f + (Random.Shared.NextSingle() * 0.015f),
                lifetime: 0.55f + (Random.Shared.NextSingle() * 0.18f),
                drag: 1.3f,
                gravity: -0.18f);
        }
    }

    private void SpawnDebrisBurst(Vector3 worldPosition, Int3 normal, string materialId, int count, float speedScale)
    {
        var baseColor = GetParticleColor(materialId);
        var outwardVector = new Vector3(normal.X, normal.Y, normal.Z);
        if (outwardVector.LengthSquared() < 0.001f)
        {
            outwardVector = Vector3.UnitY;
        }

        var outward = Vector3.Normalize(outwardVector + new Vector3(0f, 0.5f, 0f));
        for (var index = 0; index < count; index++)
        {
            var jitter = new Vector3(
                (Random.Shared.NextSingle() - 0.5f) * 0.55f,
                Random.Shared.NextSingle() * 0.55f,
                (Random.Shared.NextSingle() - 0.5f) * 0.55f);
            var velocity = (outward * (0.25f + (Random.Shared.NextSingle() * 0.30f * speedScale))) + (jitter * 0.25f);
            AddParticle(
                worldPosition + new Vector3(normal.X, normal.Y, normal.Z) * 0.04f,
                velocity,
                Shift(baseColor, Random.Shared.Next(-10, 11), Random.Shared.Next(-10, 11), Random.Shared.Next(-10, 11)),
                size: 0.04f + (Random.Shared.NextSingle() * 0.02f),
                lifetime: 0.28f + (Random.Shared.NextSingle() * 0.16f),
                drag: 2.1f,
                gravity: 2.2f);
        }
    }

    private void AddParticle(Vector3 position, Vector3 velocity, Color color, float size, float lifetime, float drag, float gravity)
    {
        if (_particles.Count >= MaxParticles)
        {
            _particles.RemoveAt(0);
        }

        _particles.Add(new RuntimeParticle(position, velocity, color, size, lifetime, drag, gravity));
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
            $"Medium: {DescribeCurrentMedium()}",
            $"Standing voxel: {standingVoxel}",
            $"Skills: {_session.DescribeSkills()}",
            $"Approach: {FormatCombatApproach(_selectedCombatApproach)} | Look Y: {DescribeMouseLookMode()}",
            $"Build: {DescribeSelectedBuild()}",
            $"Recipe: {GetSelectedRecipe().DisplayName}",
            $"Slice goals: {sliceProgress.Count(x => x.IsComplete)}/{sliceProgress.Count}",
            $"Target: {DescribeCurrentTarget()}",
            $"Inventory: {_session.DescribeInventory()}"
        ]);

        using var textBrush = new SolidBrush(Color.FromArgb(220, 228, 236));
        graphics.DrawString(overlayLines, _overlayFont, textBrush, new RectangleF(28, 58, 336, 148));
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
            $"Difficulty: {_session.CurrentDifficulty.DisplayName} | Look Y: {DescribeMouseLookMode()}",
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

    private void ToggleInvertMouseY()
    {
        _settings.InvertMouseY = !_settings.InvertMouseY;
        var saved = _settings.TrySave(_settingsPath);
        Log(saved
            ? $"Mouse look Y set to {DescribeMouseLookMode()}."
            : $"Mouse look Y set to {DescribeMouseLookMode()}, but the preference could not be saved.");
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

    private string DescribeMouseLookMode() => _settings.InvertMouseY ? "inverted" : "standard";

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

    private string DescribeCurrentMedium() =>
        _player.IsInLiquid
            ? $"{_player.CurrentMediumId} ({_player.LiquidImmersion * 100f:0}% immersed)"
            : "air";

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
        var selection = _settings.GetCharacterSelection();
        foreach (var face in GeneratedVoxelAssets.BuildPlayerFaces(selection, _player.Position * _session.VoxelRenderScale, _player.FacingDirection))
        {
            yield return face;
        }
    }

    private Vector3 GetVoxelCenter(Int3 blockPosition)
    {
        var scale = _session.VoxelRenderScale;
        return new Vector3(
            (blockPosition.X + 0.5f) * scale,
            (blockPosition.Y + 0.5f) * scale,
            (blockPosition.Z + 0.5f) * scale);
    }

    private static Vector3 GetFaceCenter(MeshFace face) => (face.A + face.B + face.C + face.D) / 4f;

    private Int3 ToVoxelPosition(Vector3 worldPosition)
    {
        var scale = _session.VoxelRenderScale;
        return new Int3(
            (int)MathF.Floor(worldPosition.X / scale),
            (int)MathF.Floor(worldPosition.Y / scale),
            (int)MathF.Floor(worldPosition.Z / scale));
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

    private static bool IsLiquidMaterial(MaterialDef material) =>
        !material.IsSolid && !string.Equals(material.Id, "air", StringComparison.OrdinalIgnoreCase);

    private static Color Darken(Color color, float amount) =>
        Color.FromArgb(
            color.A,
            Math.Clamp((int)(color.R * (1f - amount)), 0, 255),
            Math.Clamp((int)(color.G * (1f - amount)), 0, 255),
            Math.Clamp((int)(color.B * (1f - amount)), 0, 255));

    private static Color GetMaterialColor(MeshFace face)
    {
        if (face.OverrideColor is Color overrideColor)
        {
            return overrideColor;
        }

        if (!string.IsNullOrWhiteSpace(face.PaletteId))
        {
            return GetPaletteColor(face);
        }

        return face.MaterialId switch
        {
            "air" => Color.FromArgb(12, 16, 24),
            "water" => GetWaterColor(face),
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
            "ember-vein" => GetTerrainColor(
                face,
                top: Color.FromArgb(255, 128, 42),
                side: Color.FromArgb(220, 92, 24),
                accent: Color.FromArgb(255, 178, 88),
                variationStrength: 18),
            "rootwood" => GetTerrainColor(
                face,
                top: Color.FromArgb(72, 52, 36),
                side: Color.FromArgb(56, 40, 28),
                accent: Color.FromArgb(98, 72, 48),
                variationStrength: 7),
            "void-crystal" => GetTerrainColor(
                face,
                top: Color.FromArgb(118, 74, 194),
                side: Color.FromArgb(88, 52, 162),
                accent: Color.FromArgb(172, 132, 238),
                variationStrength: 16),
            "workbench-block" => Color.FromArgb(145, 105, 72),
            _ => Color.Magenta
        };
    }

    private static Color GetWaterColor(MeshFace face)
    {
        var baseColor = face.Normal.Y > 0
            ? Color.FromArgb(168, 78, 148, 214)
            : Color.FromArgb(148, 52, 112, 176);
        var noise = SampleColorNoise(face.BlockPosition, 6);
        return Shift(baseColor, noise / 3, noise / 4, noise);
    }

    private static Color GetParticleColor(string materialId) =>
        materialId switch
        {
            "water" => Color.FromArgb(192, 122, 186, 238),
            "rootsoil" => Color.FromArgb(198, 126, 114, 82),
            "loomstone" => Color.FromArgb(206, 152, 163, 176),
            "cinderclay" => Color.FromArgb(212, 199, 120, 88),
            "emberstone" => Color.FromArgb(220, 242, 156, 88),
            "crystal-silt" => Color.FromArgb(210, 148, 188, 216),
            "shardstone" => Color.FromArgb(216, 206, 236, 248),
            "ember-vein" => Color.FromArgb(228, 255, 148, 52),
            "rootwood" => Color.FromArgb(205, 88, 68, 48),
            "void-crystal" => Color.FromArgb(224, 146, 106, 220),
            "shelter-block" => Color.FromArgb(208, 222, 198, 150),
            "workbench-block" => Color.FromArgb(205, 168, 126, 86),
            _ => Color.FromArgb(200, 230, 230, 230)
        };

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

    private static Color Lighten(Color color, float amount) =>
        Color.FromArgb(
            color.A,
            Math.Clamp((int)(color.R + ((255 - color.R) * amount)), 0, 255),
            Math.Clamp((int)(color.G + ((255 - color.G) * amount)), 0, 255),
            Math.Clamp((int)(color.B + ((255 - color.B) * amount)), 0, 255));

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

        _viewport.ClearFrame();
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

        public void ClearFrame() => _currentFrame = null;

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
