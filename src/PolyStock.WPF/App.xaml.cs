using System;
using System.Windows;
using PolyStock.Data;
using PolyStock.Services;
using PolyStock.WPF.ViewModels;

namespace PolyStock.WPF;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global unhandled exception handlers: prevent abrupt crashes and keep application active
        Services.ErrorHandlingService.Initialize(this);

        try
        {
            // Initialize Access Database, verify/create schema and seed default security roles/users
            DatabaseInitializer.Initialize();

            RunApplication();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to start PolyStock application:\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}",
                "Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void RunApplication()
    {
        // Prevent application from shutting down when the modal LoginWindow closes
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var inventoryService = new InventoryService();

        // 1. Authenticate user via LoginWindow
        var loginWindow = new Views.LoginWindow(inventoryService.Security);
        bool? loginSuccess = loginWindow.ShowDialog();
        if (loginSuccess != true || !inventoryService.Security.IsAuthenticated)
        {
            Shutdown();
            return;
        }

        // 2. Launch Main Window with authenticated context
        var mainVm = new MainViewModel(inventoryService);
        var mainWindow = new MainWindow
        {
            DataContext = mainVm
        };

        this.MainWindow = mainWindow;
        this.ShutdownMode = ShutdownMode.OnMainWindowClose;

        mainVm.RequestUserSwitchAction = () =>
        {
            mainWindow.Hide();
            var switchDialog = new Views.LoginWindow(inventoryService.Security);
            bool? switchSuccess = switchDialog.ShowDialog();
            if (switchSuccess == true && inventoryService.Security.IsAuthenticated)
            {
                mainWindow.Show();
            }
            else
            {
                mainWindow.Close();
                Shutdown();
            }
        };

        mainWindow.Loaded += async (s, args) =>
        {
            await Services.UpdateService.CheckOnStartupAsync(mainWindow);
        };

        mainWindow.Show();
    }
}
