using System.Numerics;
using Sectorbound.Engine;

namespace Sectorbound.Client;

internal sealed class PlayerController
{
    private const float BaseSpeedInOriginalBlocks = 6.25f;
    private const float SprintMultiplier = 1.6f;
    private const float MaxStepHeightInOriginalBlocks = 1.2f;
    private const float StandingOffsetInOriginalBlocks = 1.05f;
    private const float SupportProbeHeightInOriginalBlocks = 2f / 3f;
    private const float GroundSnapToleranceInOriginalBlocks = 1f / 12f;
    private const float GravityInOriginalBlocks = -22.5f;
    private const float LiquidFeetSampleHeightInOriginalBlocks = 0.18f;
    private const float LiquidTorsoSampleHeightInOriginalBlocks = 0.92f;
    private const float LiquidHeadSampleHeightInOriginalBlocks = 1.58f;

    private static readonly Vector2[] SupportSamplesInOriginalBlocks =
    [
        new Vector2(0f, 0f),
        new Vector2(0.12f, 0f),
        new Vector2(-0.12f, 0f),
        new Vector2(0f, 0.12f),
        new Vector2(0f, -0.12f)
    ];

    private readonly record struct MediumState(
        string MaterialId,
        float Immersion,
        bool FeetInLiquid,
        bool TorsoInLiquid,
        bool HeadInLiquid);

    private float _verticalVelocity;

    public Vector3 Position { get; private set; }
    public Int3 FacingDirection { get; private set; } = new(0, 0, -1);
    public bool IsGrounded { get; private set; }
    public bool IsInLiquid { get; private set; }
    public bool IsSubmerged { get; private set; }
    public float LiquidImmersion { get; private set; }
    public string CurrentMediumId { get; private set; } = "air";

    public void Spawn(GameSession session, Vector2 worldXZ)
    {
        var ground = FindStandingHeight(session, worldXZ.X, worldXZ.Y, session.MaxWorldY);
        Position = new Vector3(worldXZ.X, ground, worldXZ.Y);
        FacingDirection = new Int3(0, 0, -1);
        _verticalVelocity = 0f;
        IsGrounded = true;
        ApplyMediumState(SampleMediumState(session, Position));
    }

