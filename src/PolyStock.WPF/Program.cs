using System;
using Velopack;
using PolyStock.WPF.Services;

namespace PolyStock.WPF;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            // Velopack startup hook: handles install/uninstall/restart hooks immediately
            VelopackApp.Build()
                .OnFirstRun((version) =>
                {
                    UpdateService.IsFirstRunAfterInstall = true;
                    UpdateService.JustUpdatedVersion = version.ToFullString();
                })
                .OnRestarted((version) =>
                {
                    UpdateService.IsAppJustRestartedAfterUpdate = true;
                    UpdateService.JustUpdatedVersion = version.ToFullString();
                })
                .Run();

            // Launch WPF Application
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
        catch (Exception ex)
        {
            ErrorHandlingService.LogException("Program.Main Startup Crash", ex);
            System.Windows.MessageBox.Show(
                $"Application Startup Error:\n{ex.Message}\n\nTechnical details have been saved to the error log:\n{ErrorHandlingService.LogFilePath}",
                "PolyStock Startup Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }
}
