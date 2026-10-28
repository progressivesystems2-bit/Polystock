using System.Windows.Controls;
using System.Windows.Input;
using PolyStock.Core.Models;
using PolyStock.WPF.ViewModels;

namespace PolyStock.WPF.Views;

public partial class LedgerReportView : UserControl
{
    public LedgerReportView()
    {
        InitializeComponent();
    }

    private LedgerReportViewModel? ViewModel => DataContext as LedgerReportViewModel;

    private void StatementRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow row && row.DataContext is LedgerStatementItem item)
        {
            e.Handled = true;
            ViewModel?.OpenTransaction(item);
        }
    }

    private void GroupRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow row && row.DataContext is Account acc)
        {
            e.Handled = true;
            ViewModel?.DrillDownToAccount(acc);
        }
    }

    private void StatementDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (StatementDataGrid.SelectedItem is LedgerStatementItem item)
            {
                e.Handled = true;
                ViewModel?.OpenTransaction(item);
            }
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            ViewModel?.BackToGroupReportCommand.Execute(null);
        }
    }

    private void GroupDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (GroupDataGrid.SelectedItem is Account acc)
            {
                e.Handled = true;
                ViewModel?.DrillDownToAccount(acc);
            }
        }
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Global Esc key navigates back from Individual Statement to Group Summary (Tally ERP style)
        if (e.Key == Key.Escape && ViewModel != null && ViewModel.IsIndividualStatementSelected)
        {
            e.Handled = true;
            ViewModel.BackToGroupReportCommand.Execute(null);
        }
    }
}
