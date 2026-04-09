namespace Sectorbound.Engine;

public static class SectorboundUserData
{
    private const string ProductFolderName = "SectorboundPrototype";

    public static string GetDataRoot()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ProductFolderName);
        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetSaveRoot()
    {
        var path = Path.Combine(GetDataRoot(), "Saves");
        Directory.CreateDirectory(path);
        return path;
    }
}
