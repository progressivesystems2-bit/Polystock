using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using PolyStock.WPF.Views;

namespace PolyStock.WPF.Services;

public static class ErrorHandlingService
{
    private static readonly object _logLock = new();
    private static string? _logFilePath;

    public static string LogFilePath
    {
        get
        {
            if (_logFilePath == null)
            {
                try
                {
                    string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string logDir = Path.Combine(localAppData, "PolyStock", "Logs");
                    Directory.CreateDirectory(logDir);
                    _logFilePath = Path.Combine(logDir, "error_log.txt");
                }
                catch
                {
                    _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error_log.txt");
                }
            }
            return _logFilePath;
        }
    }

    public static void Initialize(Application app)
    {
        // 1. Intercept UI thread exceptions and prevent process termination
        app.DispatcherUnhandledException += OnDispatcherUnhandledException;

        // 2. Intercept async background Task exceptions
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // 3. Intercept AppDomain unhandled exceptions
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            // Log the exception to disk
            LogException("UI Dispatcher Exception", e.Exception);

            // Display non-fatal, informative dialog to user
            ShowErrorDialog(e.Exception);

            // Mark exception as HANDLED so the application dispatcher remains alive
            e.Handled = true;
        }
        catch (Exception ex)
        {
            // Fallback safety: do not allow the error handler itself to crash the app
            try
            {
                MessageBox.Show(
                    $"An error occurred:\n{e.Exception?.Message}\n\n(Error Handler Note: {ex.Message})",
                    "PolyStock Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch { }

            e.Handled = true;
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        try
        {
            LogException("Unobserved Task Exception", e.Exception);
            // Prevent the unobserved task exception from triggering process termination
            e.SetObserved();
        }
        catch { }
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        try
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogException($"AppDomain Exception (IsTerminating: {e.IsTerminating})", ex);
            }
        }
        catch { }
    }

    public static void LogException(string context, Exception? ex)
    {
        if (ex == null) return;

        try
        {
            lock (_logLock)
            {
                var sb = new StringBuilder();
                sb.AppendLine("================================================================================");
                sb.AppendLine($"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | Context: {context}");
                sb.AppendLine($"Exception: {ex.GetType().FullName}");
                sb.AppendLine($"Message: {ex.Message}");
                sb.AppendLine($"Stack Trace:\n{ex.StackTrace}");

                Exception? inner = ex.InnerException;
                while (inner != null)
                {
                    sb.AppendLine("--- Inner Exception ---");
                    sb.AppendLine($"Type: {inner.GetType().FullName}");
                    sb.AppendLine($"Message: {inner.Message}");
                    sb.AppendLine($"Stack Trace:\n{inner.StackTrace}");
                    inner = inner.InnerException;
                }
                sb.AppendLine();

                File.AppendAllText(LogFilePath, sb.ToString());
            }
        }
        catch
        {
            // Best-effort logging; ignore disk write errors
        }
    }

    public static void ShowErrorDialog(Exception ex)
    {
        try
        {
            var dialog = new ErrorDialogWindow(ex, LogFilePath);

            // Set parent window if main window exists and is visible
            if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                dialog.Owner = Application.Current.MainWindow;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            dialog.ShowDialog();
        }
        catch
        {
            // Fallback to standard MessageBox if custom dialog fails
            MessageBox.Show(
                $"Operation Notice:\n{ex.Message}\n\nThe application protected your session and remains open.",
                "PolyStock - Notice",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
