using System;
using System.Windows;
using System.Windows.Input;
using PolyStock.Core.Models;
using PolyStock.Services;

namespace PolyStock.WPF.Views;

public partial class LoginWindow : Window
{
    private readonly SecurityService _security;

    public User? AuthenticatedUser { get; private set; }

    public LoginWindow(SecurityService security)
    {
        InitializeComponent();
        _security = security;

        Loaded += LoginWindow_Loaded;
        MouseDown += (s, e) =>
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        };
    }

    private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UsernameTextBox.Focus();
        CheckCapsLock();
    }

    private void Input_KeyDown(object sender, KeyEventArgs e)
    {
        CheckCapsLock();
    }

    private void CheckCapsLock()
    {
        bool isCapsLock = Console.CapsLock;
        CapsLockWarningText.Visibility = isCapsLock ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        string username = UsernameTextBox.Text.Trim();
        string password = PasswordInputBox.Password;

        if (string.IsNullOrWhiteSpace(username))
        {
            ShowError("Please enter your username.");
            UsernameTextBox.Focus();
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowError("Please enter your password.");
            PasswordInputBox.Focus();
            return;
        }

        var result = _security.Authenticate(username, password);
        if (result.Success && result.User != null)
        {
            AuthenticatedUser = result.User;
            DialogResult = true;
            Close();
        }
        else
        {
            ShowError(result.Message);
            PasswordInputBox.Password = string.Empty;
            PasswordInputBox.Focus();
        }
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowError(string message)
    {
        ErrorMessageText.Text = message;
        ErrorBanner.Visibility = Visibility.Visible;
    }
}
