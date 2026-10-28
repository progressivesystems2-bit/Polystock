using System.Windows;
using System.Windows.Controls;
using PolyStock.WPF.ViewModels;

namespace PolyStock.WPF.Views;

public partial class PrintStockReportWindow : Window
{
    public PrintStockReportWindow(StockRegisterViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        var printDlg = new PrintDialog();
        if (printDlg.ShowDialog() == true)
        {
            printDlg.PrintVisual(PrintArea, "PolyStock - Live Inventory Position Report");
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