    public void Update(
        GameSession session,
        Vector3 movement,
        Vector2 facingInput,
        Vector3 cameraForwardOnGround,
        Vector3 cameraRightOnGround,
        bool sprinting,
        float deltaSeconds)
    {
        if (movement.LengthSquared() > 1f)
        {
            movement = Vector3.Normalize(movement);
        }

        var medium = SampleMediumState(session, Position);
        ApplyMediumState(medium);
        UpdateFacingDirection(movement, facingInput, cameraForwardOnGround, cameraRightOnGround);

        var movementScale = medium.Immersion > 0f
            ? Lerp(0.78f, 0.42f, medium.Immersion)
            : 1f;
        var sprintScale = sprinting
            ? medium.Immersion > 0f
                ? Lerp(1.18f, 1.04f, medium.Immersion)
                : SprintMultiplier
            : 1f;
        var speed = ScaleByVoxelDensity(session, BaseSpeedInOriginalBlocks) * movementScale * sprintScale;
        var desiredDelta = movement * speed * deltaSeconds;
        var supportProbeHeight = ScaleByVoxelDensity(session, SupportProbeHeightInOriginalBlocks);
        var groundSnapTolerance = ScaleByVoxelDensity(session, GroundSnapToleranceInOriginalBlocks);

        var candidate = Position;
        candidate = TryMove(session, candidate, new Vector2(desiredDelta.X, 0f));
        candidate = TryMove(session, candidate, new Vector2(0f, desiredDelta.Z));

        var liquidMedium = SampleMediumState(session, candidate);
        var standingHeight = FindStandingHeight(session, candidate.X, candidate.Z, candidate.Y + supportProbeHeight);
        var hasSupport = !float.IsNegativeInfinity(standingHeight);
        var canSnapToGround = hasSupport
            && _verticalVelocity <= 0f
            && candidate.Y <= standingHeight + groundSnapTolerance
            && liquidMedium.Immersion < 0.45f;

        if (canSnapToGround)
        {
            candidate.Y = standingHeight;
            _verticalVelocity = 0f;
            IsGrounded = true;
        }
        else
        {
            IsGrounded = false;
            if (liquidMedium.Immersion > 0f)
            {
                var verticalDamping = 1f - MathF.Exp(-4.5f * deltaSeconds * liquidMedium.Immersion);
                _verticalVelocity = Lerp(_verticalVelocity, 0f, verticalDamping);
            }

            var gravityScale = liquidMedium.Immersion > 0f
                ? Lerp(0.36f, 0.12f, liquidMedium.Immersion)
                : 1f;
            var gravity = ScaleByVoxelDensity(session, GravityInOriginalBlocks) * gravityScale;
            _verticalVelocity += gravity * deltaSeconds;
            if (liquidMedium.Immersion > 0f)
            {
                var buoyancy = ScaleByVoxelDensity(session, Lerp(7.5f, 18f, liquidMedium.Immersion));
                _verticalVelocity += buoyancy * MathF.Max(0f, liquidMedium.Immersion - 0.15f) * deltaSeconds;
                var maxRise = ScaleByVoxelDensity(session, 4f);
                var maxSink = ScaleByVoxelDensity(session, -6.5f);
                _verticalVelocity = Math.Clamp(_verticalVelocity, maxSink, maxRise);
            }

            candidate.Y += _verticalVelocity * deltaSeconds;

            if (hasSupport && candidate.Y <= standingHeight)
            {
                candidate.Y = standingHeight;
                if (liquidMedium.Immersion >= 0.45f)
                {
                    _verticalVelocity = ScaleByVoxelDensity(session, 1.4f);
                    IsGrounded = false;
                }
                else
                {
                    _verticalVelocity = 0f;
                    IsGrounded = true;
                }
            }
        }

        Position = candidate;
        ApplyMediumState(SampleMediumState(session, Position));
    }

    public Int3 GetStandingVoxel(GameSession session)
    {
        var supportProbeHeight = ScaleByVoxelDensity(session, SupportProbeHeightInOriginalBlocks);
        var x = (int)MathF.Floor(Position.X);
        var z = (int)MathF.Floor(Position.Z);
        var supportY = FindSupportBlockY(session, x, z, (int)MathF.Floor(Position.Y + supportProbeHeight));
        return new Int3(x, Math.Max(session.MinWorldY, supportY), z);
    }

    private void UpdateFacingDirection(
        Vector3 movement,
        Vector2 facingInput,
        Vector3 cameraForwardOnGround,
        Vector3 cameraRightOnGround)
    {
        Vector3 facingWorld;
        if (facingInput.LengthSquared() > 0.0001f)
        {
            facingWorld = (-facingInput.Y * cameraForwardOnGround) + (facingInput.X * cameraRightOnGround);
        }
        else
        {
            facingWorld = movement;
        }

        if (facingWorld.LengthSquared() < 0.0001f)
        {
            return;
        }

        if (MathF.Abs(facingWorld.X) >= MathF.Abs(facingWorld.Z))
        {
            FacingDirection = facingWorld.X >= 0f
                ? new Int3(1, 0, 0)
                : new Int3(-1, 0, 0);
            return;
        }

        FacingDirection = facingWorld.Z >= 0f
            ? new Int3(0, 0, 1)
            : new Int3(0, 0, -1);
    }

    private static Vector3 TryMove(GameSession session, Vector3 start, Vector2 delta)
    {
        if (delta == Vector2.Zero)
        {
            return start;
        }

        var supportProbeHeight = ScaleByVoxelDensity(session, SupportProbeHeightInOriginalBlocks);
        var maxStepHeight = ScaleByVoxelDensity(session, MaxStepHeightInOriginalBlocks);
        var candidate = new Vector3(start.X + delta.X, start.Y, start.Z + delta.Y);
        var currentGround = FindStandingHeight(session, start.X, start.Z, start.Y + supportProbeHeight);
        var targetGround = FindStandingHeight(session, candidate.X, candidate.Z, start.Y + maxStepHeight);
        if (float.IsNegativeInfinity(targetGround))
        {
            return candidate;
        }

        if (!float.IsNegativeInfinity(currentGround) && targetGround - currentGround > maxStepHeight)
        {
            return start;
        }

        return candidate;
    }

