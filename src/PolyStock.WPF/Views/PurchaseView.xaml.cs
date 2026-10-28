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

public partial class PurchaseView : UserControl
{
    public PurchaseView()
    {
        InitializeComponent();
    }

    private PurchaseViewModel? ViewModel => DataContext as PurchaseViewModel;

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.RequestFocusPartyAction = () =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    SupplierCombo?.Focus();
                    if (SupplierCombo != null)
                    {
                        var innerTb = FindChild<TextBox>(SupplierCombo, "PART_EditableTextBox");
                        innerTb?.SelectAll();
                        if (innerTb != null)
                        {
                            Keyboard.Focus(innerTb);
                        }
                        else
                        {
                            Keyboard.Focus(SupplierCombo);
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
            PurchaseDatePicker?.Focus();
            return;
        }
        if (e.Key == Key.F3)
        {
            e.Handled = true;
            SupplierCombo?.Focus();
            if (SupplierCombo != null)
            {
                var innerTb = FindChild<TextBox>(SupplierCombo, "PART_EditableTextBox");
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

    private void PurchaseGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl+Delete or Ctrl+Backspace removes currently focused item line
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && (e.Key == Key.Delete || e.Key == Key.Back))
        {
            var focused = Keyboard.FocusedElement as DependencyObject;
            var row = FindAncestor<DataGridRow>(focused);
            if (row?.DataContext is PurchaseRowViewModel item && ViewModel != null)
            {
                e.Handled = true;
                ViewModel.RemoveLine(item);
            }
        }
    }

    // --- HEADER SEQUENTIAL PROGRESSION (ENTER KEY) ---

    private void SupplierCombo_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            TryResolveSupplierSelection(SupplierCombo);
            GodownCombo?.Focus();
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
            PurchaseDatePicker?.Focus();
        }
    }

    private void PurchaseDatePicker_PreviewKeyDown(object sender, KeyEventArgs e)
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
        if (sender is FrameworkElement fe && fe.DataContext is PurchaseRowViewModel row && ViewModel != null)
        {
            if (e.Item != null && row.Item != e.Item)
            {
                row.Item = e.Item;
            }
            else if (e.IsEmpty && row.Item != null)
            {
                row.Item = null;
            }

            if (e.IsEmpty || (row.Item == null && e.Item == null))
            {
                GstPercentBox?.Focus();
                GstPercentBox?.SelectAll();
                return;
            }

            int idx = PurchaseGrid.Items.IndexOf(row);
            if (idx >= 0)
            {
                FocusRowControl(idx, "LinePackSizeBox");
            }
        }
    }

    private void LineItemCombo_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (sender is ComboBox cb && cb.DataContext is PurchaseRowViewModel row && ViewModel != null)
            {
                TryResolveItemSelection(cb, row);

                // If no item selected on empty line, signal finished entering items -> jump to Bottom Expenses!
                if (row.Item == null && string.IsNullOrWhiteSpace(cb.Text))
                {
                    GstPercentBox?.Focus();
                    GstPercentBox?.SelectAll();
                    return;
                }

                int idx = PurchaseGrid.Items.IndexOf(row);
                if (idx >= 0)
                {
                    FocusRowControl(idx, "LinePackSizeBox");
                }
            }
        }
    }

    private void LineGodownCombo_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (sender is ComboBox cb && cb.DataContext is PurchaseRowViewModel row)
            {
                int idx = PurchaseGrid.Items.IndexOf(row);
                if (idx >= 0)
                {
                    FocusRowControl(idx, "LinePackSizeBox");
                }
            }
        }
    }

    private void LinePackSizeBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (sender is TextBox tb && tb.DataContext is PurchaseRowViewModel row)
            {
                int idx = PurchaseGrid.Items.IndexOf(row);
                if (idx >= 0)
                {
                    FocusRowControl(idx, "LineBagsBox");
                }
            }
        }
    }

    private void LineBagsBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (sender is TextBox tb && tb.DataContext is PurchaseRowViewModel row)
            {
                int idx = PurchaseGrid.Items.IndexOf(row);
                if (idx >= 0)
                {
                    FocusRowControl(idx, "LineRateBox");
                }
            }
        }
    }

    private void LineRateBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (sender is TextBox tb && tb.DataContext is PurchaseRowViewModel row && ViewModel != null)
            {
                int idx = PurchaseGrid.Items.IndexOf(row);
                ViewModel.EnsureTrailingEmptyRow();
                int nextIdx = idx + 1;
                FocusRowControl(nextIdx, "LineItemCombo");
            }
        }
    }

    // --- BOTTOM EXPENSES & CHARGES (ENTER KEY) ---

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

    private void TryResolveSupplierSelection(ComboBox combo)
    {
        if (ViewModel == null) return;

        if (combo.SelectedItem == null && !string.IsNullOrWhiteSpace(combo.Text))
        {
            string txt = combo.Text.Trim();
            var match = ViewModel.AllAccounts.FirstOrDefault(a => a.AccountName.Equals(txt, StringComparison.OrdinalIgnoreCase))
                     ?? ViewModel.AllAccounts.FirstOrDefault(a => a.AccountName.StartsWith(txt, StringComparison.OrdinalIgnoreCase))
                     ?? ViewModel.AllAccounts.FirstOrDefault(a => a.AccountName.Contains(txt, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                combo.SelectedItem = match;
                ViewModel.SupplierName = match.AccountName;
            }
        }
    }

    private void TryResolveItemSelection(ComboBox combo, PurchaseRowViewModel row)
    {
        if (ViewModel == null) return;

        if (row.Item == null && !string.IsNullOrWhiteSpace(combo.Text))
        {
            string txt = combo.Text.Trim();
            var match = ViewModel.AllItems.FirstOrDefault(i => i.ItemName.Equals(txt, StringComparison.OrdinalIgnoreCase))
                     ?? ViewModel.AllItems.FirstOrDefault(i => i.ItemName.StartsWith(txt, StringComparison.OrdinalIgnoreCase))
                     ?? ViewModel.AllItems.FirstOrDefault(i => i.ItemName.Contains(txt, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                combo.SelectedItem = match;
                row.Item = match;
            }
        }
    }

    private void FocusRowControl(int rowIndex, string controlName)
    {
        PurchaseGrid.UpdateLayout();
        if (rowIndex < 0 || rowIndex >= PurchaseGrid.Items.Count) return;

        var item = PurchaseGrid.Items[rowIndex];
        PurchaseGrid.ScrollIntoView(item);

        Action focusAction = () =>
        {
            var row = PurchaseGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex) as DataGridRow;
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

        var existingRow = PurchaseGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex) as DataGridRow;
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
