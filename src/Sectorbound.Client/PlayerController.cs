using System.Numerics;
using Sectorbound.Engine;

namespace Sectorbound.Client;

internal sealed class PlayerController
{
    private const float BaseSpeed = 6.25f;
    private const float SprintMultiplier = 1.6f;
    private const float MaxStepHeight = 1.15f;
    private const float StandingOffset = 1.05f;
    private const float SupportProbeHeight = 0.65f;
    private const float GroundSnapTolerance = 0.08f;
    private const float Gravity = -22.5f;

    private static readonly Vector2[] SupportSamples =
    [
        new Vector2(0f, 0f),
        new Vector2(0.24f, 0f),
        new Vector2(-0.24f, 0f),
        new Vector2(0f, 0.24f),
        new Vector2(0f, -0.24f)
    ];

    private float _verticalVelocity;

    public Vector3 Position { get; private set; }
    public Int3 FacingDirection { get; private set; } = new(0, 0, -1);
    public bool IsGrounded { get; private set; }

    public void Spawn(GameSession session, Vector2 worldXZ)
    {
        var ground = FindStandingHeight(session, worldXZ.X, worldXZ.Y, session.MaxWorldY);
        Position = new Vector3(worldXZ.X, ground, worldXZ.Y);
        FacingDirection = new Int3(0, 0, -1);
        _verticalVelocity = 0f;
        IsGrounded = true;
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

        UpdateFacingDirection(movement, facingInput, cameraForwardOnGround, cameraRightOnGround);

        var speed = BaseSpeed * (sprinting ? SprintMultiplier : 1f);
        var desiredDelta = movement * speed * deltaSeconds;

        var candidate = Position;
        candidate = TryMove(session, candidate, new Vector2(desiredDelta.X, 0f));
        candidate = TryMove(session, candidate, new Vector2(0f, desiredDelta.Z));

        var standingHeight = FindStandingHeight(session, candidate.X, candidate.Z, candidate.Y + SupportProbeHeight);
        var hasSupport = !float.IsNegativeInfinity(standingHeight);
        var canSnapToGround = hasSupport
            && _verticalVelocity <= 0f
            && candidate.Y <= standingHeight + GroundSnapTolerance;

        if (canSnapToGround)
        {
            candidate.Y = standingHeight;
            _verticalVelocity = 0f;
            IsGrounded = true;
        }
        else
        {
            IsGrounded = false;
            _verticalVelocity += Gravity * deltaSeconds;
            candidate.Y += _verticalVelocity * deltaSeconds;

            if (hasSupport && candidate.Y <= standingHeight)
            {
                candidate.Y = standingHeight;
                _verticalVelocity = 0f;
                IsGrounded = true;
            }
        }

        Position = candidate;
    }

    public Int3 GetStandingVoxel(GameSession session)
    {
        var x = (int)MathF.Floor(Position.X);
        var z = (int)MathF.Floor(Position.Z);
        var supportY = FindSupportBlockY(session, x, z, (int)MathF.Floor(Position.Y + SupportProbeHeight));
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

        var candidate = new Vector3(start.X + delta.X, start.Y, start.Z + delta.Y);
        var currentGround = FindStandingHeight(session, start.X, start.Z, start.Y + SupportProbeHeight);
        var targetGround = FindStandingHeight(session, candidate.X, candidate.Z, start.Y + MaxStepHeight);
        if (float.IsNegativeInfinity(targetGround))
        {
            return candidate;
        }

        if (!float.IsNegativeInfinity(currentGround) && targetGround - currentGround > MaxStepHeight)
        {
            return start;
        }

        return candidate;
    }

    private static float FindStandingHeight(GameSession session, float worldX, float worldZ, float referenceY)
    {
        var searchStart = Math.Clamp((int)MathF.Floor(referenceY), session.MinWorldY, session.MaxWorldY);
        var bestSupport = float.NegativeInfinity;
        foreach (var sample in SupportSamples)
        {
            var x = (int)MathF.Floor(worldX + sample.X);
            var z = (int)MathF.Floor(worldZ + sample.Y);
            var supportY = FindSupportBlockY(session, x, z, searchStart);
            if (supportY < session.MinWorldY)
            {
                continue;
            }

            bestSupport = MathF.Max(bestSupport, supportY + StandingOffset);
        }

        return bestSupport;
    }

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
