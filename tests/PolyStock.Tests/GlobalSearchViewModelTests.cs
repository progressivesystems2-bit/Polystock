using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PolyStock.Data;
using PolyStock.Services;
using PolyStock.WPF.ViewModels;
using Xunit;

namespace PolyStock.Tests;

public class GlobalSearchViewModelTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly InventoryService _inventoryService;

    public GlobalSearchViewModelTests()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "PolyStockSearchTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        _testDbPath = Path.Combine(tempDir, "TestPolyStock.accdb");

        try
        {
            DatabaseInitializer.Initialize(_testDbPath);
        }
        catch { }

        _inventoryService = new InventoryService(_testDbPath);
    }

    public void Dispose()
    {
        try
        {
            var dir = Path.GetDirectoryName(_testDbPath);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task GlobalSearch_MatchesMenuAndNavigates()
    {
        var vm = new MainViewModel(_inventoryService);
        vm.GlobalSearchText = "Stock Register";

        // Allow debounce and query completion
        for (int i = 0; i < 30 && vm.GlobalSearchResults.Count == 0; i++)
        {
            await Task.Delay(100);
        }

        Assert.True(vm.IsGlobalSearchOpen);
        Assert.NotEmpty(vm.GlobalSearchResults);

        var stockResult = vm.GlobalSearchResults.FirstOrDefault(r => r.Title.Contains("Stock Register", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(stockResult);
        Assert.Equal("StockRegister", stockResult.NavigationTarget);

        vm.OpenGlobalSearchResult(stockResult);

        Assert.False(vm.IsGlobalSearchOpen);
        Assert.Equal("StockRegister", vm.CurrentViewName);
    }

    [Fact]
    public async Task GlobalSearch_ArrowKeySelectionNavigation()
    {
        var vm = new MainViewModel(_inventoryService);
        vm.GlobalSearchText = "Voucher";

        // Allow debounce and query completion
        for (int i = 0; i < 30 && vm.GlobalSearchResults.Count == 0; i++)
        {
            await Task.Delay(100);
        }

        Assert.NotEmpty(vm.GlobalSearchResults);
        Assert.NotNull(vm.SelectedSearchResult);

        var first = vm.SelectedSearchResult;
        vm.SelectNextSearchResult();
        if (vm.GlobalSearchResults.Count > 1)
        {
            Assert.NotEqual(first, vm.SelectedSearchResult);
        }

        vm.SelectPreviousSearchResult();
        Assert.Equal(first, vm.SelectedSearchResult);
    }

    [Fact]
    public void GlobalSearch_ClearCommand_ResetsState()
    {
        var vm = new MainViewModel(_inventoryService);
        vm.GlobalSearchText = "Test";
        vm.IsGlobalSearchOpen = true;

        vm.ClearGlobalSearchCommand.Execute(null);

        Assert.Empty(vm.GlobalSearchText);
        Assert.False(vm.IsGlobalSearchOpen);
    }
}
