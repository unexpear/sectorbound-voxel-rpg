using System.Numerics;

namespace Sectorbound.Client;

internal sealed class InputTracker
{
    private readonly HashSet<Keys> _pressed = [];
    private readonly List<Keys> _movementPressOrder = [];

    public bool SetKeyDown(Keys key)
    {
        var normalized = Normalize(key);
        var added = _pressed.Add(normalized);
        if (added && IsMovementKey(normalized))
        {
            _movementPressOrder.Remove(normalized);
            _movementPressOrder.Add(normalized);
        }

        return added;
    }

    public void SetKeyUp(Keys key)
    {
        var normalized = Normalize(key);
        _pressed.Remove(normalized);
        _movementPressOrder.Remove(normalized);
    }

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

    public Vector2 ReadFacingMovement()
    {
        for (var index = _movementPressOrder.Count - 1; index >= 0; index--)
        {
            var key = _movementPressOrder[index];
            if (!_pressed.Contains(key))
            {
                continue;
            }

            return key switch
            {
                Keys.W or Keys.Up => new Vector2(0f, -1f),
                Keys.S or Keys.Down => new Vector2(0f, 1f),
                Keys.A or Keys.Left => new Vector2(-1f, 0f),
                Keys.D or Keys.Right => new Vector2(1f, 0f),
                _ => Vector2.Zero
            };
        }

        return Vector2.Zero;
    }

    private static Keys Normalize(Keys key) => key & Keys.KeyCode;

    private static bool IsMovementKey(Keys key) =>
        key is Keys.W or Keys.A or Keys.S or Keys.D
            or Keys.Up or Keys.Left or Keys.Down or Keys.Right;
}
