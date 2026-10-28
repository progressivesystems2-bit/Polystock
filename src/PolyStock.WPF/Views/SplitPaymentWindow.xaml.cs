using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PolyStock.Core.Models;

namespace PolyStock.WPF.Views;

public partial class SplitPaymentWindow : Window
{
    private readonly double _grandTotal;

    public double CashAmount { get; private set; }
    public double UpiAmount { get; private set; }
    public double RemainingCredit { get; private set; }
    public Account? SelectedUpiAccount { get; private set; }
    public string? UtrRefNo { get; private set; }

    public SplitPaymentWindow(
        double grandTotal,
        IEnumerable<Account> upiAccounts,
        double initialCash = 0,
        double initialUpi = 0,
        Account? initialUpiAccount = null,
        string? initialUtr = null)
    {
        InitializeComponent();

        _grandTotal = Math.Max(0, grandTotal);
        TxtGrandTotal.Text = $"₹ {_grandTotal:N2}";

        var accountsList = upiAccounts.ToList();
        CmbUpiAccount.ItemsSource = accountsList;

        if (initialUpiAccount != null)
        {
            CmbUpiAccount.SelectedItem = accountsList.FirstOrDefault(a => a.Id == initialUpiAccount.Id) ?? accountsList.FirstOrDefault();
        }
        else
        {
            CmbUpiAccount.SelectedItem = accountsList.FirstOrDefault();
        }

        TxtCashAmount.Text = initialCash > 0 ? initialCash.ToString("F2") : "0.00";
        TxtUpiAmount.Text = initialUpi > 0 ? initialUpi.ToString("F2") : "0.00";
        TxtUtrRef.Text = initialUtr ?? string.Empty;

        UpdateCalculations();

        Loaded += (s, e) =>
        {
            TxtCashAmount.Focus();
            TxtCashAmount.SelectAll();
        };

        PreviewKeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        };
    }

    private void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateCalculations();
    }

    private void UpdateCalculations()
    {
        double.TryParse(TxtCashAmount.Text, out double cash);
        double.TryParse(TxtUpiAmount.Text, out double upi);

        cash = Math.Max(0, cash);
        upi = Math.Max(0, upi);

        double totalReceived = cash + upi;
        double remaining = _grandTotal - totalReceived;

        if (remaining < -0.01)
        {
            TxtWarning.Text = $"⚠️ Total received (₹ {totalReceived:N2}) exceeds bill total by ₹ {Math.Abs(remaining):N2}!";
            TxtRemainingCredit.Text = "₹ 0.00";
            BtnConfirm.IsEnabled = false;
        }
        else
        {
            TxtWarning.Text = string.Empty;
            TxtRemainingCredit.Text = $"₹ {Math.Max(0, remaining):N2}";
            BtnConfirm.IsEnabled = true;
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        double.TryParse(TxtCashAmount.Text, out double cash);
        double.TryParse(TxtUpiAmount.Text, out double upi);

        cash = Math.Max(0, cash);
        upi = Math.Max(0, upi);

        if (cash + upi > _grandTotal + 0.01)
        {
            MessageBox.Show("Total allocated payment cannot exceed Grand Total.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (upi > 0 && CmbUpiAccount.SelectedItem == null)
        {
            MessageBox.Show("Please select a UPI / Bank Account for the UPI payment portion.", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
            CmbUpiAccount.Focus();
            return;
        }

        CashAmount = cash;
        UpiAmount = upi;
        RemainingCredit = Math.Max(0, _grandTotal - (cash + upi));
        SelectedUpiAccount = CmbUpiAccount.SelectedItem as Account;
        UtrRefNo = TxtUtrRef.Text?.Trim();

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void TxtCashAmount_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            TxtUpiAmount.Focus();
            TxtUpiAmount.SelectAll();
            e.Handled = true;
        }
    }

    private void TxtUpiAmount_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (CmbUpiAccount.SelectedItem == null)
            {
                CmbUpiAccount.Focus();
            }
            else
            {
                BtnConfirm.Focus();
            }
            e.Handled = true;
        }
    }
}
