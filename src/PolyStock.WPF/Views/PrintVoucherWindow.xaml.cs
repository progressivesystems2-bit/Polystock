using System;
using System.Windows;
using System.Windows.Controls;
using PolyStock.Core.Models;

namespace PolyStock.WPF.Views;

public partial class PrintVoucherWindow : Window
{
    public PrintVoucherWindow(FinancialVoucher voucher)
    {
        InitializeComponent();
        DataContext = voucher;
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var printDlg = new PrintDialog();
            if (printDlg.ShowDialog() == true)
            {
                printDlg.PrintVisual(PrintArea, "PolyStock Accounting Voucher");
                MessageBox.Show("Voucher slip sent to printer successfully.", "Print Complete", MessageBoxButton.OK, MessageBoxImage.Information);
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
