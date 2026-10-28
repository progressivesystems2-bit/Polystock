using System;
using System.Linq;
using PolyStock.Data;
using PolyStock.Services;
using PolyStock.WPF.ViewModels;
using Xunit;

namespace PolyStock.Tests;

public class ShortcutsViewModelTests
{
    [Fact]
    public void ShortcutsViewModel_InitializesWithAllShortcuts()
    {
        var vm = new ShortcutsViewModel();

        Assert.NotEmpty(vm.FilteredShortcuts);
        Assert.Contains(vm.FilteredShortcuts, s => s.Key == "F1");
        Assert.Contains(vm.FilteredShortcuts, s => s.Key == "F8");
        Assert.Contains(vm.FilteredShortcuts, s => s.Key == "F5");
        Assert.Contains(vm.FilteredShortcuts, s => s.Category == "Sales");
        Assert.Contains(vm.FilteredShortcuts, s => s.Category == "Purchase");
        Assert.Contains(vm.FilteredShortcuts, s => s.Category == "Vouchers");
    }

    [Fact]
    public void ShortcutsViewModel_FiltersByCategory()
    {
        var vm = new ShortcutsViewModel();
        vm.SelectedCategory = "Sales";

        Assert.All(vm.FilteredShortcuts, s => Assert.Equal("Sales", s.Category, ignoreCase: true));
        Assert.Contains(vm.FilteredShortcuts, s => s.Key == "F8");
    }

    [Fact]
    public void ShortcutsViewModel_FiltersBySearchText()
    {
        var vm = new ShortcutsViewModel();
        vm.SearchText = "F8";

        Assert.NotEmpty(vm.FilteredShortcuts);
        Assert.All(vm.FilteredShortcuts, s => 
            Assert.True(s.Key.Contains("F8", StringComparison.OrdinalIgnoreCase) || 
                        s.Action.Contains("F8", StringComparison.OrdinalIgnoreCase) || 
                        s.Description.Contains("F8", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void ShortcutsViewModel_NavigateCommandTriggersAction()
    {
        string? navigatedPage = null;
        var vm = new ShortcutsViewModel(pageId => navigatedPage = pageId);

        vm.NavigateToPageCommand.Execute("Sales");

        Assert.Equal("Sales", navigatedPage);
    }

    [Fact]
    public void MainViewModel_DynamicShortcutsUpdateWithActivePage()
    {
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PolyStockMainVMTests_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(tempDir);
        var testDbPath = System.IO.Path.Combine(tempDir, "TestPolyStock.accdb");

        try
        {
            DatabaseInitializer.Initialize(testDbPath);
        }
        catch { }

        try
        {
            var service = new InventoryService(testDbPath);
            var mainVm = new MainViewModel(service);

            // Initial view is Dashboard
            Assert.Equal("Dashboard", mainVm.CurrentViewName);
            Assert.Contains("F1 Help", mainVm.CurrentPageShortcutsSummary);

            // Switch to Sales
            mainVm.OpenTab("Sales");
            Assert.Equal("Sales", mainVm.CurrentViewName);
            Assert.Contains("F8 Save", mainVm.CurrentPageShortcutsSummary);
            Assert.Contains("F9 Print", mainVm.CurrentPageShortcutsSummary);
            Assert.Contains("Sales Entry Shortcuts", mainVm.CurrentPageShortcutsTooltip);

            // Switch to Purchase
            mainVm.OpenTab("Purchase");
            Assert.Equal("Purchase", mainVm.CurrentViewName);
            Assert.Contains("Ctrl+A Save", mainVm.CurrentPageShortcutsSummary);

            // Switch to Vouchers
            mainVm.OpenTab("Vouchers");
            Assert.Equal("Vouchers", mainVm.CurrentViewName);
            Assert.Contains("F5 Pay", mainVm.CurrentPageShortcutsSummary);

            // Open Shortcuts tab via OpenShortcutsCommand
            mainVm.OpenShortcutsCommand.Execute(null);
            Assert.Equal("Shortcuts", mainVm.CurrentViewName);
            Assert.Contains(mainVm.OpenTabs, t => t.Id == "Shortcuts");
            Assert.Equal("Vouchers", mainVm.ShortcutsVM.SelectedCategory);
        }
        finally
        {
            try
            {
                if (System.IO.Directory.Exists(tempDir))
                {
                    System.IO.Directory.Delete(tempDir, true);
                }
            }
            catch { }
        }
    }

    [Fact]
    public void EqualityToBoolConverter_MultiBinding_EvaluatesEqualityCorrectly()
    {
        var converter = new PolyStock.WPF.Converters.EqualityToBoolConverter();

        // Same values
        var resultEqual = converter.Convert(new object[] { "Sales", "Sales" }, typeof(bool), null!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(true, resultEqual);

        // Different values
        var resultDiff = converter.Convert(new object[] { "Sales", "Purchase" }, typeof(bool), null!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(false, resultDiff);

        // Unset value
        var resultUnset = converter.Convert(new object[] { System.Windows.DependencyProperty.UnsetValue, "Sales" }, typeof(bool), null!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(false, resultUnset);
    }

    [Fact]
    public void EqualityToVisibilityConverter_MultiBinding_EvaluatesVisibilityCorrectly()
    {
        var converter = new PolyStock.WPF.Converters.EqualityToVisibilityConverter();

        // Same values -> Visible
        var resultEqual = converter.Convert(new object[] { "Active", "Active" }, typeof(System.Windows.Visibility), null!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(System.Windows.Visibility.Visible, resultEqual);

        // Different values -> Collapsed
        var resultDiff = converter.Convert(new object[] { "Active", "Inactive" }, typeof(System.Windows.Visibility), null!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(System.Windows.Visibility.Collapsed, resultDiff);
    }
}
