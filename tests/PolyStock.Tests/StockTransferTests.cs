using System;
using System.IO;
using System.Linq;
using PolyStock.Core.Models;
using PolyStock.Data;
using PolyStock.Services;
using Xunit;

namespace PolyStock.Tests;

public class StockTransferTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly InventoryService _inventoryService;

    public StockTransferTests()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "PolyStockTransferTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        _testDbPath = Path.Combine(tempDir, "TestPolyStockTransfer.accdb");

        try
        {
            DatabaseInitializer.Initialize(_testDbPath);
        }
        catch
        {
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

    [Fact]
    public void TestStockTransfer_PreventsSameSourceAndDestination()
    {
        var service = _inventoryService;

        var transfer = new StockTransfer
        {
            TransferNo = "TRF-TEST-001",
            FromGodownId = 1,
            FromGodownName = "Godown A",
            ToGodownId = 1,
            ToGodownName = "Godown A",
            TotalBags = 5,
            TotalWeightKg = 125.0
        };
        transfer.Items.Add(new StockTransferItem
        {
            ItemId = 1,
            ItemName = "Test Item",
            PackSizeKg = 25.0,
            Bags = 5,
            TotalKg = 125.0
        });

        Assert.Throws<InvalidOperationException>(() => service.SaveStockTransfer(transfer));
    }

    [Fact]
    public void TestStockTransfer_RequiresAtLeastOneItem()
    {
        var service = _inventoryService;

        var transfer = new StockTransfer
        {
            TransferNo = "TRF-TEST-002",
            FromGodownId = 1,
            FromGodownName = "Godown A",
            ToGodownId = 2,
            ToGodownName = "Godown B"
        };

        Assert.Throws<InvalidOperationException>(() => service.SaveStockTransfer(transfer));
    }

    [Fact]
    public void TestStockTransfer_AutoSequencesDocumentNumber()
    {
        var service = _inventoryService;
        var nextNo = service.GetNextStockTransferNo();

        Assert.NotNull(nextNo);
        Assert.StartsWith($"TRF-{DateTime.Today:yyyyMMdd}-", nextNo);
    }

    [Fact]
    public void TestStockTransfer_DeductsFromSource_AddsToDestination_AndWritesLedger()
    {
        var service = _inventoryService;

        // 1. Create two godowns
        var g1 = new Godown { Code = "GDN-A", Name = "Central Warehouse", IsActive = true };
        g1.Id = service.SaveGodown(g1);

        var g2 = new Godown { Code = "GDN-B", Name = "Retail Branch Depot", IsActive = true };
        g2.Id = service.SaveGodown(g2);

        var items = service.GetAllItems();
        Assert.NotEmpty(items);
        var item = items.First();

        // 2. Put initial stock into Central Warehouse (Godown A) via Purchase of 20 bags @ 25kg
        var purchase = new Purchase
        {
            InvoiceNo = $"PUR-TRF-{Guid.NewGuid().ToString("N")[..6]}",
            SupplierName = "Transfer Setup Supplier",
            GodownId = g1.Id,
            GodownName = g1.Name,
            PurchaseDate = DateTime.Today,
            TotalBags = 20,
            TotalWeightKg = 500.0,
            TotalAmount = 20000.0
        };
        purchase.Items.Add(new PurchaseItem
        {
            ItemId = item.Id,
            ItemName = item.ItemName,
            GodownId = g1.Id,
            GodownName = g1.Name,
            PackSizeKg = 25.0,
            Bags = 20,
            TotalKg = 500.0,
            RatePerKg = 40.0,
            Amount = 20000.0
        });
        service.SavePurchase(purchase);

        // Verify Central Warehouse stock
        var stockA_Before = service.GetStock(item.Id, 25.0, g1.Id);
        Assert.NotNull(stockA_Before);
        int initialBagsA = stockA_Before.FullBags;
        Assert.True(initialBagsA >= 20);

        // 3. Transfer 8 bags from Central Warehouse (Godown A) to Retail Branch Depot (Godown B)
        string trfNo = $"TRF-{DateTime.Today:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4]}";
        var transfer = new StockTransfer
        {
            TransferNo = trfNo,
            TransferDate = DateTime.Today,
            FromGodownId = g1.Id,
            FromGodownName = g1.Name,
            ToGodownId = g2.Id,
            ToGodownName = g2.Name,
            TotalBags = 8,
            TotalWeightKg = 200.0,
            TotalAmount = 8000.0,
            Notes = "Inter-warehouse transfer memo MH-12-AB-1234"
        };
        transfer.Items.Add(new StockTransferItem
        {
            ItemId = item.Id,
            ItemName = item.ItemName,
            PackSizeKg = 25.0,
            Bags = 8,
            LooseKg = 0.0,
            TotalKg = 200.0,
            RatePerKg = 40.0,
            Amount = 8000.0,
            Remarks = "Standard 25kg polymer bags"
        });

        int trfId = service.SaveStockTransfer(transfer);
        Assert.True(trfId > 0);

        // 4. Verify Stock Balances:
        // Source (Godown A): Deducted 8 bags
        var stockA_After = service.GetStock(item.Id, 25.0, g1.Id);
        Assert.NotNull(stockA_After);
        Assert.Equal(initialBagsA - 8, stockA_After.FullBags);
        Assert.Equal((initialBagsA - 8) * 25.0, stockA_After.TotalKg);

        // Destination (Godown B): Added 8 bags
        var stockB_After = service.GetStock(item.Id, 25.0, g2.Id);
        Assert.NotNull(stockB_After);
        Assert.Equal(8, stockB_After.FullBags);
        Assert.Equal(200.0, stockB_After.TotalKg);

        // 5. Verify Transfer Document Retrieval
        var retrieved = service.GetStockTransferById(trfId);
        Assert.NotNull(retrieved);
        Assert.Equal(trfNo, retrieved.TransferNo);
        Assert.Equal(g1.Name, retrieved.FromGodownName);
        Assert.Equal(g2.Name, retrieved.ToGodownName);
        Assert.Equal(8, retrieved.TotalBags);
        Assert.Equal(200.0, retrieved.TotalWeightKg);
        Assert.Single(retrieved.Items);
        Assert.Equal(8, retrieved.Items[0].Bags);

        // 6. Verify Stock Ledger Audit Trail:
        // Must contain TRANSFER_OUT for Godown A and TRANSFER_IN for Godown B
        var ledgerEntries = service.GetLedger(item.Id, 25.0);
        Assert.NotEmpty(ledgerEntries);

        var outEntry = ledgerEntries.FirstOrDefault(l => l.RefInvoiceNo == trfNo && l.TransactionType == "TRANSFER_OUT");
        Assert.NotNull(outEntry);
        Assert.Equal(g1.Id, outEntry.GodownId);
        Assert.Equal(-8, outEntry.FullBagsChange);
        Assert.Equal(-200.0, outEntry.TotalKgChange);
        Assert.Contains(g2.Name, outEntry.Remarks);

        var inEntry = ledgerEntries.FirstOrDefault(l => l.RefInvoiceNo == trfNo && l.TransactionType == "TRANSFER_IN");
        Assert.NotNull(inEntry);
        Assert.Equal(g2.Id, inEntry.GodownId);
        Assert.Equal(8, inEntry.FullBagsChange);
        Assert.Equal(200.0, inEntry.TotalKgChange);
        Assert.Contains(g1.Name, inEntry.Remarks);
    }
}
