using System.Numerics;

namespace Sectorbound.Client;

internal sealed class InputTracker
{
    public bool IsJumpPressed() => IsDown(Keys.Space);
    private readonly HashSet<Keys> _pressed = [];

    public bool SetKeyDown(Keys key) => _pressed.Add(Normalize(key));

    public void SetKeyUp(Keys key) => _pressed.Remove(Normalize(key));

    public bool IsDown(Keys key) => _pressed.Contains(Normalize(key));

    public Vector2 ReadMovement()
    {
        var axis = Vector2.Zero;
        if (IsDown(Keys.W) || IsDown(Keys.Up))
        {
            axis.Y -= 1f;
        }

        if (IsDown(Keys.S) || IsDown(Keys.Down))
        {
            axis.Y += 1f;
        }

        if (IsDown(Keys.A) || IsDown(Keys.Left))
        {
            axis.X -= 1f;
        }

        if (IsDown(Keys.D) || IsDown(Keys.Right))
        {
            axis.X += 1f;
        }

        return axis.LengthSquared() > 1f ? Vector2.Normalize(axis) : axis;
    }

    private static Keys Normalize(Keys key) => key & Keys.KeyCode;
}
