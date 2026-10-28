using System.Windows;
using System.Windows.Controls;
using PolyStock.WPF.ViewModels;

namespace PolyStock.WPF.Views;

public partial class PrintProfitLossWindow : Window
{
    public PrintProfitLossWindow(ProfitLossReportViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        var printDlg = new PrintDialog();
        if (printDlg.ShowDialog() == true)
        {
            printDlg.PrintVisual(PrintArea, "PolyStock - Profit & Loss (P/L) Trading Analysis Report");
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