    private static float FindStandingHeight(GameSession session, float worldX, float worldZ, float referenceY)
    {
        var voxelsPerOriginalBlock = session.VoxelsPerOriginalBlock;
        var standingOffset = ScaleByVoxelDensity(session, StandingOffsetInOriginalBlocks);
        var searchStart = Math.Clamp((int)MathF.Floor(referenceY), session.MinWorldY, session.MaxWorldY);
        var bestSupport = float.NegativeInfinity;
        foreach (var sample in SupportSamplesInOriginalBlocks)
        {
            var x = (int)MathF.Floor(worldX + (sample.X * voxelsPerOriginalBlock));
            var z = (int)MathF.Floor(worldZ + (sample.Y * voxelsPerOriginalBlock));
            var supportY = FindSupportBlockY(session, x, z, searchStart);
            if (supportY < session.MinWorldY)
            {
                continue;
            }

            bestSupport = MathF.Max(bestSupport, supportY + standingOffset);
        }

        return bestSupport;
    }

    private void ApplyMediumState(MediumState medium)
    {
        IsInLiquid = medium.Immersion > 0f;
        IsSubmerged = medium.HeadInLiquid || medium.Immersion >= 2f / 3f;
        LiquidImmersion = medium.Immersion;
        CurrentMediumId = medium.MaterialId;
    }

    private static MediumState SampleMediumState(GameSession session, Vector3 position)
    {
        var feetMaterial = SampleLiquidMaterialId(session, position, LiquidFeetSampleHeightInOriginalBlocks);
        var torsoMaterial = SampleLiquidMaterialId(session, position, LiquidTorsoSampleHeightInOriginalBlocks);
        var headMaterial = SampleLiquidMaterialId(session, position, LiquidHeadSampleHeightInOriginalBlocks);
        var immersion =
            ((feetMaterial is not null ? 1f : 0f)
             + (torsoMaterial is not null ? 1f : 0f)
             + (headMaterial is not null ? 1f : 0f))
            / 3f;
        return new MediumState(
            headMaterial ?? torsoMaterial ?? feetMaterial ?? "air",
            immersion,
            feetMaterial is not null,
            torsoMaterial is not null,
            headMaterial is not null);
    }

    private static string? SampleLiquidMaterialId(GameSession session, Vector3 position, float heightInOriginalBlocks)
    {
        var voxelsPerOriginalBlock = session.VoxelsPerOriginalBlock;
        var y = (int)MathF.Floor(position.Y + (heightInOriginalBlocks * voxelsPerOriginalBlock));
        foreach (var sample in SupportSamplesInOriginalBlocks)
        {
            var x = (int)MathF.Floor(position.X + (sample.X * voxelsPerOriginalBlock));
            var z = (int)MathF.Floor(position.Z + (sample.Y * voxelsPerOriginalBlock));
            var material = session.InspectMaterialAtCurrentPlanet(new Int3(x, y, z));
            if (IsLiquidMaterial(material))
            {
                return material.Id;
            }
        }

        return null;
    }

    private static float ScaleByVoxelDensity(GameSession session, float originalBlockUnits) =>
        originalBlockUnits * session.VoxelsPerOriginalBlock;

    private static float Lerp(float from, float to, float amount) =>
        from + ((to - from) * amount);

    private static bool IsLiquidMaterial(MaterialDef material) =>
        !material.IsSolid && !string.Equals(material.Id, "air", StringComparison.OrdinalIgnoreCase);

    private static int FindSupportBlockY(GameSession session, int x, int z, int startY)
    {
        var clampedStart = Math.Clamp(startY, session.MinWorldY, session.MaxWorldY);
        for (var y = clampedStart; y >= session.MinWorldY; y--)
        {
            if (session.IsSolidAtCurrentPlanet(new Int3(x, y, z)))
            {
                return y;
            }
        }

        return session.MinWorldY - 1;
    }
}
