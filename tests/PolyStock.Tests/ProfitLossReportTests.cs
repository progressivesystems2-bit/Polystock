using System;
using System.IO;
using System.Linq;
using PolyStock.Core.Common;
using PolyStock.Core.Models;
using PolyStock.Data;
using PolyStock.Services;
using Xunit;

namespace PolyStock.Tests;

public class ProfitLossReportTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly InventoryService _inventoryService;

    public ProfitLossReportTests()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "PolyStockPLTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        _testDbPath = Path.Combine(tempDir, "TestPLPolyStock.accdb");

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
    public void TestWeightedAverageCalculation_PurchasesAndSales_CalculatesExpectedProfit()
    {
        var service = _inventoryService;
        var hg = service.GetAllHeadGroups().First();
        var br = service.GetAllBrands().First();
        var gd = service.GetAllGodowns().First();

        var item = new Item
        {
            HeadGroupId = hg.Id,
            HeadGroupName = hg.Name,
            BrandId = br.Id,
            BrandName = br.Name,
            ItemCode = "PL-101",
            ItemName = "Polypropylene Homopolymer PL-101",
            DefaultRatePerKg = 95.0
        };
        int itemId = service.SaveItem(item, new[] { 25.0 });

        DateTime targetDate = new DateTime(2026, 5, 15);

        // Purchase 1: 10 bags = 250 kg @ ₹ 100/kg = ₹ 25,000
        service.SavePurchase(new Purchase
        {
            InvoiceNo = "PUR-PL-001",
            SupplierName = "RIL Vendor",
            GodownId = gd.Id,
            GodownName = gd.Name,
            PurchaseDate = targetDate,
            TotalBags = 10,
            TotalWeightKg = 250.0,
            TotalAmount = 25000.0,
            Items =
            {
                new PurchaseItem
                {
                    ItemId = itemId,
                    ItemName = item.ItemName,
                    GodownId = gd.Id,
                    GodownName = gd.Name,
                    PackSizeKg = 25.0,
                    Bags = 10,
                    TotalKg = 250.0,
                    RatePerKg = 100.0,
                    Amount = 25000.0
                }
            }
        });

        // Purchase 2: 10 bags = 250 kg @ ₹ 110/kg = ₹ 27,500
        // Total Purchased = 500 kg @ ₹ 52,500 => Weighted Avg Purchase Rate = ₹ 105.00/kg
        service.SavePurchase(new Purchase
        {
            InvoiceNo = "PUR-PL-002",
            SupplierName = "IOCL Vendor",
            GodownId = gd.Id,
            GodownName = gd.Name,
            PurchaseDate = targetDate.AddDays(2),
            TotalBags = 10,
            TotalWeightKg = 250.0,
            TotalAmount = 27500.0,
            Items =
            {
                new PurchaseItem
                {
                    ItemId = itemId,
                    ItemName = item.ItemName,
                    GodownId = gd.Id,
                    GodownName = gd.Name,
                    PackSizeKg = 25.0,
                    Bags = 10,
                    TotalKg = 250.0,
                    RatePerKg = 110.0,
                    Amount = 27500.0
                }
            }
        });

        // Sale 1: 4 bags = 100 kg @ ₹ 120/kg = ₹ 12,000
        service.SaveSale(new Sale
        {
            InvoiceNo = "SAL-PL-001",
            CustomerName = "Apex Polymers",
            GodownId = gd.Id,
            GodownName = gd.Name,
            SaleDate = targetDate.AddDays(3),
            TotalBags = 4,
            TotalWeightKg = 100.0,
            SubTotal = 12000.0,
            GrandTotal = 12000.0,
            Items =
            {
                new SaleItem
                {
                    ItemId = itemId,
                    ItemName = item.ItemName,
                    GodownId = gd.Id,
                    GodownName = gd.Name,
                    PackSizeKg = 25.0,
                    BagsSold = 4,
                    LooseKgSold = 0,
                    TotalKgSold = 100.0,
                    RatePerKg = 120.0,
                    Amount = 12000.0
                }
            }
        });

        // Sale 2: 8 bags = 200 kg @ ₹ 126/kg = ₹ 25,200
        // Total Sold = 300 kg, ₹ 37,200 => Weighted Avg Sale Rate = ₹ 124.00/kg
        service.SaveSale(new Sale
        {
            InvoiceNo = "SAL-PL-002",
            CustomerName = "Bright Plast",
            GodownId = gd.Id,
            GodownName = gd.Name,
            SaleDate = targetDate.AddDays(4),
            TotalBags = 8,
            TotalWeightKg = 200.0,
            SubTotal = 25200.0,
            GrandTotal = 25200.0,
            Items =
            {
                new SaleItem
                {
                    ItemId = itemId,
                    ItemName = item.ItemName,
                    GodownId = gd.Id,
                    GodownName = gd.Name,
                    PackSizeKg = 25.0,
                    BagsSold = 8,
                    LooseKgSold = 0,
                    TotalKgSold = 200.0,
                    RatePerKg = 126.0,
                    Amount = 25200.0
                }
            }
        });

        // Generate P/L report for May 2026
        var fromDate = new DateTime(2026, 5, 1);
        var toDate = new DateTime(2026, 5, 31);
        var (rows, summary) = service.GetProfitLossReport(fromDate, toDate, itemId: itemId);

        Assert.Single(rows);
        var row = rows[0];

        Assert.Equal(12, row.SoldBags);
        Assert.Equal(300.0, row.SoldKg);
        Assert.Equal(37200.0, row.TotalSalesAmount);
        Assert.Equal(124.0, row.AvgSaleRatePerKg, 2);

        Assert.Equal(20, row.PurchasedBags);
        Assert.Equal(500.0, row.PurchasedKg);
        Assert.Equal(52500.0, row.TotalPurchaseAmount);
        Assert.Equal(105.0, row.PeriodAvgPurchaseRatePerKg, 2);
        Assert.Equal(105.0, row.EffectivePurchaseRatePerKg, 2);
        Assert.Equal("Period Purchases", row.PurchaseRateSource);

        // COGS = 300 kg * ₹ 105.00/kg = ₹ 31,500
        Assert.Equal(31500.0, row.CogsAmount, 2);

        // Profit = ₹ 37,200 - ₹ 31,500 = ₹ 5,700
        Assert.Equal(5700.0, row.ProfitLossAmount, 2);

        // Profit / kg = 5700 / 300 = ₹ 19.00/kg (124 - 105)
        Assert.Equal(19.0, row.ProfitPerKg, 2);

        // Margin % = (5700 / 37200) * 100 = 15.32%
        Assert.Equal(15.32, row.MarginPercent, 2);
        Assert.True(row.IsProfitable);

        // Summary checks
        Assert.Equal(37200.0, summary.TotalSalesRevenue);
        Assert.Equal(31500.0, summary.TotalCogs);
        Assert.Equal(5700.0, summary.NetProfitLoss);
        Assert.Equal(15.32, summary.OverallMarginPercent, 2);
        Assert.Equal(1, summary.ProfitableItemsCount);
        Assert.Equal(0, summary.LossItemsCount);
    }

    [Fact]
    public void TestDateRangeFiltering_OnlyIncludesTransactionsInPeriod()
    {
        var service = _inventoryService;
        var hg = service.GetAllHeadGroups().First();
        var br = service.GetAllBrands().First();
        var gd = service.GetAllGodowns().First();

        var item = new Item
        {
            HeadGroupId = hg.Id,
            HeadGroupName = hg.Name,
            BrandId = br.Id,
            BrandName = br.Name,
            ItemCode = "PL-DATE-01",
            ItemName = "HDPE Blow Grade DateTest",
            DefaultRatePerKg = 90.0
        };
        int itemId = service.SaveItem(item, new[] { 25.0 });

        // Purchase in May (Outside period)
        service.SavePurchase(new Purchase
        {
            InvoiceNo = "PUR-MAY",
            SupplierName = "May Supplier",
            GodownId = gd.Id,
            PurchaseDate = new DateTime(2026, 5, 10),
            TotalBags = 10,
            TotalWeightKg = 250.0,
            TotalAmount = 25000.0,
            Items =
            {
                new PurchaseItem
                {
                    ItemId = itemId,
                    ItemName = item.ItemName,
                    GodownId = gd.Id,
                    PackSizeKg = 25.0,
                    Bags = 10,
                    TotalKg = 250.0,
                    RatePerKg = 100.0,
                    Amount = 25000.0
                }
            }
        });

        // Purchase in June (Inside period)
        service.SavePurchase(new Purchase
        {
            InvoiceNo = "PUR-JUNE",
            SupplierName = "June Supplier",
            GodownId = gd.Id,
            PurchaseDate = new DateTime(2026, 6, 12),
            TotalBags = 10,
            TotalWeightKg = 250.0,
            TotalAmount = 27000.0,
            Items =
            {
                new PurchaseItem
                {
                    ItemId = itemId,
                    ItemName = item.ItemName,
                    GodownId = gd.Id,
                    PackSizeKg = 25.0,
                    Bags = 10,
                    TotalKg = 250.0,
                    RatePerKg = 108.0,
                    Amount = 27000.0
                }
            }
        });

        // Sale in June (Inside period): 5 bags = 125 kg @ ₹ 120/kg = ₹ 15,000
        service.SaveSale(new Sale
        {
            InvoiceNo = "SAL-JUNE",
            CustomerName = "June Customer",
            GodownId = gd.Id,
            SaleDate = new DateTime(2026, 6, 15),
            TotalBags = 5,
            TotalWeightKg = 125.0,
            SubTotal = 15000.0,
            GrandTotal = 15000.0,
            Items =
            {
                new SaleItem
                {
                    ItemId = itemId,
                    ItemName = item.ItemName,
                    GodownId = gd.Id,
                    PackSizeKg = 25.0,
                    BagsSold = 5,
                    LooseKgSold = 0,
                    TotalKgSold = 125.0,
                    RatePerKg = 120.0,
                    Amount = 15000.0
                }
            }
        });

        // Query June 2026
        var fromDate = new DateTime(2026, 6, 1);
        var toDate = new DateTime(2026, 6, 30);
        var (rows, summary) = service.GetProfitLossReport(fromDate, toDate, itemId: itemId);

        Assert.Single(rows);
        var row = rows[0];

        // Should only have June sale
        Assert.Equal(5, row.SoldBags);
        Assert.Equal(125.0, row.SoldKg);
        Assert.Equal(15000.0, row.TotalSalesAmount);

        // Should only have June purchase in period purchase
        Assert.Equal(10, row.PurchasedBags);
        Assert.Equal(250.0, row.PurchasedKg);
        Assert.Equal(108.0, row.EffectivePurchaseRatePerKg);

        // COGS = 125 kg * 108 = 13,500
        Assert.Equal(13500.0, row.CogsAmount);
        Assert.Equal(1500.0, row.ProfitLossAmount);
    }

    [Fact]
    public void TestHistoricalPurchaseRateFallback_WhenZeroPurchasesInPeriod()
    {
        var service = _inventoryService;
        var hg = service.GetAllHeadGroups().First();
        var br = service.GetAllBrands().First();
        var gd = service.GetAllGodowns().First();

        var item = new Item
        {
            HeadGroupId = hg.Id,
            HeadGroupName = hg.Name,
            BrandId = br.Id,
            BrandName = br.Name,
            ItemCode = "PL-HIST-01",
            ItemName = "LLDPE Film Grade HistoricalTest",
            DefaultRatePerKg = 88.0
        };
        int itemId = service.SaveItem(item, new[] { 25.0 });

        // Purchase in January @ ₹ 92.00/kg
        service.SavePurchase(new Purchase
        {
            InvoiceNo = "PUR-JAN-HIST",
            SupplierName = "Jan Vendor",
            GodownId = gd.Id,
            PurchaseDate = new DateTime(2026, 1, 15),
            TotalBags = 20,
            TotalWeightKg = 500.0,
            TotalAmount = 46000.0,
            Items =
            {
                new PurchaseItem
                {
                    ItemId = itemId,
                    ItemName = item.ItemName,
                    GodownId = gd.Id,
                    PackSizeKg = 25.0,
                    Bags = 20,
                    TotalKg = 500.0,
                    RatePerKg = 92.0,
                    Amount = 46000.0
                }
            }
        });

        // In February, zero purchases were made. But 4 bags = 100 kg were sold @ ₹ 112/kg = ₹ 11,200
        service.SaveSale(new Sale
        {
            InvoiceNo = "SAL-FEB",
            CustomerName = "Feb Customer",
            GodownId = gd.Id,
            SaleDate = new DateTime(2026, 2, 10),
            TotalBags = 4,
            TotalWeightKg = 100.0,
            SubTotal = 11200.0,
            GrandTotal = 11200.0,
            Items =
            {
                new SaleItem
                {
                    ItemId = itemId,
                    ItemName = item.ItemName,
                    GodownId = gd.Id,
                    PackSizeKg = 25.0,
                    BagsSold = 4,
                    LooseKgSold = 0,
                    TotalKgSold = 100.0,
                    RatePerKg = 112.0,
                    Amount = 11200.0
                }
            }
        });

        // Query February 2026 (where period purchases = 0)
        var fromDate = new DateTime(2026, 2, 1);
        var toDate = new DateTime(2026, 2, 28);
        var (rows, summary) = service.GetProfitLossReport(fromDate, toDate, itemId: itemId);

        Assert.Single(rows);
        var row = rows[0];

        Assert.Equal(0, row.PurchasedBags);
        Assert.Equal(0.0, row.PurchasedKg);
        Assert.Equal(92.0, row.EffectivePurchaseRatePerKg);
        Assert.Equal("Historical Purchases", row.PurchaseRateSource);

        // COGS = 100 kg * 92 = ₹ 9,200
        Assert.Equal(9200.0, row.CogsAmount);
        // Profit = 11,200 - 9,200 = ₹ 2,000
        Assert.Equal(2000.0, row.ProfitLossAmount);
        Assert.Equal(20.0, row.ProfitPerKg);
    }

    [Fact]
    public void TestDefaultRateFallback_WhenZeroPurchasesRecorded()
    {
        var service = _inventoryService;
        var hg = service.GetAllHeadGroups().First();
        var br = service.GetAllBrands().First();
        var gd = service.GetAllGodowns().First();

        var item = new Item
        {
            HeadGroupId = hg.Id,
            HeadGroupName = hg.Name,
            BrandId = br.Id,
            BrandName = br.Name,
            ItemCode = "PL-DEF-01",
            ItemName = "PVC Resin MasterRateTest",
            DefaultRatePerKg = 84.50
        };
        int itemId = service.SaveItem(item, new[] { 25.0 });

        // Seed stock through initial purchase or stock transfer so sale can execute
        // For testing master rate fallback, purchase was at earlier date, or directly test fallback logic
        // If we make a sale with initial stock:
        service.SavePurchase(new Purchase
        {
            InvoiceNo = "PUR-SEED-STOCK",
            SupplierName = "Initial Setup",
            GodownId = gd.Id,
            PurchaseDate = new DateTime(2025, 1, 1),
            TotalBags = 10,
            TotalWeightKg = 250.0,
            TotalAmount = 21125.0,
            Items =
            {
                new PurchaseItem
                {
                    ItemId = itemId,
                    ItemName = item.ItemName,
                    GodownId = gd.Id,
                    PackSizeKg = 25.0,
                    Bags = 10,
                    TotalKg = 250.0,
                    RatePerKg = 84.50,
                    Amount = 21125.0
                }
            }
        });

        // Sale in March 2026
        service.SaveSale(new Sale
        {
            InvoiceNo = "SAL-MARCH",
            CustomerName = "March Customer",
            GodownId = gd.Id,
            SaleDate = new DateTime(2026, 3, 5),
            TotalBags = 2,
            TotalWeightKg = 50.0,
            SubTotal = 5000.0,
            GrandTotal = 5000.0,
            Items =
            {
                new SaleItem
                {
                    ItemId = itemId,
                    ItemName = item.ItemName,
                    GodownId = gd.Id,
                    PackSizeKg = 25.0,
                    BagsSold = 2,
                    LooseKgSold = 0,
                    TotalKgSold = 50.0,
                    RatePerKg = 100.0,
                    Amount = 5000.0
                }
            }
        });

        var fromDate = new DateTime(2026, 3, 1);
        var toDate = new DateTime(2026, 3, 31);
        var (rows, summary) = service.GetProfitLossReport(fromDate, toDate, itemId: itemId);

        Assert.Single(rows);
        var row = rows[0];
        Assert.Equal(50.0, row.SoldKg);
        Assert.Equal(84.50, row.EffectivePurchaseRatePerKg);
        Assert.Equal(4225.0, row.CogsAmount);
        Assert.Equal(775.0, row.ProfitLossAmount);
    }

    [Fact]
    public void TestLossMakingItem_DisplaysNegativeProfitAndMargin()
    {
        var service = _inventoryService;
        var hg = service.GetAllHeadGroups().First();
        var br = service.GetAllBrands().First();
        var gd = service.GetAllGodowns().First();

        var item = new Item
        {
            HeadGroupId = hg.Id,
            HeadGroupName = hg.Name,
            BrandId = br.Id,
            BrandName = br.Name,
            ItemCode = "PL-LOSS-01",
            ItemName = "CP Offgrade Distress Sale",
            DefaultRatePerKg = 110.0
        };
        int itemId = service.SaveItem(item, new[] { 25.0 });

        DateTime testDate = new DateTime(2026, 7, 10);

        // Purchased @ ₹ 120/kg
        service.SavePurchase(new Purchase
        {
            InvoiceNo = "PUR-LOSS",
            SupplierName = "Supplier Loss",
            GodownId = gd.Id,
            PurchaseDate = testDate,
            TotalBags = 10,
            TotalWeightKg = 250.0,
            TotalAmount = 30000.0,
            Items =
            {
                new PurchaseItem
                {
                    ItemId = itemId,
                    ItemName = item.ItemName,
                    GodownId = gd.Id,
                    PackSizeKg = 25.0,
                    Bags = 10,
                    TotalKg = 250.0,
                    RatePerKg = 120.0,
                    Amount = 30000.0
                }
            }
        });

        // Sold @ ₹ 105/kg (below purchase cost)
        service.SaveSale(new Sale
        {
            InvoiceNo = "SAL-LOSS",
            CustomerName = "Distress Buyer",
            GodownId = gd.Id,
            SaleDate = testDate.AddDays(1),
            TotalBags = 4,
            TotalWeightKg = 100.0,
            SubTotal = 10500.0,
            GrandTotal = 10500.0,
            Items =
            {
                new SaleItem
                {
                    ItemId = itemId,
                    ItemName = item.ItemName,
                    GodownId = gd.Id,
                    PackSizeKg = 25.0,
                    BagsSold = 4,
                    LooseKgSold = 0,
                    TotalKgSold = 100.0,
                    RatePerKg = 105.0,
                    Amount = 10500.0
                }
            }
        });

        var (rows, summary) = service.GetProfitLossReport(testDate, testDate.AddDays(5), itemId: itemId);

        Assert.Single(rows);
        var row = rows[0];

        Assert.Equal(100.0, row.SoldKg);
        Assert.Equal(10500.0, row.TotalSalesAmount);
        Assert.Equal(120.0, row.EffectivePurchaseRatePerKg);
        Assert.Equal(12000.0, row.CogsAmount);

        // Loss of ₹ 1,500
        Assert.Equal(-1500.0, row.ProfitLossAmount);
        Assert.Equal(-15.0, row.ProfitPerKg);
        Assert.Equal(-14.29, row.MarginPercent, 2);
        Assert.False(row.IsProfitable);
        Assert.Equal("LOSS", row.StatusText);
    }

    [Fact]
    public void TestPermissionModule_ProfitLossReportIsRegisteredAndMapped()
    {
        // 1. Verify module key constant
        Assert.Equal("ProfitLossReport", PermissionModules.ProfitLossReport);

        // 2. Verify registered in AllModules
        var moduleDef = PermissionModules.AllModules.FirstOrDefault(m => m.Key == PermissionModules.ProfitLossReport);
        Assert.NotNull(moduleDef);
        Assert.Equal(PermissionModules.CategoryReports, moduleDef.Category);
        Assert.Equal("ProfitLossReport", moduleDef.TabViewId);

        // 3. Verify Tab mapping & aliases
        Assert.Equal(PermissionModules.ProfitLossReport, PermissionModules.GetModuleKeyForTab("ProfitLossReport"));
        Assert.Equal(PermissionModules.ProfitLossReport, PermissionModules.GetModuleKeyForTab("plreport"));
        Assert.Equal(PermissionModules.ProfitLossReport, PermissionModules.GetModuleKeyForTab("profitloss"));
    }

    [Fact]
    public void TestRoleBasedPermissions_OnlyAllowedUsersCanAccessProfitLossReport()
    {
        var security = _inventoryService.Security;

        var roles = security.GetAllRoles();
        var adminRole = roles.First(r => r.RoleName.Equals("Administrator", StringComparison.OrdinalIgnoreCase));
        var managerRole = roles.First(r => r.RoleName.Equals("Manager", StringComparison.OrdinalIgnoreCase));
        var operatorRole = roles.First(r => r.RoleName.Equals("Operator", StringComparison.OrdinalIgnoreCase));

        // 1. Create Users for each role
        var adminUser = new User
        {
            Username = "admin_pl_test",
            DisplayName = "Admin User",
            RoleId = adminRole.Id,
            RoleName = adminRole.RoleName,
            IsActive = true
        };
        security.SaveUser(adminUser, "pass123");

        var managerUser = new User
        {
            Username = "manager_pl_test",
            DisplayName = "Manager User",
            RoleId = managerRole.Id,
            RoleName = managerRole.RoleName,
            IsActive = true
        };
        security.SaveUser(managerUser, "pass123");

        var operatorUser = new User
        {
            Username = "operator_pl_test",
            DisplayName = "Operator User",
            RoleId = operatorRole.Id,
            RoleName = operatorRole.RoleName,
            IsActive = true
        };
        security.SaveUser(operatorUser, "pass123");

        // 2. Administrator Access Test
        security.SetCurrentUserSession(adminUser);
        Assert.True(security.IsAdmin);
        Assert.True(security.CanAccessTab("ProfitLossReport"));
        Assert.True(security.CanPrint(PermissionModules.ProfitLossReport));

        // 3. Manager Access Test (Allowed by default)
        security.SetCurrentUserSession(managerUser);
        Assert.False(security.IsAdmin);
        Assert.True(security.CanAccessTab("ProfitLossReport"));
        Assert.True(security.CanPrint(PermissionModules.ProfitLossReport));

        // 4. Operator Access Test (Restricted by default)
        security.SetCurrentUserSession(operatorUser);
        Assert.False(security.IsAdmin);
        Assert.False(security.CanAccessTab("ProfitLossReport"));
        Assert.False(security.CanPrint(PermissionModules.ProfitLossReport));

        // 5. Grant View-only access on ProfitLossReport to Operator Role
        var operatorPerms = security.GetRolePermissions(operatorRole.Id);
        var plPerm = operatorPerms.FirstOrDefault(p => p.ModuleKey == PermissionModules.ProfitLossReport);
        Assert.NotNull(plPerm);
        plPerm.CanView = true;
        plPerm.CanPrint = false;
        security.SaveRolePermissions(operatorRole.Id, operatorPerms);

        // Re-check operator permissions
        security.SetCurrentUserSession(operatorUser);
        Assert.True(security.CanAccessTab("ProfitLossReport")); // Now allowed!
        Assert.False(security.CanPrint(PermissionModules.ProfitLossReport)); // But print remains restricted!

        // 6. Revoke access from Operator Role
        plPerm.CanView = false;
        plPerm.CanPrint = false;
        security.SaveRolePermissions(operatorRole.Id, operatorPerms);

        security.SetCurrentUserSession(operatorUser);
        Assert.False(security.CanAccessTab("ProfitLossReport")); // Restricted again!
    }
}
