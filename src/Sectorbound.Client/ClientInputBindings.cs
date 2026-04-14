namespace Sectorbound.Client;

internal static class ClientInputBindings
{
    public static bool IsDecreaseMouseSensitivityKey(Keys key) => key == Keys.Oem4;

    public static bool IsIncreaseMouseSensitivityKey(Keys key) => key == Keys.Oem6;
}
