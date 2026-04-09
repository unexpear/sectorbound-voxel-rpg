namespace Sectorbound.Client;

static class Program
{
    [STAThread]
    static void Main()
    {
        ClientDiagnostics.Install();
        ClientDiagnostics.WriteInfo("Starting Sectorbound client.");
        ApplicationConfiguration.Initialize();
        try
        {
            Application.Run(new RuntimeForm());
        }
        catch (Exception exception)
        {
            ClientDiagnostics.WriteException("startup", exception);
            throw;
        }
    }
}
