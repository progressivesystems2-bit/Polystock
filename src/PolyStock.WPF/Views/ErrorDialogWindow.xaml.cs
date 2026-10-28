using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace PolyStock.WPF.Views;

public partial class ErrorDialogWindow : Window
{
    private readonly string _fullDetails;
    private readonly string? _logFilePath;

    public ErrorDialogWindow(Exception exception, string? logFilePath = null)
    {
        InitializeComponent();
        _logFilePath = logFilePath;

        string errorSummary = exception.Message;
        if (exception.InnerException != null)
        {
            errorSummary += $"\nInner Error: {exception.InnerException.Message}";
        }

        _fullDetails = $"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                       $"Exception Type: {exception.GetType().FullName}\n" +
                       $"Message: {exception.Message}\n\n" +
                       $"Stack Trace:\n{exception.StackTrace}";

        if (exception.InnerException != null)
        {
            _fullDetails += $"\n\nInner Exception: {exception.InnerException.GetType().FullName}\n" +
                            $"Inner Message: {exception.InnerException.Message}\n" +
                            $"Inner Stack Trace:\n{exception.InnerException.StackTrace}";
        }

        ErrorMessageText.Text = errorSummary;
        StackTraceTextBox.Text = _fullDetails;

        if (string.IsNullOrEmpty(_logFilePath) || !File.Exists(_logFilePath))
        {
            OpenLogButton.Visibility = Visibility.Collapsed;
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_fullDetails);
            CopyStatusText.Text = "✓ Copied to clipboard!";
        }
        catch
        {
            CopyStatusText.Text = "Unable to copy";
        }
    }

    private void OpenLogButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_logFilePath) && File.Exists(_logFilePath))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _logFilePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open log file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
