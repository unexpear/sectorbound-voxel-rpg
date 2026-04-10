using System.Numerics;

namespace Sectorbound.Client;

internal sealed class RuntimeCamera
{
    private const float CameraSmoothing = 10f;
    private const float FocusHeight = 1.45f;
    private const float MinDistance = 4.25f;
    private const float MaxDistance = 10f;
    private const float MinPitch = 0.12f;
    private const float MaxPitch = 0.95f;

    private float _distance = 5.75f;
    private float _yaw = -MathF.PI * 0.5f;
    private float _pitch = 0.42f;

    public Vector3 Position { get; private set; } = new(0f, 3.5f, 6f);
    public Vector3 Target { get; private set; } = Vector3.Zero;
    public Vector3 ForwardOnGround { get; private set; } = new(0f, 0f, -1f);
    public Vector3 RightOnGround { get; private set; } = new(1f, 0f, 0f);
    public Matrix4x4 ViewProjection { get; private set; }

    public void Update(Vector3 target, Size viewportSize, float deltaSeconds)
    {
        var focusPoint = target + new Vector3(0f, FocusHeight, 0f);
        Target = focusPoint;

        var viewDirection = Vector3.Normalize(new Vector3(
            MathF.Cos(_pitch) * MathF.Cos(_yaw),
            -MathF.Sin(_pitch),
            MathF.Cos(_pitch) * MathF.Sin(_yaw)
        ));
        var desiredPosition = focusPoint - (viewDirection * _distance);
        var smoothing = 1f - MathF.Exp(-CameraSmoothing * deltaSeconds);
        Position = Vector3.Lerp(Position, desiredPosition, smoothing);

        var forward = Target - Position;
        if (forward.LengthSquared() > 0.0001f)
        {
            forward = Vector3.Normalize(forward);
        }
        else
        {
            forward = viewDirection;
        }

        var forwardOnGround = new Vector3(forward.X, 0f, forward.Z);
        ForwardOnGround = forwardOnGround.LengthSquared() > 0.0001f
            ? Vector3.Normalize(forwardOnGround)
            : new Vector3(0f, 0f, -1f);
        RightOnGround = Vector3.Normalize(Vector3.Cross(ForwardOnGround, Vector3.UnitY));

        var aspect = Math.Max(1f, viewportSize.Width / (float)Math.Max(1, viewportSize.Height));
        var view = Matrix4x4.CreateLookAt(Position, focusPoint, Vector3.UnitY);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3.2f, aspect, 0.1f, 256f);
        ViewProjection = view * projection;
    }

    public void AdjustDistance(float delta)
    {
        _distance = Math.Clamp(_distance + delta, MinDistance, MaxDistance);
    }

    public void AdjustYawPitch(float deltaYaw, float deltaPitch)
    {
        _yaw += deltaYaw;
        _pitch = Math.Clamp(_pitch + deltaPitch, MinPitch, MaxPitch);
    }
}
