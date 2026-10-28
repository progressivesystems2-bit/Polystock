using System;
using System.IO;
using System.Linq;
using PolyStock.Core.Models;
using PolyStock.Data;
using PolyStock.Services;
using Xunit;

namespace PolyStock.Tests;

public class LedgerReportTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly InventoryService _inventoryService;

    public LedgerReportTests()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "PolyStockReportTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        _testDbPath = Path.Combine(tempDir, "TestReportPolyStock.accdb");

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
    public void TestGroupSummaryReportCalculation()
    {
        var service = _inventoryService;

        // Create 2 Customer accounts
        var cust1 = new Account
        {
            AccountType = "Customer",
            AccountName = "Alpha Plastic Corp",
            OpeningBalance = 40000.0,
            OpeningBalanceType = "Dr"
        };
        int cust1Id = service.SaveAccount(cust1);

        var cust2 = new Account
        {
            AccountType = "Customer",
            AccountName = "Beta Polymers Ltd",
            OpeningBalance = 60000.0,
            OpeningBalanceType = "Dr"
        };
        int cust2Id = service.SaveAccount(cust2);

        var cashAcc = service.GetAllAccounts().FirstOrDefault(a => a.AccountName.Equals("Cash in Hand", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(cashAcc);

        // Post Receipt of 15,000 for Alpha
        service.SaveVoucher(new FinancialVoucher
        {
            VoucherType = "RECEIPT",
            VoucherNo = "REC-RPT-0001",
            VoucherDate = DateTime.Today,
            DebitAccountId = cashAcc.Id,
            DebitAccountName = cashAcc.AccountName,
            CreditAccountId = cust1Id,
            CreditAccountName = cust1.AccountName,
            Amount = 15000.0,
            PaymentMode = "Cash"
        });

        // Post Receipt of 25,000 for Beta
        service.SaveVoucher(new FinancialVoucher
        {
            VoucherType = "RECEIPT",
            VoucherNo = "REC-RPT-0002",
            VoucherDate = DateTime.Today,
            DebitAccountId = cashAcc.Id,
            DebitAccountName = cashAcc.AccountName,
            CreditAccountId = cust2Id,
            CreditAccountName = cust2.AccountName,
            Amount = 25000.0,
            PaymentMode = "Cash"
        });

        // Fetch Customer Group Summary
        var groupReport = service.GetGroupSummaryReport("Customer");
        Assert.NotEmpty(groupReport);

        var repCust1 = groupReport.FirstOrDefault(a => a.Id == cust1Id);
        Assert.NotNull(repCust1);
        Assert.Equal(15000.0, repCust1.PeriodCredit);
        Assert.Equal(25000.0, repCust1.PeriodClosingBalance);

        var repCust2 = groupReport.FirstOrDefault(a => a.Id == cust2Id);
        Assert.NotNull(repCust2);
        Assert.Equal(25000.0, repCust2.PeriodCredit);
        Assert.Equal(35000.0, repCust2.PeriodClosingBalance);
    }

    [Fact]
    public void TestIndividualAccountStatementRunningBalance()
    {
        var service = _inventoryService;

        var cust = new Account
        {
            AccountType = "Customer",
            AccountName = "Delta Industries",
            OpeningBalance = 10000.0,
            OpeningBalanceType = "Dr"
        };
        int custId = service.SaveAccount(cust);

        var cashAcc = service.GetAllAccounts().FirstOrDefault(a => a.AccountName.Equals("Cash in Hand", StringComparison.OrdinalIgnoreCase));
        var discountAcc = service.GetAllAccounts().FirstOrDefault(a => a.AccountName.Contains("Discount", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(cashAcc);
        Assert.NotNull(discountAcc);

        // Transaction 1: Receipt ₹ 3,000
        service.SaveVoucher(new FinancialVoucher
        {
            VoucherType = "RECEIPT",
            VoucherNo = "REC-DELTA-01",
            VoucherDate = DateTime.Today.AddDays(-5),
            DebitAccountId = cashAcc.Id,
            DebitAccountName = cashAcc.AccountName,
            CreditAccountId = custId,
            CreditAccountName = cust.AccountName,
            Amount = 3000.0,
            PaymentMode = "Cash"
        });

        // Transaction 2: Journal Discount ₹ 500
        service.SaveVoucher(new FinancialVoucher
        {
            VoucherType = "JOURNAL",
            VoucherNo = "JRN-DELTA-01",
            VoucherDate = DateTime.Today.AddDays(-2),
            DebitAccountId = discountAcc.Id,
            DebitAccountName = discountAcc.AccountName,
            CreditAccountId = custId,
            CreditAccountName = cust.AccountName,
            Amount = 500.0,
            PaymentMode = "Adjustment"
        });

        // Fetch Statement
        var statement = service.GetAccountStatement(custId);
        Assert.Equal(3, statement.Count); // Opening + Receipt + Journal

        // Row 0: Opening
        Assert.Equal("OPENING", statement[0].TransactionType);
        Assert.Equal(10000.0, statement[0].RunningBalance);
        Assert.Equal("+", statement[0].RunningBalanceType);

        // Row 1: Receipt 3000 Cr -> Running = 7000 Dr
        Assert.Equal("RECEIPT", statement[1].TransactionType);
        Assert.Equal(3000.0, statement[1].Credit);
        Assert.Equal(7000.0, statement[1].RunningBalance);
        Assert.Equal("+", statement[1].RunningBalanceType);

        // Row 2: Journal 500 Cr -> Running = 6500 Dr
        Assert.Equal("JOURNAL", statement[2].TransactionType);
        Assert.Equal(500.0, statement[2].Credit);
        Assert.Equal(6500.0, statement[2].RunningBalance);
        Assert.Equal("+", statement[2].RunningBalanceType);
    }

    [Fact]
    public void TestStatementDateRangeFilteringAndOpeningBalanceRollforward()
    {
        var service = _inventoryService;

        var supp = new Account
        {
            AccountType = "Supplier",
            AccountName = "Global Petrochem Ltd",
            OpeningBalance = 50000.0,
            OpeningBalanceType = "Cr"
        };
        int suppId = service.SaveAccount(supp);

        var bankAcc = service.GetAllAccounts().FirstOrDefault(a => a.AccountName.Equals("Bank Account", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(bankAcc);

        // Transaction in the past (10 days ago): Payment ₹ 20,000 (Dr 20,000)
        service.SaveVoucher(new FinancialVoucher
        {
            VoucherType = "PAYMENT",
            VoucherNo = "PAY-PAST-01",
            VoucherDate = DateTime.Today.AddDays(-10),
            DebitAccountId = suppId,
            DebitAccountName = supp.AccountName,
            CreditAccountId = bankAcc.Id,
            CreditAccountName = bankAcc.AccountName,
            Amount = 20000.0,
            PaymentMode = "Bank"
        });

        // Transaction within period (Today): Payment ₹ 10,000 (Dr 10,000)
        service.SaveVoucher(new FinancialVoucher
        {
            VoucherType = "PAYMENT",
            VoucherNo = "PAY-CURRENT-01",
            VoucherDate = DateTime.Today,
            DebitAccountId = suppId,
            DebitAccountName = supp.AccountName,
            CreditAccountId = bankAcc.Id,
            CreditAccountName = bankAcc.AccountName,
            Amount = 10000.0,
            PaymentMode = "Bank"
        });

        // Statement filtered from 5 days ago to Today
        DateTime fromDate = DateTime.Today.AddDays(-5);
        DateTime toDate = DateTime.Today;

        var statement = service.GetAccountStatement(suppId, fromDate, toDate);
        Assert.Equal(2, statement.Count); // Opening b/f + Current Payment

        // Initial Opening on fromDate should roll forward past payment:
        // -50,000 (Cr) + 20,000 (Dr) = -30,000 (Cr)
        Assert.Equal("OPENING", statement[0].TransactionType);
        Assert.Equal(-30000.0, statement[0].RunningBalance);
        Assert.Equal("-", statement[0].RunningBalanceType);

        // Current Payment: Dr 10,000 -> Closing = -20,000 (Cr)
        Assert.Equal("PAYMENT", statement[1].TransactionType);
        Assert.Equal(10000.0, statement[1].Debit);
        Assert.Equal(-20000.0, statement[1].RunningBalance);
        Assert.Equal("-", statement[1].RunningBalanceType);
    }

    [Fact]
    public void TestLookupByInvoiceAndVoucherNo()
    {
        var service = _inventoryService;

        // Create Sale
        var sale = new Sale
        {
            InvoiceNo = "INV-LOOKUP-101",
            CustomerName = "Direct Customer",
            SaleDate = DateTime.Today,
            TotalBags = 1,
            TotalLooseKg = 0,
            TotalWeightKg = 25.0,
            TotalAmount = 1500.0
        };
        var saleId = service.SaveSale(sale);
        Assert.True(saleId > 0);

        // Create Purchase
        var purchase = new Purchase
        {
            InvoiceNo = "PUR-LOOKUP-202",
            SupplierName = "Direct Supplier",
            PurchaseDate = DateTime.Today,
            TotalBags = 2,
            TotalWeightKg = 50.0,
            TotalAmount = 2500.0
        };
        var purchaseId = service.SavePurchase(purchase);
        Assert.True(purchaseId > 0);

        // Create Voucher
        var voucher = new FinancialVoucher
        {
            VoucherType = "RECEIPT",
            VoucherNo = "REC-LOOKUP-303",
            VoucherDate = DateTime.Today,
            Amount = 1500.0,
            PaymentMode = "Cash"
        };
        var voucherId = service.SaveVoucher(voucher);
        Assert.True(voucherId > 0);

        // Test Lookups
        var foundSale = service.GetSaleByInvoiceNo("INV-LOOKUP-101");
        Assert.NotNull(foundSale);
        Assert.Equal(saleId, foundSale.Id);
        Assert.Equal("Direct Customer", foundSale.CustomerName);

        var foundPurchase = service.GetPurchaseByInvoiceNo("PUR-LOOKUP-202");
        Assert.NotNull(foundPurchase);
        Assert.Equal(purchaseId, foundPurchase.Id);
        Assert.Equal("Direct Supplier", foundPurchase.SupplierName);

        var foundVoucher = service.GetVoucherByVoucherNo("REC-LOOKUP-303");
        Assert.NotNull(foundVoucher);
        Assert.Equal(voucherId, foundVoucher.Id);
        Assert.Equal("RECEIPT", foundVoucher.VoucherType);
        Assert.Equal(1500.0, foundVoucher.Amount);

        // Test Not Found
        Assert.Null(service.GetSaleByInvoiceNo("NON-EXISTENT"));
        Assert.Null(service.GetPurchaseByInvoiceNo("NON-EXISTENT"));
        Assert.Null(service.GetVoucherByVoucherNo("NON-EXISTENT"));
    }

    [Fact]
    public void TestBillingEnsureTrailingEmptyRow()
    {
        var service = _inventoryService;
        var item = new Item
        {
            ItemName = "High Grade Poly Granules",
            DefaultRatePerKg = 60.0
        };
        service.SaveItem(item);

        // 1. SalesViewModel
        var salesVM = new PolyStock.WPF.ViewModels.SalesViewModel(service);
        // Initially in Browse mode
        Assert.False(salesVM.IsFormActive);
        Assert.True(salesVM.IsBrowseMode);

        // Start new bill to activate
        salesVM.StartNewBill();
        Assert.True(salesVM.IsFormActive);
        Assert.Single(salesVM.Lines);

        // Blank row shouldn't add redundant rows
        salesVM.EnsureTrailingEmptyRow();
        Assert.Single(salesVM.Lines);

        // Fill current row
        salesVM.Lines[0].Item = item;
        salesVM.Lines[0].BagsSold = 2;
        salesVM.EnsureTrailingEmptyRow();
        Assert.Equal(2, salesVM.Lines.Count);

        // Remove row keeps at least 1 row
        salesVM.RemoveLine(salesVM.Lines[1]);
        salesVM.RemoveLine(salesVM.Lines[0]);
        Assert.Single(salesVM.Lines);

        // 2. PurchaseViewModel
        var purchaseVM = new PolyStock.WPF.ViewModels.PurchaseViewModel(service);
        // Initially in Browse mode
        Assert.False(purchaseVM.IsFormActive);
        Assert.True(purchaseVM.IsBrowseMode);

        // Start new purchase to activate
        purchaseVM.StartNewBill();
        Assert.True(purchaseVM.IsFormActive);
        Assert.Single(purchaseVM.Lines);

        // Blank row shouldn't add redundant rows
        purchaseVM.EnsureTrailingEmptyRow();
        Assert.Single(purchaseVM.Lines);

        // Fill current row
        purchaseVM.Lines[0].Item = item;
        purchaseVM.Lines[0].Bags = 5;
        purchaseVM.EnsureTrailingEmptyRow();
        Assert.Equal(2, purchaseVM.Lines.Count);

        // Remove all rows keeps at least 1 row
        purchaseVM.RemoveLine(purchaseVM.Lines[1]);
        purchaseVM.RemoveLine(purchaseVM.Lines[0]);
        Assert.Single(purchaseVM.Lines);
    }

    [Fact]
    public void TestSalesAndPurchaseInactiveByDefaultWorkflow()
    {
        var service = _inventoryService;
        var item = new Item { ItemName = "Poly Resin", DefaultRatePerKg = 50.0 };
        service.SaveItem(item);
        var godown = new Godown { Name = "Main Godown", IsActive = true };
        service.SaveGodown(godown);

        // --- Sales View Model Workflow ---
        var salesVM = new PolyStock.WPF.ViewModels.SalesViewModel(service);
        Assert.Equal(PolyStock.Core.Common.EntryFormMode.Browse, salesVM.FormMode);
        Assert.False(salesVM.IsFormActive);
        Assert.True(salesVM.IsBrowseMode);
        Assert.False(salesVM.CanModify);
        Assert.False(salesVM.CanSave);
        Assert.False(salesVM.CanCancel);

        // Add new bill
        bool focusInvoked = false;
        salesVM.RequestFocusPartyAction = () => focusInvoked = true;
        salesVM.StartNewBill();

        Assert.Equal(PolyStock.Core.Common.EntryFormMode.New, salesVM.FormMode);
        Assert.True(salesVM.IsFormActive);
        Assert.False(salesVM.IsBrowseMode);
        Assert.True(salesVM.CanSave);
        Assert.True(salesVM.CanCancel);
        Assert.True(focusInvoked);

        // Cancel re-locks to browse
        salesVM.Cancel();
        Assert.Equal(PolyStock.Core.Common.EntryFormMode.Browse, salesVM.FormMode);
        Assert.False(salesVM.IsFormActive);
        Assert.True(salesVM.IsBrowseMode);

        // --- Purchase View Model Workflow ---
        var purchaseVM = new PolyStock.WPF.ViewModels.PurchaseViewModel(service);
        Assert.Equal(PolyStock.Core.Common.EntryFormMode.Browse, purchaseVM.FormMode);
        Assert.False(purchaseVM.IsFormActive);
        Assert.True(purchaseVM.IsBrowseMode);
        Assert.False(purchaseVM.CanModify);
        Assert.False(purchaseVM.CanSave);
        Assert.False(purchaseVM.CanCancel);

        focusInvoked = false;
        purchaseVM.RequestFocusPartyAction = () => focusInvoked = true;
        purchaseVM.StartNewBill();

        Assert.Equal(PolyStock.Core.Common.EntryFormMode.New, purchaseVM.FormMode);
        Assert.True(purchaseVM.IsFormActive);
        Assert.False(purchaseVM.IsBrowseMode);
        Assert.True(purchaseVM.CanSave);
        Assert.True(purchaseVM.CanCancel);
        Assert.True(focusInvoked);

        // Cancel re-locks to browse
        purchaseVM.Cancel();
        Assert.Equal(PolyStock.Core.Common.EntryFormMode.Browse, purchaseVM.FormMode);
        Assert.False(purchaseVM.IsFormActive);
        Assert.True(purchaseVM.IsBrowseMode);
    }
}


