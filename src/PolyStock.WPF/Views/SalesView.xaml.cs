using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PolyStock.Core.Models;
using PolyStock.WPF.ViewModels;

namespace PolyStock.WPF.Views;

public partial class SalesView : UserControl
{
    public SalesView()
    {
        InitializeComponent();
    }

    private SalesViewModel? ViewModel => DataContext as SalesViewModel;

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.RequestFocusPartyAction = () =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    CustomerCombo?.Focus();
                    if (CustomerCombo != null)
                    {
                        var innerTb = FindChild<TextBox>(CustomerCombo, "PART_EditableTextBox");
                        innerTb?.SelectAll();
                        if (innerTb != null)
                        {
                            Keyboard.Focus(innerTb);
                        }
                        else
                        {
                            Keyboard.Focus(CustomerCombo);
                        }
                    }
                }), DispatcherPriority.Input);
            };
        }
        FocusInitialControl();
    }

    private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            FocusInitialControl();
        }
    }

    public void FocusInitialControl()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (AddNewButton != null && AddNewButton.IsVisible)
            {
                AddNewButton.Focus();
                Keyboard.Focus(AddNewButton);
            }
        }), DispatcherPriority.Input);
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 1. Function Key Shortcuts
        if (e.Key == Key.F2)
        {
            e.Handled = true;
            SaleDatePicker?.Focus();
            return;
        }
        if (e.Key == Key.F3)
        {
            e.Handled = true;
            CustomerCombo?.Focus();
            if (CustomerCombo != null)
            {
                var innerTb = FindChild<TextBox>(CustomerCombo, "PART_EditableTextBox");
                innerTb?.SelectAll();
            }
            return;
        }
        if (e.Key == Key.F4)
        {
            e.Handled = true;
            FocusRowControl(0, "LineItemCombo");
            return;
        }
        if (e.Key == Key.F7)
        {
            e.Handled = true;
            GstPercentBox?.Focus();
            GstPercentBox?.SelectAll();
            return;
        }
        if (e.Key == Key.F8)
        {
            e.Handled = true;
            CommitFocusedElement();
            ViewModel?.SaveCommand.Execute(null);
            return;
        }
        if (e.Key == Key.F9)
        {
            e.Handled = true;
            CommitFocusedElement();
            ViewModel?.PrintCommand.Execute(null);
            return;
        }
        if (e.Key == Key.Escape)
        {
            if (ViewModel?.IsFormActive == true)
            {
                e.Handled = true;
                ViewModel.CancelCommand.Execute(null);
                FocusInitialControl();
                return;
            }
            if (HandleEscapeKey())
            {
                e.Handled = true;
                return;
            }
        }

        // 2. Global Accelerator Shortcuts
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (e.Key == Key.A || e.Key == Key.S)
            {
                e.Handled = true;
                CommitFocusedElement();
                ViewModel?.SaveCommand.Execute(null);
                return;
            }
            if (e.Key == Key.N)
            {
                e.Handled = true;
                ViewModel?.AddNewCommand.Execute(null);
                return;
            }
            if (e.Key == Key.M)
            {
                if (ViewModel?.CanModify == true)
                {
                    e.Handled = true;
                    ViewModel.ModifyCommand.Execute(null);
                }
                return;
            }
            if (e.Key == Key.P)
            {
                e.Handled = true;
                CommitFocusedElement();
                ViewModel?.PrintCommand.Execute(null);
                return;
            }
            if (e.Key == Key.L)
            {
                e.Handled = true;
                ViewModel?.ListCommand.Execute(null);
                return;
            }
        }
    }

    private void SalesGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl+Delete or Ctrl+Backspace or Delete removes currently focused item line
        if ((Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && (e.Key == Key.Delete || e.Key == Key.Back)) ||
            (e.Key == Key.Delete && Keyboard.FocusedElement is DataGridRow))
        {
            var focused = Keyboard.FocusedElement as DependencyObject;
            var row = FindAncestor<DataGridRow>(focused);
            if (row?.DataContext is SaleRowViewModel item && ViewModel != null)
            {
                e.Handled = true;
                ViewModel.RemoveLine(item);
            }
        }
    }

    // --- HEADER SEQUENTIAL PROGRESSION (ENTER KEY) ---

    private void CustomerCombo_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            TryResolveAccountSelection(CustomerCombo);
            // Streamlined header flow: If godown is already selected, jump directly to first item line!
            // (If user wants to edit Godown, Date, or Notes, Tab key moves normally).
            if (ViewModel?.SelectedGodown != null)
            {
                FocusRowControl(0, "LineItemCombo");
            }
            else
            {
                GodownCombo?.Focus();
            }
        }
    }

    private void GodownCombo_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            InvoiceNoBox?.Focus();
            InvoiceNoBox?.SelectAll();
        }
    }

    private void InvoiceNoBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            SaleDatePicker?.Focus();
        }
    }

    private void SaleDatePicker_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            NotesBox?.Focus();
            NotesBox?.SelectAll();
        }
    }

    private void NotesBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            FocusRowControl(0, "LineItemCombo");
        }
    }

    // --- LINE ITEMS GRID PROGRESSION (ENTER KEY) ---

    private void LineItemBox_ItemCommitted(object sender, Controls.ItemCommittedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is SaleRowViewModel row && ViewModel != null)
        {
            if (e.Item != null && row.Item != e.Item)
            {
                row.Item = e.Item;
            }
            else if (e.IsEmpty && row.Item != null)
            {
                row.Item = null;
            }

            // If empty item committed on empty line, signal finished entering items -> jump to Bottom Charges!
            if (e.IsEmpty || (row.Item == null && e.Item == null))
            {
                GstPercentBox?.Focus();
                GstPercentBox?.SelectAll();
                return;
            }

            int idx = SalesGrid.Items.IndexOf(row);
            if (idx >= 0)
            {
                // Rapid line-item flow: Pack size and Godown are already auto-selected by default.
                // Jump straight to Bags (Full)!
                FocusRowControl(idx, "LineBagsBox");
            }
        }
    }

    private void LineItemCombo_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (sender is ComboBox cb && cb.DataContext is SaleRowViewModel row && ViewModel != null)
            {
                TryResolveItemSelection(cb, row);

                // If no item selected on empty line, signal finished entering items -> jump to Bottom Charges!
                if (row.Item == null && string.IsNullOrWhiteSpace(cb.Text))
                {
                    GstPercentBox?.Focus();
                    GstPercentBox?.SelectAll();
                    return;
                }

                int idx = SalesGrid.Items.IndexOf(row);
                if (idx >= 0)
                {
                    // Rapid line-item flow: Pack size and Godown are already auto-selected by default.
                    // Jump straight to Bags (Full)!
                    FocusRowControl(idx, "LineBagsBox");
                }
            }
        }
        else if (e.Key == Key.Down || e.Key == Key.Up)
        {
            if (sender is ComboBox cb && !cb.IsDropDownOpen)
            {
                if (NavigateRowVertically(sender, e.Key == Key.Down ? 1 : -1, "LineItemCombo"))
                {
                    e.Handled = true;
                }
            }
        }
    }

    private void LineGodownCombo_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (sender is ComboBox cb && cb.DataContext is SaleRowViewModel row)
            {
                int idx = SalesGrid.Items.IndexOf(row);
                if (idx >= 0)
                {
                    FocusRowControl(idx, "LinePackSizeCombo");
                }
            }
        }
        else if (e.Key == Key.Down || e.Key == Key.Up)
        {
            if (sender is ComboBox cb && !cb.IsDropDownOpen)
            {
                if (NavigateRowVertically(sender, e.Key == Key.Down ? 1 : -1, "LineGodownCombo"))
                {
                    e.Handled = true;
                }
            }
        }
    }

    private void LinePackSizeCombo_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (sender is ComboBox cb && cb.DataContext is SaleRowViewModel row)
            {
                int idx = SalesGrid.Items.IndexOf(row);
                if (idx >= 0)
                {
                    FocusRowControl(idx, "LineBagsBox");
                }
            }
        }
        else if (e.Key == Key.Down || e.Key == Key.Up)
        {
            if (sender is ComboBox cb && !cb.IsDropDownOpen)
            {
                if (NavigateRowVertically(sender, e.Key == Key.Down ? 1 : -1, "LinePackSizeCombo"))
                {
                    e.Handled = true;
                }
            }
        }
    }

    private void LineBagsBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (sender is TextBox tb && tb.DataContext is SaleRowViewModel row)
            {
                int idx = SalesGrid.Items.IndexOf(row);
                if (idx >= 0)
                {
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                    {
                        // Shift+Enter explicitly visits Loose (kg) for cut bags
                        FocusRowControl(idx, "LineLooseBox");
                    }
                    else
                    {
                        // Rapid flow: Enter moves directly to Rate / kg
                        FocusRowControl(idx, "LineRateBox");
                    }
                }
            }
        }
        else if (e.Key == Key.Down || e.Key == Key.Up)
        {
            if (NavigateRowVertically(sender, e.Key == Key.Down ? 1 : -1, "LineBagsBox"))
            {
                e.Handled = true;
            }
        }
    }

    private void LineLooseBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (sender is TextBox tb && tb.DataContext is SaleRowViewModel row)
            {
                int idx = SalesGrid.Items.IndexOf(row);
                if (idx >= 0)
                {
                    FocusRowControl(idx, "LineRateBox");
                }
            }
        }
        else if (e.Key == Key.Down || e.Key == Key.Up)
        {
            if (NavigateRowVertically(sender, e.Key == Key.Down ? 1 : -1, "LineLooseBox"))
            {
                e.Handled = true;
            }
        }
    }

    private void LineRateBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (sender is TextBox tb && tb.DataContext is SaleRowViewModel row && ViewModel != null)
            {
                var binding = BindingOperations.GetBindingExpression(tb, TextBox.TextProperty);
                binding?.UpdateSource();

                int idx = SalesGrid.Items.IndexOf(row);
                ViewModel.EnsureTrailingEmptyRow();
                int nextIdx = idx + 1;
                FocusRowControl(nextIdx, "LineItemCombo");
            }
        }
        else if (e.Key == Key.Down || e.Key == Key.Up)
        {
            if (NavigateRowVertically(sender, e.Key == Key.Down ? 1 : -1, "LineRateBox"))
            {
                e.Handled = true;
            }
        }
    }

    // --- BOTTOM CHARGES & TAXES (ENTER KEY) ---

    private void GstPercentBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (ViewModel != null && ViewModel.GstPercent > 0.0001)
            {
                FreightBox?.Focus();
                FreightBox?.SelectAll();
            }
            else
            {
                GstAmountBox?.Focus();
                GstAmountBox?.SelectAll();
            }
        }
    }

    private void GstAmountBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            FreightBox?.Focus();
            FreightBox?.SelectAll();
        }
    }

    private void FreightBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            LoadingUnloadingBox?.Focus();
            LoadingUnloadingBox?.SelectAll();
        }
    }

    private void LoadingUnloadingBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            QuickSaveButton?.Focus();
        }
    }

    // --- HELPER UTILITIES ---

    private void CommitFocusedElement()
    {
        if (Keyboard.FocusedElement is TextBox tb)
        {
            var binding = BindingOperations.GetBindingExpression(tb, TextBox.TextProperty);
            binding?.UpdateSource();
        }
        else if (Keyboard.FocusedElement is ComboBox cb)
        {
            var binding = BindingOperations.GetBindingExpression(cb, ComboBox.TextProperty);
            binding?.UpdateSource();
        }
    }

    private void TryResolveAccountSelection(ComboBox combo)
    {
        if (ViewModel == null) return;

        if (combo.SelectedItem == null && !string.IsNullOrWhiteSpace(combo.Text))
        {
            string txt = combo.Text.Trim();
            var match = ViewModel.AllAccounts.FirstOrDefault(a => a.AccountName.Equals(txt, StringComparison.OrdinalIgnoreCase))
                     ?? ViewModel.AllAccounts.FirstOrDefault(a => a.AccountName.StartsWith(txt, StringComparison.OrdinalIgnoreCase))
                     ?? ViewModel.AllAccounts.FirstOrDefault(a => a.AccountName.Contains(txt, StringComparison.OrdinalIgnoreCase))
                     ?? ViewModel.AllAccounts.FirstOrDefault(a => !string.IsNullOrEmpty(a.Mobile) && a.Mobile.Contains(txt))
                     ?? ViewModel.AllAccounts.FirstOrDefault(a => !string.IsNullOrEmpty(a.GSTIN) && a.GSTIN.StartsWith(txt, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                combo.SelectedItem = match;
                ViewModel.CustomerName = match.AccountName;
            }
        }
    }

    private void TryResolveItemSelection(ComboBox combo, SaleRowViewModel row)
    {
        if (ViewModel == null) return;

        if (row.Item == null && !string.IsNullOrWhiteSpace(combo.Text))
        {
            string txt = combo.Text.Trim();
            var match = ViewModel.AllItems.FirstOrDefault(i => i.ItemName.Equals(txt, StringComparison.OrdinalIgnoreCase))
                     ?? ViewModel.AllItems.FirstOrDefault(i => !string.IsNullOrEmpty(i.ItemCode) && i.ItemCode.Equals(txt, StringComparison.OrdinalIgnoreCase))
                     ?? ViewModel.AllItems.FirstOrDefault(i => i.ItemName.StartsWith(txt, StringComparison.OrdinalIgnoreCase))
                     ?? ViewModel.AllItems.FirstOrDefault(i => i.ItemName.Contains(txt, StringComparison.OrdinalIgnoreCase))
                     ?? ViewModel.AllItems.FirstOrDefault(i => !string.IsNullOrEmpty(i.Description) && i.Description.Contains(txt, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                combo.SelectedItem = match;
                row.Item = match;
            }
        }
    }

    private bool NavigateRowVertically(object sender, int delta, string controlName)
    {
        if (sender is DependencyObject d)
        {
            var row = FindAncestor<DataGridRow>(d);
            if (row != null)
            {
                int currentIdx = SalesGrid.ItemContainerGenerator.IndexFromContainer(row);
                if (currentIdx >= 0)
                {
                    int targetIdx = currentIdx + delta;
                    if (targetIdx >= 0 && targetIdx < SalesGrid.Items.Count)
                    {
                        FocusRowControl(targetIdx, controlName);
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private bool HandleEscapeKey()
    {
        var focused = Keyboard.FocusedElement as DependencyObject;
        if (focused != null)
        {
            var row = FindAncestor<DataGridRow>(focused);
            if (row?.DataContext is SaleRowViewModel item && ViewModel != null)
            {
                // If the line has some partial data, clear/reset the active line
                if (item.Item != null || item.BagsSold > 0 || item.LooseKgSold > 0)
                {
                    item.Item = null;
                    item.BagsSold = 0;
                    item.LooseKgSold = 0;
                    item.RatePerKg = 0;
                    item.Recalculate();
                    ViewModel.RecalculateTotals();
                    int idx = SalesGrid.Items.IndexOf(item);
                    if (idx >= 0) FocusRowControl(idx, "LineItemCombo");
                    return true;
                }
                else if (SalesGrid.Items.Count > 1)
                {
                    // If row is already empty and there are other rows, delete it
                    ViewModel.RemoveLine(item);
                    int prevIdx = Math.Max(0, SalesGrid.Items.Count - 1);
                    FocusRowControl(prevIdx, "LineItemCombo");
                    return true;
                }
            }
        }
        return false;
    }

    private void FocusRowControl(int rowIndex, string controlName)
    {
        SalesGrid.UpdateLayout();
        if (rowIndex < 0 || rowIndex >= SalesGrid.Items.Count) return;

        var item = SalesGrid.Items[rowIndex];
        SalesGrid.ScrollIntoView(item);

        Action focusAction = () =>
        {
            var row = SalesGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex) as DataGridRow;
            if (row != null)
            {
                var ctrl = FindChild<FrameworkElement>(row, controlName);
                if (ctrl != null)
                {
                    ctrl.Focus();
                    if (ctrl is Controls.SearchableItemBox sib)
                    {
                        sib.FocusInput();
                    }
                    else if (ctrl is TextBox tb)
                    {
                        tb.SelectAll();
                    }
                    else if (ctrl is ComboBox cb && cb.IsEditable)
                    {
                        var innerTb = FindChild<TextBox>(cb, "PART_EditableTextBox");
                        innerTb?.SelectAll();
                    }
                }
            }
        };

        var existingRow = SalesGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex) as DataGridRow;
        if (existingRow == null)
        {
            Dispatcher.BeginInvoke(focusAction, DispatcherPriority.Loaded);
        }
        else
        {
            Dispatcher.BeginInvoke(focusAction, DispatcherPriority.Input);
        }
    }

    private static T? FindChild<T>(DependencyObject parent, string? childName = null) where T : DependencyObject
    {
        if (parent == null) return null;
        int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childrenCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                if (string.IsNullOrEmpty(childName)) return typedChild;
                if (child is FrameworkElement fe && fe.Name == childName) return typedChild;
            }
            var result = FindChild<T>(child, childName);
            if (result != null) return result;
        }
        return null;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T target) return target;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
