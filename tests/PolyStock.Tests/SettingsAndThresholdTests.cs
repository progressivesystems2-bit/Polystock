using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PolyStock.Core.Common;
using PolyStock.Core.Models;
using PolyStock.Data;
using PolyStock.Services;
using PolyStock.WPF.ViewModels;
using Xunit;

namespace PolyStock.Tests;

public class SettingsAndThresholdTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly InventoryService _inventoryService;
    private readonly bool _canRunOleDb;

    public SettingsAndThresholdTests()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "PolyStockSettingsTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        _testDbPath = Path.Combine(tempDir, "TestSettingsPolyStock.accdb");

        try
        {
            DatabaseInitializer.Initialize(_testDbPath);
            _canRunOleDb = true;
        }
        catch
        {
            _canRunOleDb = false;
        }

        _inventoryService = new InventoryService(_testDbPath);
    }

    public void Dispose()
    {
        try
        {
            var dir = Path.GetDirectoryName(_testDbPath);
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
        catch
        {
        }
    }

    [Theory]
    [InlineData(5.0, 0.0, true)]       // Threshold disabled (0) -> deducts
    [InlineData(0.1, 0.0, true)]       // Threshold disabled (0) -> deducts
    [InlineData(50.0, -5.0, true)]     // Negative threshold -> deducts
    [InlineData(4.99, 5.0, false)]     // Below threshold -> do NOT deduct
    [InlineData(5.0, 5.0, true)]       // At threshold -> deducts
    [InlineData(12.5, 10.0, true)]     // Above threshold -> deducts
    [InlineData(2.0, 10.0, false)]     // Below threshold -> do NOT deduct
    public void CalculationEngine_ShouldDeductInventory_CalculatesCorrectly(double totalKgSold, double thresholdKg, bool expectedDeduct)
    {
        bool actual = StockCalculationEngine.ShouldDeductInventory(totalKgSold, thresholdKg);
        Assert.Equal(expectedDeduct, actual);
    }

    [Fact]
    public void SettingsViewModel_ValidationAndProperties_WorkAsExpected()
    {
        var vm = new SettingsViewModel(_inventoryService);

        // Invalid text
        vm.ThresholdKgText = "abc";
        vm.SaveSettings();
        Assert.True(vm.IsErrorMessage);
        Assert.Contains("valid numeric", vm.StatusMessage);

        // Negative number
        vm.ThresholdKgText = "-10";
        vm.SaveSettings();
        Assert.True(vm.IsErrorMessage);
        Assert.Contains("cannot be negative", vm.StatusMessage);

        // Valid positive number
        bool callbackFired = false;
        vm.SettingsSavedAction = () => callbackFired = true;

        if (_canRunOleDb)
        {
            vm.ThresholdKgText = "7.5";
            vm.SaveSettings();
            Assert.False(vm.IsErrorMessage);
            Assert.Contains("7.5", vm.StatusMessage);
            Assert.True(callbackFired);
            Assert.Contains("Active Threshold", vm.ThresholdStatusSummary);

            // Verify persistence via service
            double retrieved = _inventoryService.GetSalesDeductionThresholdKg();
            Assert.Equal(7.5, retrieved);

            // Disable threshold
            vm.ThresholdKgText = "0";
            vm.SaveSettings();
            Assert.False(vm.IsErrorMessage);
            Assert.Contains("disabled", vm.StatusMessage);
            Assert.Contains("Standard Mode", vm.ThresholdStatusSummary);
        }
    }

    [Fact]
    public void SaleRowViewModel_BelowThreshold_ShowsCorrectStatusAndPreview()
    {
        var row = new SaleRowViewModel(null!)
        {
            ThresholdKg = 10.0,
            PackSizeKg = 25.0,
            BagsSold = 0,
            LooseKgSold = 4.0,
            RatePerKg = 100
        };

        // Even though AvailableStock is null, since it's below threshold (4kg < 10kg), stock is not insufficient!
        Assert.True(row.IsBelowInventoryThreshold);
        Assert.False(row.IsStockInsufficient);
        Assert.Contains("Below 10 kg threshold", row.StockStatusText);
        Assert.Equal("No inventory deduction", row.MorphedBagPreviewText);

        // Increase quantity above threshold
        row.LooseKgSold = 12.0; // 12kg >= 10kg threshold
        Assert.False(row.IsBelowInventoryThreshold);
        Assert.True(row.IsStockInsufficient); // Now insufficient because AvailableStock is null and TotalKgSold > 0
    }

    [Fact]
    public void SaleWithItemsBelowThreshold_DoesNotDeductInventory_AndRebuildPreservesStock()
    {
        if (!_canRunOleDb) return;

        var items = _inventoryService.GetAllItems();
        var godowns = _inventoryService.GetAllGodowns();
        Assert.NotEmpty(items);
        Assert.NotEmpty(godowns);

        var item = items[0];
        var godown = godowns[0];

        // 1. Ensure clean stock by purchasing 10 bags of 25kg
        var purchase = new Purchase
        {
            InvoiceNo = $"PUR-THRESH-{Guid.NewGuid().ToString("N")[..6]}",
            SupplierName = "Test Supplier",
            GodownId = godown.Id,
            GodownName = godown.Name,
            PurchaseDate = DateTime.Today,
            TotalBags = 10,
            TotalWeightKg = 250.0,
            TotalAmount = 10000.0,
            Notes = "Stock setup"
        };
        purchase.Items.Add(new PurchaseItem
        {
            ItemId = item.Id,
            ItemName = item.ItemName,
            GodownId = godown.Id,
            GodownName = godown.Name,
            PackSizeKg = 25.0,
            Bags = 10,
            TotalKg = 250.0,
            RatePerKg = 40.0,
            Amount = 10000.0
        });
        _inventoryService.SavePurchase(purchase);

        // Verify initial stock has 10 bags of 25kg
        var initialStock = _inventoryService.GetStock(item.Id, 25.0);
        Assert.NotNull(initialStock);
        Assert.True(initialStock.FullBags >= 10);
        int initialBags = initialStock.FullBags;
        double initialKg = initialStock.TotalKg;

        // 2. Set Settings threshold to 10.0 KG
        _inventoryService.SetSalesDeductionThresholdKg(10.0);
        Assert.Equal(10.0, _inventoryService.GetSalesDeductionThresholdKg());

        // 3. Create Sale A: 5.0 kg loose (< 10kg threshold) -> Should NOT deduct stock
        var saleA = new Sale
        {
            InvoiceNo = $"INV-THRESH-{Guid.NewGuid().ToString("N")[..6]}",
            CustomerName = "Small Sample Buyer",
            GodownId = godown.Id,
            GodownName = godown.Name,
            SaleDate = DateTime.Today,
            TotalBags = 0,
            TotalLooseKg = 5.0,
            TotalWeightKg = 5.0,
            TotalAmount = 500.0
        };
        saleA.Items.Add(new SaleItem
        {
            ItemId = item.Id,
            ItemName = item.ItemName,
            GodownId = godown.Id,
            GodownName = godown.Name,
            PackSizeKg = 25.0,
            BagsSold = 0,
            LooseKgSold = 5.0,
            TotalKgSold = 5.0,
            RatePerKg = 100.0,
            Amount = 500.0
        });

        int saleAId = _inventoryService.SaveSale(saleA);
        Assert.True(saleAId > 0);

        // Verify item was marked SkipStockDeduction = true
        var savedSaleA = _inventoryService.GetSaleById(saleAId);
        Assert.NotNull(savedSaleA);
        Assert.Single(savedSaleA.Items);
        Assert.True(savedSaleA.Items[0].SkipStockDeduction);

        // Stock after sale A should remain EXACTLY unchanged (no bag cut, no kg deducted)
        var stockAfterA = _inventoryService.GetStock(item.Id, 25.0);
        Assert.NotNull(stockAfterA);
        Assert.Equal(initialBags, stockAfterA.FullBags);
        Assert.Equal(initialKg, stockAfterA.TotalKg);

        // 4. Create Sale B: 15.0 kg loose (>= 10kg threshold) -> Should deduct normally
        var saleB = new Sale
        {
            InvoiceNo = $"INV-THRESH-{Guid.NewGuid().ToString("N")[..6]}",
            CustomerName = "Bulk Buyer",
            GodownId = godown.Id,
            GodownName = godown.Name,
            SaleDate = DateTime.Today,
            TotalBags = 0,
            TotalLooseKg = 15.0,
            TotalWeightKg = 15.0,
            TotalAmount = 1500.0
        };
        saleB.Items.Add(new SaleItem
        {
            ItemId = item.Id,
            ItemName = item.ItemName,
            GodownId = godown.Id,
            GodownName = godown.Name,
            PackSizeKg = 25.0,
            BagsSold = 0,
            LooseKgSold = 15.0,
            TotalKgSold = 15.0,
            RatePerKg = 100.0,
            Amount = 1500.0
        });

        int saleBId = _inventoryService.SaveSale(saleB);
        Assert.True(saleBId > 0);

        var savedSaleB = _inventoryService.GetSaleById(saleBId);
        Assert.NotNull(savedSaleB);
        Assert.False(savedSaleB.Items[0].SkipStockDeduction);

        // Verify stock after Sale B: 1 bag of 25kg cut, leaves initialBags - 1, and creates 1 bag of 10kg
        var stock25 = _inventoryService.GetStock(item.Id, 25.0);
        var stock10 = _inventoryService.GetStock(item.Id, 10.0);
        Assert.NotNull(stock25);
        Assert.Equal(initialBags - 1, stock25.FullBags);
        Assert.NotNull(stock10);
        Assert.Equal(1, stock10.FullBags);

        // 5. Test RebuildStockFromVouchers: Replaying transactions must preserve the exact stock!
        var reconResult = _inventoryService.RebuildStockFromVouchers();
        Assert.True(reconResult.SalesProcessed > 0);

        var stock25AfterRebuild = _inventoryService.GetStock(item.Id, 25.0);
        var stock10AfterRebuild = _inventoryService.GetStock(item.Id, 10.0);
        Assert.NotNull(stock25AfterRebuild);
        Assert.Equal(initialBags - 1, stock25AfterRebuild.FullBags);
        Assert.NotNull(stock10AfterRebuild);
        Assert.Equal(1, stock10AfterRebuild.FullBags);
    }
}
