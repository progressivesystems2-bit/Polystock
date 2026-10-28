using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using PolyStock.WPF.ViewModels;

namespace PolyStock.WPF;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        PreviewKeyDown += MainWindow_PreviewKeyDown;
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        // Ctrl+F or Ctrl+E: Focus Global Search Box
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (e.Key is Key.F or Key.E)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    GlobalSearchTextBox.Focus();
                    GlobalSearchTextBox.SelectAll();
                    Keyboard.Focus(GlobalSearchTextBox);
                }), DispatcherPriority.Input);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.W)
            {
                vm.CloseActiveTab();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.N)
            {
                if (vm.CurrentViewName.Equals("Sales", StringComparison.OrdinalIgnoreCase))
                {
                    vm.SalesVM.AddNewCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
                if (vm.CurrentViewName.Equals("Purchase", StringComparison.OrdinalIgnoreCase))
                {
                    vm.PurchaseVM.AddNewCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
            }
        }

        // When Global Search Popup is open, handle keyboard navigation
        if (vm.IsGlobalSearchOpen)
        {
            if (e.Key == Key.Down)
            {
                vm.SelectNextSearchResult();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Up)
            {
                vm.SelectPreviousSearchResult();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                if (vm.SelectedSearchResult != null)
                {
                    vm.OpenGlobalSearchResultCommand.Execute(vm.SelectedSearchResult);
                    e.Handled = true;
                    return;
                }
            }
            if (e.Key == Key.Escape)
            {
                vm.CloseGlobalSearch();
                e.Handled = true;
                return;
            }
        }
    }

    private void GlobalSearchResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GlobalSearchResultsList.SelectedItem != null)
        {
            GlobalSearchResultsList.ScrollIntoView(GlobalSearchResultsList.SelectedItem);
        }
    }

    private void GlobalSearchResultItem_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is GlobalSearchResultViewModel result && DataContext is MainViewModel vm)
        {
            vm.SelectedSearchResult = result;
        }
    }
}