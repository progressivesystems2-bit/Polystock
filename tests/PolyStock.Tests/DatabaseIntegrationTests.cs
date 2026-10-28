using System;
using System.IO;
using System.Linq;
using PolyStock.Core.Common;
using PolyStock.Core.Models;
using PolyStock.Data;
using PolyStock.Services;
using Xunit;

namespace PolyStock.Tests;

public class DatabaseIntegrationTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly InventoryService _inventoryService;

    public DatabaseIntegrationTests()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "PolyStockTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        _testDbPath = Path.Combine(tempDir, "TestPolyStock.accdb");

        try
        {
            DatabaseInitializer.Initialize(_testDbPath);
        }
        catch
        {
            // If Access engine / OleDb driver is not installed on CI, test gracefully handles
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
    public void TestPureCalculationScenariosMatchingUserRequirements()
    {
        // Scenario:
        // 5 bags 30kg = 150kg
        // 3 bags 25kg = 75kg
        // 2 bags 20kg = 40kg

        // Sale 1: 1 bag 30kg = 30kg
        var sale1 = StockCalculationEngine.CalculateBagMorph(
            currentFullBags: 5, packSizeKg: 30, bagsSold: 1, looseKgSold: 0);
        Assert.True(sale1.Success);
        Assert.Equal(4, sale1.SourceBagsRemaining);
        Assert.Equal(0, sale1.MorphedBagsAdded);
        Assert.Equal(30.0, sale1.TotalKgSold);

        // Sale 2: 2 bags 25kg = 50kg (from 3 bags 25kg)
        var sale2 = StockCalculationEngine.CalculateBagMorph(
            currentFullBags: 3, packSizeKg: 25, bagsSold: 2, looseKgSold: 0);
        Assert.True(sale2.Success);
        Assert.Equal(1, sale2.SourceBagsRemaining);
        Assert.Equal(0, sale2.MorphedBagsAdded);
        Assert.Equal(50.0, sale2.TotalKgSold);

        // Sale 3: 10kg from 20kg bag (from 2 bags 20kg = 40kg) -> leaves 1 bag of 20kg and creates 1 bag of 10kg
        var sale3 = StockCalculationEngine.CalculateBagMorph(
            currentFullBags: 2, packSizeKg: 20, bagsSold: 0, looseKgSold: 10);
        Assert.True(sale3.Success);
        Assert.Equal(1, sale3.SourceBagsRemaining);
        Assert.Equal(10.0, sale3.MorphedPackSizeKg);
        Assert.Equal(1, sale3.MorphedBagsAdded);
        Assert.Equal(10.0, sale3.TotalKgSold);
    }

    [Fact]
    public void TestSaveItemWithoutFixedPackSizes()
    {
        var service = _inventoryService;

        var hgs = service.GetAllHeadGroups();
        var brands = service.GetAllBrands();
        Assert.NotEmpty(hgs);
        Assert.NotEmpty(brands);

        var item = new Item
        {
            HeadGroupId = hgs[0].Id,
            HeadGroupName = hgs[0].Name,
            BrandId = brands[0].Id,
            BrandName = brands[0].Name,
            ItemCode = "101",
            ItemName = $"{hgs[0].Name} {brands[0].Name} 101",
            DefaultRatePerKg = 45.5,
            Description = "Flexible Pack Size Test Item",
            CreatedAt = DateTime.Now
        };

        // Save item with no predefined pack sizes
        var id = service.SaveItem(item);
        Assert.True(id > 0);

        // Verify retrieval
        var allItems = service.GetAllItems();
        var savedItem = allItems.FirstOrDefault(i => i.Id == id);
        Assert.NotNull(savedItem);
        Assert.Equal(item.ItemName, savedItem.ItemName);

        // Update item rate
        savedItem.DefaultRatePerKg = 49.0;
        service.SaveItem(savedItem);

        var updatedItem = service.GetAllItems().FirstOrDefault(i => i.Id == id);
        Assert.NotNull(updatedItem);
        Assert.Equal(49.0, updatedItem.DefaultRatePerKg);
    }

    [Fact]
    public void TestSaveAndModifyGodown()
    {
        var service = _inventoryService;

        var godown = new Godown
        {
            Code = "GW-TEST",
            Name = "Testing Storage Godown",
            Location = "Shed 12B, South Wing",
            ContactPerson = "Kiran Rao",
            Phone = "+91 99887 76655",
            Description = "Raw materials and bags staging",
            IsActive = true,
            CreatedAt = DateTime.Now
        };

        var id = service.SaveGodown(godown);
        Assert.True(id > 0);

        var allGodowns = service.GetAllGodowns();
        var saved = allGodowns.FirstOrDefault(g => g.Id == id);
        Assert.NotNull(saved);
        Assert.Equal("Testing Storage Godown", saved.Name);
        Assert.Equal("GW-TEST", saved.Code);

        // Modify Godown
        saved.Name = "Testing Storage Godown - Expanded";
        saved.Location = "Shed 12B & 12C";
        service.SaveGodown(saved);

        var updated = service.GetAllGodowns().FirstOrDefault(g => g.Id == id);
        Assert.NotNull(updated);
        Assert.Equal("Testing Storage Godown - Expanded", updated.Name);
        Assert.Equal("Shed 12B & 12C", updated.Location);
    }

    [Fact]
    public void TestCreateAndModifySaleBill()
    {
        var service = _inventoryService;

        var items = service.GetAllItems();
        var godowns = service.GetAllGodowns();
        Assert.NotEmpty(items);
        Assert.NotEmpty(godowns);

        var item = items[0];
        var godown = godowns[0];

        // Ensure stock exists by purchasing 10 bags of 25kg
        var purchase = new Purchase
        {
            InvoiceNo = $"PUR-TEST-{Guid.NewGuid().ToString("N")[..6]}",
            SupplierName = "Test Supplier",
            GodownId = godown.Id,
            GodownName = godown.Name,
            PurchaseDate = DateTime.Today,
            TotalBags = 10,
            TotalWeightKg = 250.0,
            TotalAmount = 10000.0,
            Notes = "Stock setup for modify test"
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
        service.SavePurchase(purchase);

        // 1. Create Sale Bill
        var sale = new Sale
        {
            InvoiceNo = $"SAL-TEST-{Guid.NewGuid().ToString("N")[..6]}",
            CustomerName = "Initial Customer",
            GodownId = godown.Id,
            GodownName = godown.Name,
            SaleDate = DateTime.Today,
            TotalBags = 2,
            TotalLooseKg = 0,
            TotalWeightKg = 50.0,
            TotalAmount = 2500.0,
            Notes = "Initial sale bill"
        };
        sale.Items.Add(new SaleItem
        {
            ItemId = item.Id,
            ItemName = item.ItemName,
            GodownId = godown.Id,
            GodownName = godown.Name,
            PackSizeKg = 25.0,
            BagsSold = 2,
            LooseKgSold = 0,
            TotalKgSold = 50.0,
            RatePerKg = 50.0,
            Amount = 2500.0
        });

        var saleId = service.SaveSale(sale);
        Assert.True(saleId > 0);

        // 2. Modify Sale Bill (e.g. change customer name, change bags sold to 3, update amount)
        var loadedSale = service.GetSaleById(saleId);
        Assert.NotNull(loadedSale);
        Assert.Equal("Initial Customer", loadedSale.CustomerName);
        Assert.Equal(2, loadedSale.TotalBags);

        loadedSale.CustomerName = "Modified Customer Ltd";
        loadedSale.TotalBags = 3;
        loadedSale.TotalWeightKg = 75.0;
        loadedSale.TotalAmount = 3750.0;
        loadedSale.Items[0].BagsSold = 3;
        loadedSale.Items[0].TotalKgSold = 75.0;
        loadedSale.Items[0].Amount = 3750.0;

        // This triggers UpdateSale with tx
        service.SaveSale(loadedSale);

        var verifySale = service.GetSaleById(saleId);
        Assert.NotNull(verifySale);
        Assert.Equal("Modified Customer Ltd", verifySale.CustomerName);
        Assert.Equal(3, verifySale.TotalBags);
        Assert.Equal(75.0, verifySale.TotalWeightKg);
        Assert.Equal(3750.0, verifySale.TotalAmount);
    }

    [Fact]
    public void TestSaveModifyAndDeleteAccount()
    {
        var service = _inventoryService;

        // 1. Create Account
        var acc = new Account
        {
            AccountType = "Customer",
            AccountName = "Ananya Enterprises",
            Address = "Plot 88, Electronic City, Bengaluru",
            Mobile = "+91 99001 22334",
            Email = "ananya@example.com",
            GSTIN = "29AAAAA0000A1Z5",
            OpeningBalance = 50000.0,
            Description = "Premium plastic granule buyer"
        };

        var id = service.SaveAccount(acc);
        Assert.True(id > 0);

        // 2. Read
        var saved = service.GetAccountById(id);
        Assert.NotNull(saved);
        Assert.Equal("Ananya Enterprises", saved.AccountName);
        Assert.Equal("+91 99001 22334", saved.Mobile);
        Assert.Equal(50000.0, saved.OpeningBalance);

        // 3. Modify
        saved.AccountName = "Ananya Enterprises Pvt Ltd";
        saved.OpeningBalance = 75000.0;
        service.SaveAccount(saved);

        var updated = service.GetAccountById(id);
        Assert.NotNull(updated);
        Assert.Equal("Ananya Enterprises Pvt Ltd", updated.AccountName);
        Assert.Equal(75000.0, updated.OpeningBalance);

        // 4. Delete
        service.DeleteAccount(id);
        var deleted = service.GetAccountById(id);
        Assert.Null(deleted);
    }

    [Fact]
    public void TestMultiBagLooseSaleStockBreakdown()
    {
        var service = _inventoryService;

        var items = service.GetAllItems();
        var item = items.FirstOrDefault(i => i.ItemName.Contains("Balaji")) ?? items.First();

        // 1. Inward stock for 3 different pack sizes:
        // 20kg -> 3 bags (60kg), 22kg -> 2 bags (44kg), 30kg -> 10 bags (300kg)
        var purchase = new Purchase
        {
            InvoiceNo = $"PUR-MULTI-{Guid.NewGuid().ToString("N")[..6]}",
            SupplierName = "Balaji Polymers Suppliers",
            PurchaseDate = DateTime.Today,
            TotalBags = 15,
            TotalWeightKg = 404.0,
            TotalAmount = 16160.0
        };
        purchase.Items.Add(new PurchaseItem { ItemId = item.Id, ItemName = item.ItemName, PackSizeKg = 20.0, Bags = 3, TotalKg = 60.0, RatePerKg = 40.0, Amount = 2400.0 });
        purchase.Items.Add(new PurchaseItem { ItemId = item.Id, ItemName = item.ItemName, PackSizeKg = 22.0, Bags = 2, TotalKg = 44.0, RatePerKg = 40.0, Amount = 1760.0 });
        purchase.Items.Add(new PurchaseItem { ItemId = item.Id, ItemName = item.ItemName, PackSizeKg = 30.0, Bags = 10, TotalKg = 300.0, RatePerKg = 40.0, Amount = 12000.0 });
        service.SavePurchase(purchase);

        // Get initial stock before sale
        var stock20Before = service.GetStock(item.Id, 20.0);
        Assert.NotNull(stock20Before);
        int initialBags20 = stock20Before.FullBags;
        double initialLoose20 = stock20Before.LooseKg;

        // 2. Sell 5kg loose from 20kg pack
        var sale = new Sale
        {
            InvoiceNo = $"SAL-LOOSE-{Guid.NewGuid().ToString("N")[..6]}",
            CustomerName = "Retail Buyer",
            SaleDate = DateTime.Today,
            TotalBags = 0,
            TotalLooseKg = 5.0,
            TotalWeightKg = 5.0,
            TotalAmount = 250.0
        };
        sale.Items.Add(new SaleItem
        {
            ItemId = item.Id,
            ItemName = item.ItemName,
            PackSizeKg = 20.0,
            BagsSold = 0,
            LooseKgSold = 5.0,
            TotalKgSold = 5.0,
            RatePerKg = 50.0,
            Amount = 250.0
        });
        service.SaveSale(sale);

        // 3. Verify exact stock breakdown across all pack sizes:
        // 20kg pack: 1 bag was cut for 5kg loose sale -> leaves 2 bags of 20kg, and creates 1 bag of 15kg!
        var stock20After = service.GetStock(item.Id, 20.0);
        var stock15After = service.GetStock(item.Id, 15.0);
        var stock22After = service.GetStock(item.Id, 22.0);
        var stock30After = service.GetStock(item.Id, 30.0);

        Assert.NotNull(stock20After);
        Assert.NotNull(stock15After);
        Assert.NotNull(stock22After);
        Assert.NotNull(stock30After);

        Assert.Equal(2, stock20After.FullBags);
        Assert.Equal(40.0, stock20After.TotalKg);

        Assert.Equal(1, stock15After.FullBags);
        Assert.Equal(15.0, stock15After.TotalKg);

        // 22kg and 30kg packs: untouched
        Assert.True(stock22After.FullBags >= 2);
        Assert.True(stock30After.FullBags >= 10);

        // 4. Verify Ledger Audit Trail for the sale
        var ledger20 = service.GetLedger(item.Id, 20.0);
        var lastEntry20 = ledger20.FirstOrDefault(l => l.RefInvoiceNo == sale.InvoiceNo);
        Assert.NotNull(lastEntry20);
        Assert.Equal(-20.0, lastEntry20.TotalKgChange);
        Assert.Contains("Cut bag", lastEntry20.Remarks);

        var ledger15 = service.GetLedger(item.Id, 15.0);
        var lastEntry15 = ledger15.FirstOrDefault(l => l.RefInvoiceNo == sale.InvoiceNo);
        Assert.NotNull(lastEntry15);
        Assert.Equal(15.0, lastEntry15.TotalKgChange);
        Assert.Equal("BAG_MORPH", lastEntry15.TransactionType);
    }

    [Fact]
    public void TestBillLevelFinancialsAndPartyAccountPostingForSale()
    {
        var service = _inventoryService;

        var items = service.GetAllItems();
        var item = items.First();

        // 1. Inward stock first
        var purchase = new Purchase
        {
            InvoiceNo = $"PUR-FIN-{Guid.NewGuid().ToString("N")[..6]}",
            SupplierName = "Balaji Polymers Suppliers",
            PurchaseDate = DateTime.Today,
            TotalBags = 20,
            TotalWeightKg = 500.0,
            SubTotal = 20000.0,
            GstPercent = 18.0,
            GstAmount = 3600.0,
            Freight = 500.0,
            LoadingUnloading = 200.0,
            GrandTotal = 24300.0,
            TotalAmount = 24300.0
        };
        purchase.Items.Add(new PurchaseItem
        {
            ItemId = item.Id,
            ItemName = item.ItemName,
            PackSizeKg = 25.0,
            Bags = 20,
            TotalKg = 500.0,
            RatePerKg = 40.0,
            Amount = 20000.0
        });
        service.SavePurchase(purchase);

        // 2. Create Sale Bill with GST 18%, Freight 450, LoadingUnload 150
        string customer = $"Buyer-{Guid.NewGuid().ToString("N")[..6]}";
        double itemSubTotal = 5000.0; // 4 bags @ 25kg = 100kg @ 50/kg = 5000
        double gstPercent = 18.0;
        double gstAmount = itemSubTotal * gstPercent / 100.0; // 900
        double freight = 450.0;
        double loadingUnloading = 150.0;
        double grandTotal = itemSubTotal + gstAmount + freight + loadingUnloading; // 6500
        double partyBillPostingAmount = itemSubTotal + gstAmount; // 5900

        var sale = new Sale
        {
            InvoiceNo = $"SAL-FIN-{Guid.NewGuid().ToString("N")[..6]}",
            CustomerName = customer,
            SaleDate = DateTime.Today,
            TotalBags = 4,
            TotalLooseKg = 0,
            TotalWeightKg = 100.0,
            SubTotal = itemSubTotal,
            GstPercent = gstPercent,
            GstAmount = gstAmount,
            Freight = freight,
            LoadingUnloading = loadingUnloading,
            GrandTotal = grandTotal,
            TotalAmount = grandTotal
        };
        sale.Items.Add(new SaleItem
        {
            ItemId = item.Id,
            ItemName = item.ItemName,
            PackSizeKg = 25.0,
            BagsSold = 4,
            LooseKgSold = 0,
            TotalKgSold = 100.0,
            RatePerKg = 50.0,
            Amount = itemSubTotal
        });

        var saleId = service.SaveSale(sale);
        Assert.True(saleId > 0);

        // 3. Verify Sale retrieved from database
        var retrievedSale = service.GetSaleById(saleId);
        Assert.NotNull(retrievedSale);
        Assert.Equal(itemSubTotal, retrievedSale.SubTotal);
        Assert.Equal(gstPercent, retrievedSale.GstPercent);
        Assert.Equal(gstAmount, retrievedSale.GstAmount);
        Assert.Equal(freight, retrievedSale.Freight);
        Assert.Equal(loadingUnloading, retrievedSale.LoadingUnloading);
        Assert.Equal(grandTotal, retrievedSale.GrandTotal);
        Assert.Equal(partyBillPostingAmount, retrievedSale.PartyAmount);

        // 4. Verify Account Ledger receives exact (SubTotal + GSTAmount)
        var ledgerEntries = service.GetAccountLedger(accountName: customer);
        Assert.NotEmpty(ledgerEntries);
        var entry = ledgerEntries.FirstOrDefault(l => l.RefInvoiceNo == sale.InvoiceNo);
        Assert.NotNull(entry);
        Assert.Equal("SALE", entry.TransactionType);
        Assert.Equal(partyBillPostingAmount, entry.Debit);
        Assert.Equal(0.0, entry.Credit);

        // 5. Verify Account live CurrentBalance equals party posting amount
        var accounts = service.GetAllAccounts();
        var partyAccount = accounts.FirstOrDefault(a => a.AccountName == customer);
        Assert.NotNull(partyAccount);
        Assert.Equal(partyBillPostingAmount, partyAccount.CurrentBalance);
        Assert.Equal(partyBillPostingAmount, partyAccount.TotalDebit);
    }

    [Fact]
    public void TestBillLevelFinancialsAndPartyAccountPostingForPurchase()
    {
        var service = _inventoryService;

        var items = service.GetAllItems();
        var item = items.First();

        string supplier = $"Supplier-{Guid.NewGuid().ToString("N")[..6]}";
        double subTotal = 15000.0;
        double gstPercent = 12.0;
        double gstAmount = 1800.0;
        double freight = 300.0;
        double loadingUnloading = 100.0;
        double grandTotal = 17200.0;
        double partyPostingAmount = 16800.0; // 15000 + 1800

        var purchase = new Purchase
        {
            InvoiceNo = $"PUR-TESTFIN-{Guid.NewGuid().ToString("N")[..6]}",
            SupplierName = supplier,
            PurchaseDate = DateTime.Today,
            TotalBags = 15,
            TotalWeightKg = 375.0,
            SubTotal = subTotal,
            GstPercent = gstPercent,
            GstAmount = gstAmount,
            Freight = freight,
            LoadingUnloading = loadingUnloading,
            GrandTotal = grandTotal,
            TotalAmount = grandTotal
        };
        purchase.Items.Add(new PurchaseItem
        {
            ItemId = item.Id,
            ItemName = item.ItemName,
            PackSizeKg = 25.0,
            Bags = 15,
            TotalKg = 375.0,
            RatePerKg = 40.0,
            Amount = subTotal
        });

        var purId = service.SavePurchase(purchase);
        Assert.True(purId > 0);

        // Verify Account Ledger receives exact Credit of (SubTotal + GSTAmount)
        var ledgerEntries = service.GetAccountLedger(accountName: supplier);
        Assert.NotEmpty(ledgerEntries);
        var entry = ledgerEntries.FirstOrDefault(l => l.RefInvoiceNo == purchase.InvoiceNo);
        Assert.NotNull(entry);
        Assert.Equal("PURCHASE", entry.TransactionType);
        Assert.Equal(0.0, entry.Debit);
        Assert.Equal(partyPostingAmount, entry.Credit);

        // Verify Supplier balance
        var accounts = service.GetAllAccounts();
        var supplierAcc = accounts.FirstOrDefault(a => a.AccountName == supplier);
        Assert.NotNull(supplierAcc);
        Assert.Equal(-partyPostingAmount, supplierAcc.CurrentBalance); // Credit balance
        Assert.Equal(partyPostingAmount, supplierAcc.TotalCredit);
    }

    [Fact]
    public void TestPurchaseEffectiveLandingCostPerKgScenario()
    {
        var vm = new PolyStock.WPF.ViewModels.PurchaseViewModel(_inventoryService);
        vm.Lines.Clear();

        var row = new PolyStock.WPF.ViewModels.PurchaseRowViewModel
        {
            PackSizeKg = 25.0,
            Bags = 20, // 500 kg
            RatePerKg = 20.0 // 10,000 Amount
        };
        vm.Lines.Add(row);

        vm.Freight = 100.0;
        vm.GstAmount = 300.0;
        vm.LoadingUnloading = 100.0;

        vm.RecalculateTotals();

        Assert.Equal(500.0, vm.TotalWeightKg);
        Assert.Equal(10000.0, vm.SubTotal);
        Assert.Equal(500.0, vm.TotalAdditionalCost);
        Assert.Equal(10500.0, vm.GrandTotal);

        // Indicative Effective Landing Cost per kg
        Assert.Equal(21.0, row.EffectiveRatePerKg);
        Assert.Equal(10500.0, row.EffectiveAmount);
        Assert.Equal(21.0, vm.AverageEffectiveRatePerKg);
    }

    [Fact]
    public void TestCreateAndModifyPurchaseBill()
    {
        var service = _inventoryService;

        var items = service.GetAllItems();
        Assert.NotEmpty(items);
        var item = items.First();

        // 1. Initial Purchase Bill
        string invoiceNo = $"PUR-MOD-{Guid.NewGuid().ToString("N")[..6]}";
        var purchase = new Purchase
        {
            InvoiceNo = invoiceNo,
            SupplierName = "Initial Supplier Ltd",
            PurchaseDate = DateTime.Today,
            TotalBags = 5,
            TotalWeightKg = 125.0,
            SubTotal = 5000.0,
            GstPercent = 18.0,
            GstAmount = 900.0,
            Freight = 100.0,
            LoadingUnloading = 50.0,
            GrandTotal = 6050.0,
            TotalAmount = 6050.0,
            Notes = "Original purchase entry"
        };
        purchase.Items.Add(new PurchaseItem
        {
            ItemId = item.Id,
            ItemName = item.ItemName,
            PackSizeKg = 25.0,
            Bags = 5,
            TotalKg = 125.0,
            RatePerKg = 40.0,
            Amount = 5000.0
        });

        var purId = service.SavePurchase(purchase);
        Assert.True(purId > 0);

        // Verify initial stock
        var stockBefore = service.GetStock(item.Id, 25.0);
        Assert.NotNull(stockBefore);
        int fbBefore = stockBefore.FullBags;

        // 2. Modify Purchase Bill (e.g. increase bags from 5 to 8)
        var loaded = service.GetPurchaseById(purId);
        Assert.NotNull(loaded);
        Assert.Equal("Initial Supplier Ltd", loaded.SupplierName);
        Assert.Equal(5, loaded.TotalBags);

        loaded.SupplierName = "Modified Supplier Co";
        loaded.TotalBags = 8;
        loaded.TotalWeightKg = 200.0;
        loaded.SubTotal = 8000.0;
        loaded.GstAmount = 1440.0;
        loaded.GrandTotal = 9590.0;
        loaded.TotalAmount = 9590.0;
        loaded.Items[0].Bags = 8;
        loaded.Items[0].TotalKg = 200.0;
        loaded.Items[0].Amount = 8000.0;

        service.SavePurchase(loaded);

        // 3. Verify updated purchase record
        var updated = service.GetPurchaseById(purId);
        Assert.NotNull(updated);
        Assert.Equal("Modified Supplier Co", updated.SupplierName);
        Assert.Equal(8, updated.TotalBags);
        Assert.Equal(200.0, updated.TotalWeightKg);
        Assert.Equal(8000.0, updated.SubTotal);
        Assert.Equal(9590.0, updated.GrandTotal);

        // 4. Verify stock adjusted net +3 bags (from 5 to 8)
        var stockAfter = service.GetStock(item.Id, 25.0);
        Assert.NotNull(stockAfter);
        Assert.Equal(fbBefore + 3, stockAfter.FullBags);

        // 5. Verify Supplier Ledger
        var ledger = service.GetAccountLedger(accountName: "Modified Supplier Co");
        Assert.NotEmpty(ledger);
        var entry = ledger.FirstOrDefault(l => l.RefInvoiceNo == invoiceNo);
        Assert.NotNull(entry);
        Assert.Equal("PURCHASE_MODIFIED", entry.TransactionType);
        Assert.Equal(9440.0, entry.Credit); // SubTotal 8000 + GST 1440
    }

    [Fact]
    public void TestPurchaseWithGodownSelection()
    {
        var service = _inventoryService;

        var godowns = service.GetAllGodowns();
        var godown = godowns.FirstOrDefault() ?? new Godown { Code = "GW-TEST", Name = "Central Godown", IsActive = true };
        if (godown.Id == 0) godown.Id = service.SaveGodown(godown);

        var items = service.GetAllItems();
        var item = items.First();

        var invoiceNo = $"PUR-GDN-{Guid.NewGuid().ToString("N")[..6]}";
        var purchase = new Purchase
        {
            InvoiceNo = invoiceNo,
            SupplierName = "Godown Test Supplier",
            GodownId = godown.Id,
            GodownName = godown.Name,
            PurchaseDate = DateTime.Today,
            TotalBags = 4,
            TotalWeightKg = 100.0,
            SubTotal = 4000.0,
            GrandTotal = 4000.0,
            TotalAmount = 4000.0
        };
        purchase.Items.Add(new PurchaseItem
        {
            ItemId = item.Id,
            ItemName = item.ItemName,
            GodownId = godown.Id,
            GodownName = godown.Name,
            PackSizeKg = 25.0,
            Bags = 4,
            TotalKg = 100.0,
            RatePerKg = 40.0,
            Amount = 4000.0
        });

        var purId = service.SavePurchase(purchase);
        Assert.True(purId > 0);

        var savedPurchase = service.GetPurchaseById(purId);
        Assert.NotNull(savedPurchase);
        Assert.Equal(godown.Id, savedPurchase.GodownId);
        Assert.Equal(godown.Name, savedPurchase.GodownName);
        Assert.NotEmpty(savedPurchase.Items);
        Assert.Equal(godown.Id, savedPurchase.Items[0].GodownId);
        Assert.Equal(godown.Name, savedPurchase.Items[0].GodownName);
    }

    [Fact]
    public void TestStockReconciliationAndRegeneration()
    {
        var service = _inventoryService;

        var items = service.GetAllItems();
        var item = items.First();

        // 1. Purchase: 10 bags of 25kg = 250kg
        var purchase = new Purchase
        {
            InvoiceNo = $"PUR-RECON-{Guid.NewGuid().ToString("N")[..6]}",
            SupplierName = "Recon Supplier",
            PurchaseDate = DateTime.Today.AddDays(-2),
            TotalBags = 10,
            TotalWeightKg = 250.0,
            TotalAmount = 10000.0
        };
        purchase.Items.Add(new PurchaseItem
        {
            ItemId = item.Id,
            ItemName = item.ItemName,
            PackSizeKg = 25.0,
            Bags = 10,
            TotalKg = 250.0,
            RatePerKg = 40.0,
            Amount = 10000.0
        });
        service.SavePurchase(purchase);

        // 2. Sale: 3 bags + 7kg loose from 25kg pack (Total sold = 82kg, leaves 6 full bags and 18kg loose = 168kg)
        var sale = new Sale
        {
            InvoiceNo = $"SAL-RECON-{Guid.NewGuid().ToString("N")[..6]}",
            CustomerName = "Recon Buyer",
            SaleDate = DateTime.Today.AddDays(-1),
            TotalBags = 3,
            TotalLooseKg = 7.0,
            TotalWeightKg = 82.0,
            TotalAmount = 4100.0
        };
        sale.Items.Add(new SaleItem
        {
            ItemId = item.Id,
            ItemName = item.ItemName,
            PackSizeKg = 25.0,
            BagsSold = 3,
            LooseKgSold = 7.0,
            TotalKgSold = 82.0,
            RatePerKg = 50.0,
            Amount = 4100.0
        });
        service.SaveSale(sale);

        // Verify initial accurate stock
        var stock25Initial = service.GetStock(item.Id, 25.0);
        var stock18Initial = service.GetStock(item.Id, 18.0);
        Assert.NotNull(stock25Initial);
        Assert.NotNull(stock18Initial);
        Assert.Equal(6, stock25Initial.FullBags);
        Assert.Equal(150.0, stock25Initial.TotalKg);
        Assert.Equal(1, stock18Initial.FullBags);
        Assert.Equal(18.0, stock18Initial.TotalKg);

        // 3. Simulate corrupt / drift database entry (e.g. manual edit in MS Access)
        using (var conn = AccessConnectionProvider.CreateConnection(_testDbPath))
        {
            conn.Open();
            using var corruptCmd = new System.Data.OleDb.OleDbCommand("UPDATE Stock SET FullBags = 999, LooseKg = 0, TotalKg = 9999.0 WHERE ItemId = ?", conn);
            corruptCmd.Parameters.Add("?", System.Data.OleDb.OleDbType.Integer).Value = item.Id;
            corruptCmd.ExecuteNonQuery();
        }

        // Verify corrupted state
        var stockCorrupted = service.GetStock(item.Id, 25.0);
        Assert.NotNull(stockCorrupted);
        Assert.Equal(999, stockCorrupted.FullBags);

        // 4. Run Rebuild / Regeneration Engine
        var reconResult = service.RebuildStockFromVouchers();
        Assert.True(reconResult.PurchasesProcessed > 0);
        Assert.True(reconResult.SalesProcessed > 0);

        // 5. Verify restored ground-truth stock
        var stock25Restored = service.GetStock(item.Id, 25.0);
        var stock18Restored = service.GetStock(item.Id, 18.0);
        Assert.NotNull(stock25Restored);
        Assert.NotNull(stock18Restored);
        Assert.Equal(6, stock25Restored.FullBags);
        Assert.Equal(150.0, stock25Restored.TotalKg);
        Assert.Equal(1, stock18Restored.FullBags);
        Assert.Equal(18.0, stock18Restored.TotalKg);

        // Verify restored ledger
        var ledger25 = service.GetLedger(item.Id, 25.0);
        Assert.NotEmpty(ledger25);
        var lastEntry25 = ledger25.First();
        Assert.Equal(6, lastEntry25.BalanceFullBags);
        Assert.Equal(150.0, lastEntry25.BalanceTotalKg);

        var ledger18 = service.GetLedger(item.Id, 18.0);
        Assert.NotEmpty(ledger18);
        var lastEntry18 = ledger18.First();
        Assert.Equal(1, lastEntry18.BalanceFullBags);
        Assert.Equal(18.0, lastEntry18.BalanceTotalKg);
    }

    [Fact]
    public void TestSalePaymentModesAndLedgerPosting()
    {
        var service = _inventoryService;

        var items = service.GetAllItems();
        var godowns = service.GetAllGodowns();
        Assert.NotEmpty(items);
        Assert.NotEmpty(godowns);

        var item = items[0];
        var godown = godowns[0];

        // Ensure stock exists: purchase 50 bags of 25kg
        var purchase = new Purchase
        {
            InvoiceNo = $"PUR-PAY-{Guid.NewGuid().ToString("N")[..6]}",
            SupplierName = "Pay Test Supplier",
            GodownId = godown.Id,
            GodownName = godown.Name,
            PurchaseDate = DateTime.Today,
            TotalBags = 50,
            TotalWeightKg = 1250.0,
            TotalAmount = 50000.0,
            Notes = "Stock setup for payment tests"
        };
        purchase.Items.Add(new PurchaseItem
        {
            ItemId = item.Id,
            ItemName = item.ItemName,
            GodownId = godown.Id,
            GodownName = godown.Name,
            PackSizeKg = 25.0,
            Bags = 50,
            TotalKg = 1250.0,
            RatePerKg = 40.0,
            Amount = 50000.0
        });
        service.SavePurchase(purchase);

        var accounts = service.GetAllAccounts();
        var bankAcc = accounts.FirstOrDefault(a => a.AccountType.Equals("Bank", StringComparison.OrdinalIgnoreCase))
                      ?? accounts.FirstOrDefault(a => a.AccountName.Contains("Bank", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(bankAcc);

        // 1. CASH SALE
        string cashCust = "Cash Customer " + Guid.NewGuid().ToString("N")[..4];
        var cashSale = new Sale
        {
            InvoiceNo = $"SAL-CASH-{Guid.NewGuid().ToString("N")[..4]}",
            CustomerName = cashCust,
            GodownId = godown.Id,
            GodownName = godown.Name,
            SaleDate = DateTime.Today,
            TotalBags = 2,
            TotalLooseKg = 0,
            TotalWeightKg = 50.0,
            SubTotal = 2500.0,
            GrandTotal = 2500.0,
            TotalAmount = 2500.0,
            PaymentMode = "Cash",
            CashReceived = 2500.0,
            UpiReceived = 0.0,
            CreditAmount = 0.0,
            Notes = "Full Cash Sale"
        };
        cashSale.Items.Add(new SaleItem
        {
            ItemId = item.Id,
            ItemName = item.ItemName,
            GodownId = godown.Id,
            GodownName = godown.Name,
            PackSizeKg = 25.0,
            BagsSold = 2,
            LooseKgSold = 0,
            TotalKgSold = 50.0,
            RatePerKg = 50.0,
            Amount = 2500.0
        });
        int cashSaleId = service.SaveSale(cashSale);
        var loadedCashSale = service.GetSaleById(cashSaleId);
        Assert.NotNull(loadedCashSale);
        Assert.Equal("Cash", loadedCashSale.PaymentMode);
        Assert.Equal(2500.0, loadedCashSale.CashReceived);
        Assert.Equal(0.0, loadedCashSale.CreditAmount);

        // Check Customer Ledger for Cash Sale: OPENING + Sale Debit 2500 + Receipt Credit 2500 => Closing balance 0
        var cashCustAcc = service.GetAllAccounts().FirstOrDefault(a => a.AccountName == cashCust);
        Assert.NotNull(cashCustAcc);
        var cashCustStatement = service.GetAccountStatement(cashCustAcc.Id);
        Assert.Equal(3, cashCustStatement.Count); // Opening + Sale + Receipt
        Assert.Equal(0.0, cashCustStatement.Last().RunningBalance);

        // 2. UPI SALE
        string upiCust = "UPI Customer " + Guid.NewGuid().ToString("N")[..4];
        var upiSale = new Sale
        {
            InvoiceNo = $"SAL-UPI-{Guid.NewGuid().ToString("N")[..4]}",
            CustomerName = upiCust,
            GodownId = godown.Id,
            GodownName = godown.Name,
            SaleDate = DateTime.Today,
            TotalBags = 2,
            TotalLooseKg = 0,
            TotalWeightKg = 50.0,
            SubTotal = 2500.0,
            GrandTotal = 2500.0,
            TotalAmount = 2500.0,
            PaymentMode = "UPI",
            BankAccountId = bankAcc.Id,
            BankAccountName = bankAcc.AccountName,
            UtrRefNo = "UPI9876543210",
            CashReceived = 0.0,
            UpiReceived = 2500.0,
            CreditAmount = 0.0,
            Notes = "Full UPI Sale"
        };
        upiSale.Items.Add(new SaleItem
        {
            ItemId = item.Id,
            ItemName = item.ItemName,
            GodownId = godown.Id,
            GodownName = godown.Name,
            PackSizeKg = 25.0,
            BagsSold = 2,
            LooseKgSold = 0,
            TotalKgSold = 50.0,
            RatePerKg = 50.0,
            Amount = 2500.0
        });
        int upiSaleId = service.SaveSale(upiSale);
        var loadedUpiSale = service.GetSaleById(upiSaleId);
        Assert.NotNull(loadedUpiSale);
        Assert.Equal("UPI", loadedUpiSale.PaymentMode);
        Assert.Equal(2500.0, loadedUpiSale.UpiReceived);
        Assert.Equal("UPI9876543210", loadedUpiSale.UtrRefNo);

        // Check Bank Ledger: Has Debit Receipt of 2500
        var bankStatement = service.GetAccountStatement(bankAcc.Id);
        Assert.Contains(bankStatement, t => t.Debit == 2500.0 && t.TransactionType == "RECEIPT");

        // 3. SPLIT SALE: 2500 Grand Total => 1000 Cash + 1000 UPI + 500 Credit
        string splitCust = "Split Customer " + Guid.NewGuid().ToString("N")[..4];
        var splitSale = new Sale
        {
            InvoiceNo = $"SAL-SPLIT-{Guid.NewGuid().ToString("N")[..4]}",
            CustomerName = splitCust,
            GodownId = godown.Id,
            GodownName = godown.Name,
            SaleDate = DateTime.Today,
            TotalBags = 2,
            TotalLooseKg = 0,
            TotalWeightKg = 50.0,
            SubTotal = 2500.0,
            GrandTotal = 2500.0,
            TotalAmount = 2500.0,
            PaymentMode = "Split",
            BankAccountId = bankAcc.Id,
            BankAccountName = bankAcc.AccountName,
            UtrRefNo = "SPLIT-UTR-01",
            CashReceived = 1000.0,
            UpiReceived = 1000.0,
            CreditAmount = 500.0,
            Notes = "Split Payment Sale"
        };
        splitSale.Items.Add(new SaleItem
        {
            ItemId = item.Id,
            ItemName = item.ItemName,
            GodownId = godown.Id,
            GodownName = godown.Name,
            PackSizeKg = 25.0,
            BagsSold = 2,
            LooseKgSold = 0,
            TotalKgSold = 50.0,
            RatePerKg = 50.0,
            Amount = 2500.0
        });
        int splitSaleId = service.SaveSale(splitSale);
        var loadedSplitSale = service.GetSaleById(splitSaleId);
        Assert.NotNull(loadedSplitSale);
        Assert.Equal("Split", loadedSplitSale.PaymentMode);
        Assert.Equal(1000.0, loadedSplitSale.CashReceived);
        Assert.Equal(1000.0, loadedSplitSale.UpiReceived);
        Assert.Equal(500.0, loadedSplitSale.CreditAmount);

        // Check Customer Ledger for Split Sale:
        // Total Debit = 2500 (Sale), Total Credit = 2000 (1000 Cash + 1000 UPI) => Net Closing Balance = 500 (Receivable)
        var splitCustAcc = service.GetAllAccounts().FirstOrDefault(a => a.AccountName == splitCust);
        Assert.NotNull(splitCustAcc);
        var splitCustStatement = service.GetAccountStatement(splitCustAcc.Id);
        Assert.Equal(4, splitCustStatement.Count); // Opening + Sale + Cash Receipt + UPI Receipt
        Assert.Equal(500.0, splitCustStatement.Last().RunningBalance);
    }
}
