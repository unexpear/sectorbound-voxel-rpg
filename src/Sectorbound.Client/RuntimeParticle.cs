using System.Drawing;
using System.Numerics;

namespace Sectorbound.Client;

internal struct RuntimeParticle
{
    public RuntimeParticle(
        Vector3 position,
        Vector3 velocity,
        Color color,
        float size,
        float lifetime,
        float drag,
        float gravity)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        Size = size;
        Lifetime = lifetime;
        Drag = drag;
        Gravity = gravity;
    }

    public Vector3 Position;
    public Vector3 Velocity;
    public Color Color;
    public float Size;
    public float Lifetime;
    public float Age;
    public float Drag;
    public float Gravity;
}
