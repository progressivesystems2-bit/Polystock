using System.Windows;
using System.Windows.Controls;
using PolyStock.WPF.ViewModels;

namespace PolyStock.WPF.Views;

public partial class PrintPurchaseWindow : Window
{
    public PrintPurchaseWindow(PurchaseViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        var printDlg = new PrintDialog();
        if (printDlg.ShowDialog() == true)
        {
            printDlg.PrintVisual(PrintArea, $"Purchase Inward Voucher - {((PurchaseViewModel)DataContext).InvoiceNo}");
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
