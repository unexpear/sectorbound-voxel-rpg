using System.Numerics;
using Sectorbound.Engine;

namespace Sectorbound.Client;

public sealed partial class RuntimeForm
{
    private void OnViewportMouseMove(object? sender, MouseEventArgs e)
    {
        _mouseInsideViewport = true;
        _mouseViewportPosition = e.Location;
        if (_lastMousePosition is Point last)
        {
            var dx = e.Location.X - last.X;
            var dy = e.Location.Y - last.Y;
            var sensitivity = _settings.MouseSensitivity;
            var pitchDelta = _settings.InvertMouseY ? dy * sensitivity : -dy * sensitivity;
            _camera.AdjustYawPitch(dx * sensitivity, pitchDelta);
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
            case var key when ClientInputBindings.IsDecreaseMouseSensitivityKey(key):
                AdjustMouseSensitivity(-1);
                break;
            case var key when ClientInputBindings.IsIncreaseMouseSensitivityKey(key):
                AdjustMouseSensitivity(1);
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
            case Keys.I:
                ToggleInvertMouseY();
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

    private void AdjustMouseSensitivity(int direction)
    {
        var updated = ClientSettings.NormalizeMouseSensitivity(
            _settings.MouseSensitivity + (direction * ClientSettings.MouseSensitivityStep));
        if (Math.Abs(updated - _settings.MouseSensitivity) < 0.0001f)
        {
            Log($"Mouse look speed is already at {_settings.MouseSensitivity:F3}.");
            return;
        }

        _settings.MouseSensitivity = updated;
        var saved = _settings.TrySave(_settingsPath);
        Log(saved
            ? $"Mouse look speed set to {_settings.MouseSensitivity:F3}."
            : $"Mouse look speed set to {_settings.MouseSensitivity:F3}, but the preference could not be saved.");
    }

    private void MineTargetFace()
    {
        if (_targetedFace is null)
        {
            return;
        }

        var affected = _targetedFace.Face.BlockPosition;
        var face = _targetedFace.Face;
        var result = _session.Mine(affected);
        Log(result.Message);
        if (result.Success)
        {
            SpawnDebrisBurst(GetFaceCenter(face), face.Normal, result.MaterialId, count: 8, speedScale: 1f);
            _meshCache.InvalidateAround(_session, affected);
        }

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
        var materialId = GetSelectedBuildMaterialId();
        var result = _session.PlaceBlock(placement, materialId);
        Log(result.Message);
        if (result.Success)
        {
            SpawnDebrisBurst(GetVoxelCenter(placement), new Int3(-normal.X, -normal.Y, -normal.Z), materialId, count: 5, speedScale: 0.7f);
            _meshCache.InvalidateAround(_session, placement);
        }

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
        _particles.Clear();
        _liquidWakeAccumulator = 0f;
        RespawnPlayer();
        StartPrewarm();
        UpdateStatusText(force: true);
        UpdateTargetLabel();
    }

    private void RespawnPlayer()
    {
        var spawn = _session.GetRecommendedSpawnPosition();
        _player.Spawn(_session, new Vector2(spawn.X + 0.5f, spawn.Z + 0.5f));
        _wasPlayerInLiquid = _player.IsInLiquid;
        _wasPlayerSubmerged = _player.IsSubmerged;
        _lastPlayerMediumId = _player.CurrentMediumId;
        _camera.Update(_player.Position * _session.VoxelRenderScale, _viewport.ClientSize, 1f / 60f);
    }

    private void StartPrewarm()
    {
        var center = _session.GetChunkCoordinateForWorldPosition(_player.GetStandingVoxel(_session));
        var r = VisibleChunkRadius;
        var chunks = new List<(ChunkColumnKey Key, int DistSq)>((2 * r + 1) * (2 * r + 1));
        for (var dz = -r; dz <= r; dz++)
        {
            for (var dx = -r; dx <= r; dx++)
            {
                chunks.Add((new ChunkColumnKey(center.X + dx, center.Z + dz), dx * dx + dz * dz));
            }
        }

        chunks.Sort((a, b) => a.DistSq.CompareTo(b.DistSq));
        _prewarmQueue = chunks.Select(x => x.Key).ToList();
        _prewarmIndex = 0;
        _isPrewarming = _prewarmQueue.Count > 0;
        _isBuildingPrewarmChunk = false;
        _prewarmActiveChunk = _prewarmQueue.Count > 0 ? _prewarmQueue[0] : null;
        _prewarmActiveChunkIndex = _prewarmQueue.Count > 0 ? 0 : -1;
        _prewarmActiveSliceBatchIndex = 0;
        _prewarmActiveSliceBatchTotal = _prewarmQueue.Count > 0 ? ChunkSliceVolume.GetSliceBatchCount(_session) : 0;
    }

    private void TickPrewarm()
    {
        var end = Math.Min(_prewarmIndex + PrewarmChunksPerFrame, _prewarmQueue.Count);
        for (var i = _prewarmIndex; i < end; i++)
        {
            var key = _prewarmQueue[i];
            _isBuildingPrewarmChunk = true;
            _prewarmActiveChunk = key;
            _prewarmActiveChunkIndex = i;
            _prewarmActiveSliceBatchIndex = 0;
            _prewarmActiveSliceBatchTotal = ChunkSliceVolume.GetSliceBatchCount(_session);
            _meshCache.GetOrBuild(_session, key, UpdatePrewarmSliceBatchProgress);
            _isBuildingPrewarmChunk = false;
        }

        _prewarmIndex = end;
        if (_prewarmIndex >= _prewarmQueue.Count)
        {
            _isPrewarming = false;
            _prewarmActiveChunk = null;
            _prewarmActiveChunkIndex = -1;
            _prewarmActiveSliceBatchIndex = 0;
            _prewarmActiveSliceBatchTotal = 0;
        }

        DrawLoadingFrame();
    }

    private void UpdatePrewarmSliceBatchProgress(ChunkSliceBatchProgress progress)
    {
        _prewarmActiveChunk = progress.Key;
        _prewarmActiveSliceBatchIndex = progress.CompletedBatches;
        _prewarmActiveSliceBatchTotal = progress.TotalBatches;
        DrawLoadingFrame();
    }

    private void DrawLoadingFrame()
    {
        EnsureBackBuffer();
        if (_backBuffer is null || _backBufferSize.Width <= 0 || _backBufferSize.Height <= 0)
        {
            return;
        }

        using var g = Graphics.FromImage(_backBuffer);
        g.Clear(Color.FromArgb(8, 10, 16));

        var cx = _backBufferSize.Width / 2;
        var cy = _backBufferSize.Height / 2;
        var total = _prewarmQueue.Count;
        var done = _prewarmIndex;
        var activeSliceProgress = _isBuildingPrewarmChunk && _prewarmActiveSliceBatchTotal > 0
            ? _prewarmActiveSliceBatchIndex / (float)_prewarmActiveSliceBatchTotal
            : 0f;
        var overallProgress = total > 0
            ? Math.Clamp((done + activeSliceProgress) / total, 0f, 1f)
            : 1f;
        var activeChunkLabel = _prewarmActiveChunk is ChunkColumnKey chunk
            ? $"[{chunk.X}, {chunk.Z}]"
            : "--";
        var activeChunkOrdinal = _prewarmActiveChunkIndex >= 0 ? _prewarmActiveChunkIndex + 1 : Math.Min(done, total);

        const int BarWidth = 320;
        const int BarHeight = 8;
        var barX = cx - BarWidth / 2;
        var barY = cy + 20;
        var sliceBarY = barY + 34;

        using var titleBrush = new SolidBrush(Color.FromArgb(245, 238, 219));
        using var subBrush = new SolidBrush(Color.FromArgb(174, 186, 205));
        using var barBgBrush = new SolidBrush(Color.FromArgb(23, 29, 42));
        using var barFillBrush = new SolidBrush(Color.FromArgb(42, 112, 76));
        using var sliceFillBrush = new SolidBrush(Color.FromArgb(88, 138, 216));

        var titleRect = new RectangleF(cx - 200, cy - 60, 400, 40);
        using var centered = new System.Drawing.StringFormat { Alignment = System.Drawing.StringAlignment.Center };
        g.DrawString("Sectorbound", _overlayTitleFont, titleBrush, titleRect, centered);
        g.DrawString("Initializing sector...", _overlayFont, subBrush, new PointF(barX, cy - 4));
        g.FillRectangle(barBgBrush, barX, barY, BarWidth, BarHeight);
        g.FillRectangle(barFillBrush, barX, barY, (int)(BarWidth * overallProgress), BarHeight);
        g.FillRectangle(barBgBrush, barX, sliceBarY, BarWidth, BarHeight);
        g.FillRectangle(
            sliceFillBrush,
            barX,
            sliceBarY,
            _prewarmActiveSliceBatchTotal > 0 ? (int)(BarWidth * (_prewarmActiveSliceBatchIndex / (float)_prewarmActiveSliceBatchTotal)) : 0,
            BarHeight);
        g.DrawString($"Chunks: {done} / {total}", _overlayFont, subBrush, new PointF(barX, barY + 16));
        g.DrawString(
            $"Slice batches: {_prewarmActiveSliceBatchIndex} / {_prewarmActiveSliceBatchTotal} in chunk {activeChunkOrdinal}/{Math.Max(total, 1)} {activeChunkLabel}",
            _overlayFont,
            subBrush,
            new PointF(barX, sliceBarY + 16));

        _viewport.Present(_backBuffer);
    }
}
