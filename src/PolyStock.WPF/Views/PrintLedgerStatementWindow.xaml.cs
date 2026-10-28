using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using PolyStock.Core.Models;

namespace PolyStock.WPF.Views;

public partial class PrintLedgerStatementWindow : Window
{
    public PrintLedgerStatementWindow(
        Account account,
        List<LedgerStatementItem> entries,
        DateTime? fromDate,
        DateTime? toDate,
        double openingBalance,
        string openingBalanceType,
        double totalDebit,
        double totalCredit,
        double closingBalance)
    {
        InitializeComponent();
        DataContext = account;

        DgStatement.ItemsSource = entries;

        if (fromDate.HasValue && toDate.HasValue)
        {
            TxtPeriod.Text = $"Period: {fromDate:dd-MMM-yyyy} to {toDate:dd-MMM-yyyy}";
        }
        else if (fromDate.HasValue)
        {
            TxtPeriod.Text = $"Period: From {fromDate:dd-MMM-yyyy}";
        }
        else if (toDate.HasValue)
        {
            TxtPeriod.Text = $"Period: Up to {toDate:dd-MMM-yyyy}";
        }
        else
        {
            TxtPeriod.Text = "Period: All Transactions";
        }

        TxtGeneratedOn.Text = $"Generated: {DateTime.Now:dd-MMM-yyyy HH:mm}";

        string openingSign = (openingBalanceType == "-" || openingBalanceType == "Cr") ? "-" : "+";
        TxtOpening.Text = Math.Abs(openingBalance) > 0.0001
            ? $"{openingSign} ₹ {Math.Abs(openingBalance):N2}"
            : "₹ 0.00";

        TxtDebits.Text = totalDebit > 0.0001 ? $"+ ₹ {totalDebit:N2}" : "₹ 0.00";
        TxtCredits.Text = totalCredit > 0.0001 ? $"- ₹ {totalCredit:N2}" : "₹ 0.00";

        string closingSign = closingBalance >= 0 ? "+" : "-";
        TxtClosing.Text = Math.Abs(closingBalance) > 0.0001
            ? $"{closingSign} ₹ {Math.Abs(closingBalance):N2}"
            : "₹ 0.00";
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var printDlg = new PrintDialog();
            if (printDlg.ShowDialog() == true)
            {
                printDlg.PrintVisual(PrintArea, "PolyStock Statement of Account");
                MessageBox.Show("Statement of Account sent to printer successfully.", "Print Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error sending to printer: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
