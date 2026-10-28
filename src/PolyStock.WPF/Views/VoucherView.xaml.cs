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

public partial class VoucherView : UserControl
{
    public VoucherView()
    {
        InitializeComponent();
    }

    private VoucherViewModel? ViewModel => DataContext as VoucherViewModel;

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        FocusInitialControl();
    }

    private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            FocusInitialControl();
        }
    }

    private void FocusInitialControl()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            NewBtn?.Focus();
            Keyboard.Focus(NewBtn);
        }), DispatcherPriority.Input);
    }

    private void ModePill_Click(object sender, RoutedEventArgs e)
    {
        FocusInitialControl();
    }

    private void NewBtn_Click(object sender, RoutedEventArgs e)
    {
        FocusFormFirstControl();
    }

    private void NewBtn_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ViewModel?.NewCommand.Execute(null);
            FocusFormFirstControl();
        }
        else if (e.Key == Key.Tab && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            e.Handled = true;
            FocusFormFirstControl();
        }
    }

    private void FocusFormFirstControl()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (VoucherDatePicker != null)
            {
                var dtBox = FindChild<TextBox>(VoucherDatePicker);
                if (dtBox != null)
                {
                    dtBox.Focus();
                    Keyboard.Focus(dtBox);
                    dtBox.SelectAll();
                }
                else
                {
                    VoucherDatePicker.Focus();
                    Keyboard.Focus(VoucherDatePicker);
                }
            }
        }), DispatcherPriority.Input);
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 1. GLOBAL FUNCTION KEY SHORTCUTS
        if (e.Key == Key.F5)
        {
            e.Handled = true;
            ViewModel?.SetPaymentModeCommand.Execute(null);
            FocusInitialControl();
            return;
        }
        if (e.Key == Key.F6)
        {
            e.Handled = true;
            ViewModel?.SetReceiptModeCommand.Execute(null);
            FocusInitialControl();
            return;
        }
        if (e.Key == Key.F7)
        {
            e.Handled = true;
            ViewModel?.SetJournalModeCommand.Execute(null);
            FocusInitialControl();
            return;
        }

        // 2. GLOBAL ACCELERATOR SHORTCUTS (Ctrl+A / Ctrl+S / Ctrl+N / Ctrl+P / Ctrl+D)
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
                ViewModel?.NewCommand.Execute(null);
                FocusFormFirstControl();
                return;
            }
            if (e.Key == Key.P)
            {
                e.Handled = true;
                ViewModel?.PrintCommand.Execute(null);
                return;
            }
            if (e.Key == Key.D && ViewModel?.IsEditing == true)
            {
                e.Handled = true;
                ViewModel?.DeleteCommand.Execute(null);
                return;
            }
        }
    }

    private void ParticularsGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl+Delete in Particulars Grid removes the currently focused row
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && (e.Key == Key.Delete || e.Key == Key.Back))
        {
            var focused = Keyboard.FocusedElement as DependencyObject;
            var row = FindAncestor<DataGridRow>(focused);
            if (row?.DataContext is VoucherLineItemViewModel item && ViewModel != null)
            {
                e.Handled = true;
                ViewModel.RemoveLineItem(item);
            }
        }
    }

    // --- FIELD PROGRESSION HANDLERS (ENTER KEY) ---

    private void VoucherDatePicker_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            PaymentModeCombo?.Focus();
        }
    }

    private void PaymentModeCombo_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ReferenceNoBox?.Focus();
            ReferenceNoBox?.SelectAll();
        }
    }

    private void ReferenceNoBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (ViewModel != null && ViewModel.IsSingleEntryMode)
            {
                CashBankCombo?.Focus();
            }
            else
            {
                DrAccountCombo?.Focus();
            }
        }
    }

    private void CashBankCombo_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (ViewModel != null)
            {
                TryResolveComboSelection(CashBankCombo, ViewModel.CashBankAccounts);
            }
            FocusRowControl(0, "LineAccountCombo");
        }
    }

    private void DrAccountCombo_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (ViewModel != null)
            {
                TryResolveComboSelection(DrAccountCombo, ViewModel.AllAccounts);
            }
            CrAccountCombo?.Focus();
        }
    }

    private void CrAccountCombo_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (ViewModel != null)
            {
                TryResolveComboSelection(CrAccountCombo, ViewModel.AllAccounts);
            }
            FocusRowControl(0, "LineAccountCombo");
        }
    }

    private void LineAccountCombo_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || (e.Key == Key.Tab && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)))
        {
            if (sender is ComboBox cb && cb.DataContext is VoucherLineItemViewModel item && ViewModel != null)
            {
                if (cb.IsDropDownOpen) cb.IsDropDownOpen = false;
                TryResolveComboSelection(cb, ViewModel.AllAccounts);

                // Tally Flow: If account is empty on Enter or Tab, skip out of particulars into Narration!
                if (item.SelectedAccount == null && string.IsNullOrWhiteSpace(cb.Text))
                {
                    e.Handled = true;
                    NarrationBox?.Focus();
                    NarrationBox?.SelectAll();
                    return;
                }

                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    int idx = ParticularsGrid.Items.IndexOf(item);
                    if (idx >= 0)
                    {
                        FocusRowControl(idx, "LineAmountBox");
                    }
                }
            }
        }
    }

    private void LineAccountCombo_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is ComboBox cb && ViewModel != null)
        {
            TryResolveComboSelection(cb, ViewModel.AllAccounts);
        }
    }

    private void LineAmountBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (sender is TextBox tb && tb.DataContext is VoucherLineItemViewModel item && ViewModel != null)
            {
                item.FormatAmount();
                int idx = ParticularsGrid.Items.IndexOf(item);
                if (idx >= 0)
                {
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                    {
                        // Shift+Enter fast-skips Ref & Remarks directly to next row's Account
                        ViewModel.EnsureTrailingEmptyRow();
                        FocusRowControl(idx + 1, "LineAccountCombo");
                    }
                    else
                    {
                        FocusRowControl(idx, "LineRefBox");
                    }
                }
            }
        }
        else if (e.Key == Key.Tab)
        {
            if (sender is TextBox tb && tb.DataContext is VoucherLineItemViewModel item)
            {
                item.FormatAmount();
            }
        }
    }

    private void LineTextControl_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.SelectAll();
        }
    }

    private void LineAmountBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is VoucherLineItemViewModel item)
        {
            item.FormatAmount();
        }
    }

    private void LineTextControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBox tb && !tb.IsKeyboardFocusWithin)
        {
            e.Handled = true;
            tb.Focus();
            Keyboard.Focus(tb);
            tb.SelectAll();
        }
    }

    private void LineRefBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (sender is TextBox tb && tb.DataContext is VoucherLineItemViewModel item && ViewModel != null)
            {
                int idx = ParticularsGrid.Items.IndexOf(item);
                if (idx >= 0)
                {
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                    {
                        // Shift+Enter fast-skips Remarks directly to next row's Account
                        ViewModel.EnsureTrailingEmptyRow();
                        FocusRowControl(idx + 1, "LineAccountCombo");
                    }
                    else
                    {
                        FocusRowControl(idx, "LineRemarksBox");
                    }
                }
            }
        }
    }

    private void LineRemarksBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || (e.Key == Key.Tab && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)))
        {
            e.Handled = true;
            if (sender is TextBox tb && tb.DataContext is VoucherLineItemViewModel item && ViewModel != null)
            {
                int idx = ParticularsGrid.Items.IndexOf(item);
                ViewModel.EnsureTrailingEmptyRow();
                int nextIdx = idx + 1;
                FocusRowControl(nextIdx, "LineAccountCombo");
            }
        }
    }

    private void NarrationBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Enter without Shift saves voucher instantly (Tally standard)
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            e.Handled = true;
            CommitFocusedElement();
            ViewModel?.SaveCommand.Execute(null);
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

    private void TryResolveComboSelection(ComboBox combo, IEnumerable<Account>? accounts)
    {
        if (accounts == null) return;

        if (combo.SelectedItem == null && !string.IsNullOrWhiteSpace(combo.Text))
        {
            string txt = combo.Text.Trim();
            var match = accounts.FirstOrDefault(a => a.AccountName.Equals(txt, StringComparison.OrdinalIgnoreCase))
                     ?? accounts.FirstOrDefault(a => a.AccountName.StartsWith(txt, StringComparison.OrdinalIgnoreCase))
                     ?? accounts.FirstOrDefault(a => a.AccountName.Contains(txt, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                combo.SelectedItem = match;
                if (combo.DataContext is VoucherLineItemViewModel item)
                {
                    item.SelectedAccount = match;
                }
                else if (combo == CashBankCombo && ViewModel != null)
                {
                    ViewModel.SelectedCashBankAccount = match;
                }
                else if (combo == DrAccountCombo && ViewModel != null)
                {
                    ViewModel.SelectedDrAccount = match;
                }
                else if (combo == CrAccountCombo && ViewModel != null)
                {
                    ViewModel.SelectedCrAccount = match;
                }
            }
        }
    }

    private void FocusRowControl(int rowIndex, string controlName)
    {
        ParticularsGrid.UpdateLayout();
        if (rowIndex < 0 || rowIndex >= ParticularsGrid.Items.Count) return;

        var item = ParticularsGrid.Items[rowIndex];
        ParticularsGrid.ScrollIntoView(item);

        Action focusAction = () =>
        {
            var row = ParticularsGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex) as DataGridRow;
            if (row != null)
            {
                var ctrl = FindChild<FrameworkElement>(row, controlName);
                if (ctrl != null)
                {
                    if (ctrl is ComboBox cb && cb.IsEditable)
                    {
                        var innerTb = FindChild<TextBox>(cb, "PART_EditableTextBox");
                        if (innerTb != null)
                        {
                            innerTb.Focus();
                            Keyboard.Focus(innerTb);
                            innerTb.SelectAll();
                        }
                        else
                        {
                            cb.Focus();
                            Keyboard.Focus(cb);
                        }
                    }
                    else if (ctrl is TextBox tb)
                    {
                        tb.Focus();
                        Keyboard.Focus(tb);
                        tb.SelectAll();
                    }
                    else
                    {
                        ctrl.Focus();
                        Keyboard.Focus(ctrl);
                    }
                }
            }
        };

        var existingRow = ParticularsGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex) as DataGridRow;
        if (existingRow == null)
        {
            Dispatcher.BeginInvoke(focusAction, DispatcherPriority.Loaded);
        }
        else
        {
            Dispatcher.BeginInvoke(focusAction, DispatcherPriority.Input);
        }
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private static T? FindChild<T>(DependencyObject? parent, string? childName = null) where T : DependencyObject
    {
        if (parent == null) return null;
        int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childrenCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                if (string.IsNullOrEmpty(childName) || (child is FrameworkElement fe && fe.Name == childName))
                {
                    return typedChild;
                }
            }
            var foundChild = FindChild<T>(child, childName);
            if (foundChild != null) return foundChild;
        }
        return null;
    }
}
