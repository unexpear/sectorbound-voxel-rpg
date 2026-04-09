using Sectorbound.Engine;

namespace Sectorbound.Client;

internal static class ClientDiagnostics
{
    private static readonly object SyncRoot = new();

    public static void Install()
    {
        Application.ThreadException += (_, args) => WriteException("winforms-thread", args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                WriteException(args.IsTerminating ? "appdomain-terminating" : "appdomain", exception);
            }
            else if (args.ExceptionObject is not null)
            {
                WriteInfo($"Unhandled non-exception object: {args.ExceptionObject}");
            }
        };
    }

    public static void WriteInfo(string message) => WriteLine("INFO", message);

    public static void WriteException(string source, Exception exception) =>
        WriteLine("ERROR", $"{source}: {exception}");

    private static void WriteLine(string level, string message)
    {
        try
        {
            var logRoot = SectorboundUserData.GetLogRoot();
            var logPath = Path.Combine(logRoot, $"client-{DateTime.UtcNow:yyyyMMdd}.log");
            var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {level} {message}{Environment.NewLine}";
            lock (SyncRoot)
            {
                File.AppendAllText(logPath, line);
            }
        }
        catch
        {
            // Avoid cascading failures during startup or crash handling.
        }
    }
}
