using System.Numerics;
using Sectorbound.Engine;

namespace Sectorbound.Client;

internal sealed class PlayerController
{
    private const float BaseSpeed = 6.25f;
    private const float SprintMultiplier = 1.6f;
    private const float MaxStepHeight = 1.2f;
    private const float StandingOffset = 1.05f;
    private const float HeightSmoothing = 16f;

    public Vector3 Position { get; private set; }
    public Int3 FacingDirection { get; private set; } = new(0, 0, -1);
    private float _verticalVelocity = 0f;
    private bool _isGrounded = false;
    private const float Gravity = -22.5f;
    private const float JumpVelocity = 9.5f;

    public void Spawn(GameSession session, Vector2 worldXZ)
    {
        var ground = GetStandingHeight(session, worldXZ.X, worldXZ.Y);
        Position = new Vector3(worldXZ.X, ground, worldXZ.Y);
        FacingDirection = new Int3(0, 0, -1);
        _verticalVelocity = 0f;
        _isGrounded = true;
    }

    public void Update(GameSession session, Vector3 movement, bool sprinting, bool jumpPressed, float deltaSeconds)
    {
        if (movement.LengthSquared() > 1f)
        {
            movement = Vector3.Normalize(movement);
        }

        UpdateFacingDirection(movement);

        var speed = BaseSpeed * (sprinting ? SprintMultiplier : 1f);
        var desiredDelta = movement * speed * deltaSeconds;

        var candidate = Position;
        candidate = TryMove(session, candidate, new Vector2(desiredDelta.X, 0f));
        candidate = TryMove(session, candidate, new Vector2(0f, desiredDelta.Z));

        // Gravity and jumping
        var groundY = GetStandingHeight(session, candidate.X, candidate.Z);
        _isGrounded = Math.Abs(candidate.Y - groundY) < 0.05f;
        if (_isGrounded)
        {
            candidate.Y = groundY;
            _verticalVelocity = 0f;
            if (jumpPressed)
            {
                _verticalVelocity = JumpVelocity;
                _isGrounded = false;
            }
        }
        else
        {
            _verticalVelocity += Gravity * deltaSeconds;
        }
        candidate.Y += _verticalVelocity * deltaSeconds;

        // Prevent falling through the ground
        if (candidate.Y < groundY)
        {
            candidate.Y = groundY;
            _verticalVelocity = 0f;
            _isGrounded = true;
        }

        Position = candidate;
    }
    public bool IsGrounded => _isGrounded;

    public Int3 GetStandingVoxel(GameSession session)
    {
        var x = (int)MathF.Floor(Position.X);
        var z = (int)MathF.Floor(Position.Z);
        return session.GetSurfaceVoxel(x, z);
    }

    private void UpdateFacingDirection(Vector3 movement)
    {
        if (movement.LengthSquared() < 0.0001f)
        {
            return;
        }

        if (MathF.Abs(movement.X) >= MathF.Abs(movement.Z))
        {
            FacingDirection = movement.X >= 0f
                ? new Int3(1, 0, 0)
                : new Int3(-1, 0, 0);
            return;
        }

        FacingDirection = movement.Z >= 0f
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
        var currentGround = GetStandingHeight(session, start.X, start.Z);
        var targetGround = GetStandingHeight(session, candidate.X, candidate.Z);

        return targetGround - currentGround <= MaxStepHeight ? candidate : start;
    }

    private static float GetStandingHeight(GameSession session, float worldX, float worldZ)
    {
        var tileX = (int)MathF.Floor(worldX);
        var tileZ = (int)MathF.Floor(worldZ);
        return session.GetSurfaceHeight(tileX, tileZ) + StandingOffset;
    }
}
