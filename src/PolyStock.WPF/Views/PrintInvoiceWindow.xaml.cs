using System;
using System.Windows;
using System.Windows.Controls;
using PolyStock.WPF.ViewModels;

namespace PolyStock.WPF.Views;

public partial class PrintInvoiceWindow : Window
{
    public PrintInvoiceWindow(object dataContext)
    {
        InitializeComponent();

        if (dataContext is SalesViewModel salesVM)
        {
            DataContext = InvoicePrintDocumentModel.FromSales(salesVM);
        }
        else if (dataContext is PurchaseViewModel purchaseVM)
        {
            DataContext = InvoicePrintDocumentModel.FromPurchase(purchaseVM);
        }
        else if (dataContext is InvoicePrintDocumentModel printModel)
        {
            DataContext = printModel;
        }
        else
        {
            DataContext = dataContext;
        }
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                printDialog.PrintVisual(PrintArea, $"Invoice - PolyStock");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error printing invoice: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
