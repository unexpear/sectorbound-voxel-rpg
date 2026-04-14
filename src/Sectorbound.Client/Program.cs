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
            using var menu = new StartMenuForm();
            menu.ShowDialog();
            if (!menu.ShouldStartGame)
                return;

            Application.Run(new RuntimeForm(
                newGame: menu.StartNewGame,
                newGameDifficultyPresetId: menu.SelectedDifficultyPresetId));
        }
        catch (Exception exception)
        {
            ClientDiagnostics.WriteException("startup", exception);
            throw;
        }
    }
}
