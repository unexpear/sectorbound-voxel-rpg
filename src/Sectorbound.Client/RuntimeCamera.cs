using System.Numerics;

namespace Sectorbound.Client;

internal sealed class RuntimeCamera
{
    private const float CameraSmoothing = 8f;
    private float _distance = 14f;
    private float _height = 9f;
    private float _yaw = MathF.PI * 0.25f; // 45 degrees
    private float _pitch = 0.35f; // slightly above horizontal
    private const float MinPitch = -1.2f;
    private const float MaxPitch = 1.2f;
    public Vector3 Position { get; private set; } = new(-14f, 9f, -14f);
    public Vector3 Target { get; private set; } = Vector3.Zero;
    public Vector3 ForwardOnGround { get; private set; } = Vector3.Normalize(new Vector3(1f, 0f, 1f));
    public Vector3 RightOnGround { get; private set; } = Vector3.Normalize(new Vector3(-1f, 0f, 1f));
    public Matrix4x4 ViewProjection { get; private set; }

    public void Update(Vector3 target, Size viewportSize, float deltaSeconds)
    {
        Target = target;
        // Calculate offset from yaw/pitch
        var offset = new Vector3(
            MathF.Cos(_pitch) * MathF.Cos(_yaw),
            MathF.Sin(_pitch),
            MathF.Cos(_pitch) * MathF.Sin(_yaw)
        ) * _distance;
        var desiredPosition = target + new Vector3(offset.X, offset.Y, offset.Z);
        var smoothing = 1f - MathF.Exp(-CameraSmoothing * deltaSeconds);
        Position = Vector3.Lerp(Position, desiredPosition, smoothing);

        var forward = Vector3.Normalize(Target - Position);
        ForwardOnGround = Vector3.Normalize(new Vector3(forward.X, 0f, forward.Z));
        RightOnGround = Vector3.Normalize(Vector3.Cross(ForwardOnGround, Vector3.UnitY));

        var aspect = Math.Max(1f, viewportSize.Width / (float)Math.Max(1, viewportSize.Height));
        var view = Matrix4x4.CreateLookAt(Position, target + new Vector3(0f, 1.25f, 0f), Vector3.UnitY);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3.2f, aspect, 0.1f, 256f);
        ViewProjection = view * projection;
    }

    public void AdjustDistance(float delta)
    {
        _distance = Math.Clamp(_distance + delta, 8f, 24f);
        _height = Math.Clamp((_distance * 0.66f), 6f, 15f);
    }

    public void AdjustYawPitch(float deltaYaw, float deltaPitch)
    {
        _yaw += deltaYaw;
        _pitch = Math.Clamp(_pitch + deltaPitch, MinPitch, MaxPitch);
    }
}
