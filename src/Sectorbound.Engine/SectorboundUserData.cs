namespace Sectorbound.Engine;

public static class SectorboundUserData
{
    private const string ProductFolderName = "Sectorbound";
    private const string LegacyProductFolderName = "SectorboundPrototype";

    public static string GetDataRoot()
    {
        var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var path = Path.Combine(localAppDataPath, ProductFolderName);
        MigrateLegacyData(localAppDataPath, path);
        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetSaveRoot()
    {
        var path = Path.Combine(GetDataRoot(), "Saves");
        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetLogRoot()
    {
        var path = Path.Combine(GetDataRoot(), "Logs");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void MigrateLegacyData(string localAppDataPath, string newRoot)
    {
        var legacyRoot = Path.Combine(localAppDataPath, LegacyProductFolderName);
        if (!Directory.Exists(legacyRoot)
            || string.Equals(legacyRoot, newRoot, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!Directory.Exists(newRoot))
        {
            try
            {
                Directory.Move(legacyRoot, newRoot);
                return;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        Directory.CreateDirectory(newRoot);

        foreach (var directory in Directory.GetDirectories(legacyRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(legacyRoot, directory);
            Directory.CreateDirectory(Path.Combine(newRoot, relativePath));
        }

        foreach (var file in Directory.GetFiles(legacyRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(legacyRoot, file);
            var targetPath = Path.Combine(newRoot, relativePath);
            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            if (!File.Exists(targetPath))
            {
                File.Copy(file, targetPath);
            }
        }
    }
}
