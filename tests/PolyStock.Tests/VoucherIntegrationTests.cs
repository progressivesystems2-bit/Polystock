using System;
using System.IO;
using System.Linq;
using PolyStock.Core.Models;
using PolyStock.Data;
using PolyStock.Services;
using Xunit;

namespace PolyStock.Tests;

public class VoucherIntegrationTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly InventoryService _inventoryService;

    public VoucherIntegrationTests()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "PolyStockVoucherTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        _testDbPath = Path.Combine(tempDir, "TestVoucherPolyStock.accdb");

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
    public void TestReceiptVoucherPostingAndBalanceUpdates()
    {
        var service = _inventoryService;

        // 1. Create a Customer Account
        var cust = new Account
        {
            AccountType = "Customer",
            AccountName = "Shree Balaji Traders",
            OpeningBalance = 50000.0,
            OpeningBalanceType = "Dr"
        };
        int custId = service.SaveAccount(cust);

        var cashAcc = service.GetAllAccounts().FirstOrDefault(a => a.AccountName.Equals("Cash in Hand", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(cashAcc);

        // 2. Post a Receipt Voucher of ₹ 20,000 from Customer into Cash in Hand
        var voucher = new FinancialVoucher
        {
            VoucherType = "RECEIPT",
            VoucherNo = "REC-TEST-0001",
            VoucherDate = DateTime.Today,
            DebitAccountId = cashAcc.Id,
            DebitAccountName = cashAcc.AccountName,
            CreditAccountId = custId,
            CreditAccountName = cust.AccountName,
            Amount = 20000.0,
            PaymentMode = "Cash",
            ReferenceNo = "CHQ-100234",
            Narration = "Part payment received"
        };

        int voucherId = service.SaveVoucher(voucher);
        Assert.True(voucherId > 0);

        // 3. Verify Customer Ledger and Balance
        var updatedCust = service.GetAccountById(custId);
        Assert.NotNull(updatedCust);
        // Opening balance 50,000 Dr - 20,000 Cr = 30,000 Dr
        Assert.Equal(30000.0, updatedCust.CurrentBalance);

        // 4. Verify Cash in Hand Ledger and Balance
        var updatedCash = service.GetAccountById(cashAcc.Id);
        Assert.NotNull(updatedCash);
        // Opening balance 0 + 20,000 Dr = 20,000 Dr
        Assert.Equal(20000.0, updatedCash.CurrentBalance);

        // 5. Verify Ledger entries
        var custLedger = service.GetAccountLedger(custId);
        Assert.Single(custLedger);
        Assert.Equal(20000.0, custLedger[0].Credit);
        Assert.Equal("REC-TEST-0001", custLedger[0].RefInvoiceNo);
    }

    [Fact]
    public void TestPaymentVoucherPostingAndBalanceUpdates()
    {
        var service = _inventoryService;

        // 1. Create a Supplier Account
        var supp = new Account
        {
            AccountType = "Supplier",
            AccountName = "Reliance Petro Supply",
            OpeningBalance = 80000.0,
            OpeningBalanceType = "Cr"
        };
        int suppId = service.SaveAccount(supp);

        var bankAcc = service.GetAllAccounts().FirstOrDefault(a => a.AccountName.Equals("Bank Account", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(bankAcc);

        // 2. Post a Payment Voucher of ₹ 35,000 to Supplier from Bank Account
        var voucher = new FinancialVoucher
        {
            VoucherType = "PAYMENT",
            VoucherNo = "PAY-TEST-0001",
            VoucherDate = DateTime.Today,
            DebitAccountId = suppId,
            DebitAccountName = supp.AccountName,
            CreditAccountId = bankAcc.Id,
            CreditAccountName = bankAcc.AccountName,
            Amount = 35000.0,
            PaymentMode = "NEFT / RTGS",
            ReferenceNo = "UTR99887766",
            Narration = "Vendor advance against purchase"
        };

        int voucherId = service.SaveVoucher(voucher);
        Assert.True(voucherId > 0);

        // 3. Verify Supplier Balance
        var updatedSupp = service.GetAccountById(suppId);
        Assert.NotNull(updatedSupp);
        // Opening balance -80,000 (Cr) + 35,000 (Dr) = -45,000 (Cr)
        Assert.Equal(-45000.0, updatedSupp.CurrentBalance);

        // 4. Verify Bank Balance
        var updatedBank = service.GetAccountById(bankAcc.Id);
        Assert.NotNull(updatedBank);
        // Opening 0 - 35,000 (Cr) = -35,000 (Cr)
        Assert.Equal(-35000.0, updatedBank.CurrentBalance);
    }

    [Fact]
    public void TestJournalVoucherPostingAndBalanceUpdates()
    {
        var service = _inventoryService;

        // 1. Customer with 10,000 Dr
        var cust = new Account
        {
            AccountType = "Customer",
            AccountName = "Royal Polymer Fab",
            OpeningBalance = 10000.0,
            OpeningBalanceType = "Dr"
        };
        int custId = service.SaveAccount(cust);

        var discountAcc = service.GetAllAccounts().FirstOrDefault(a => a.AccountName.Contains("Discount", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(discountAcc);

        // 2. Journal entry: Dr Discount / Round Off ₹ 500, Cr Royal Polymer Fab ₹ 500
        var voucher = new FinancialVoucher
        {
            VoucherType = "JOURNAL",
            VoucherNo = "JRN-TEST-0001",
            VoucherDate = DateTime.Today,
            DebitAccountId = discountAcc.Id,
            DebitAccountName = discountAcc.AccountName,
            CreditAccountId = custId,
            CreditAccountName = cust.AccountName,
            Amount = 500.0,
            PaymentMode = "Adjustment / Transfer",
            Narration = "Special festive discount adjustment"
        };

        service.SaveVoucher(voucher);

        // 3. Customer balance should reduce to 9,500 Dr
        var updatedCust = service.GetAccountById(custId);
        Assert.NotNull(updatedCust);
        Assert.Equal(9500.0, updatedCust.CurrentBalance);

        // 4. Discount expense account should have 500 Dr
        var updatedDisc = service.GetAccountById(discountAcc.Id);
        Assert.NotNull(updatedDisc);
        Assert.Equal(500.0, updatedDisc.CurrentBalance);
    }

    [Fact]
    public void TestVoucherModificationReversesPriorPostings()
    {
        var service = _inventoryService;

        var cust = new Account
        {
            AccountType = "Customer",
            AccountName = "Kisan Polymers",
            OpeningBalance = 100000.0,
            OpeningBalanceType = "Dr"
        };
        int custId = service.SaveAccount(cust);

        var cashAcc = service.GetAllAccounts().FirstOrDefault(a => a.AccountName.Equals("Cash in Hand", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(cashAcc);

        var voucher = new FinancialVoucher
        {
            VoucherType = "RECEIPT",
            VoucherNo = "REC-MOD-0001",
            VoucherDate = DateTime.Today,
            DebitAccountId = cashAcc.Id,
            DebitAccountName = cashAcc.AccountName,
            CreditAccountId = custId,
            CreditAccountName = cust.AccountName,
            Amount = 30000.0,
            PaymentMode = "Cash"
        };

        int voucherId = service.SaveVoucher(voucher);

        // Modify amount from 30,000 to 50,000
        voucher.Id = voucherId;
        voucher.Amount = 50000.0;
        service.SaveVoucher(voucher);

        var updatedCust = service.GetAccountById(custId);
        Assert.NotNull(updatedCust);
        // 100,000 - 50,000 = 50,000 Dr
        Assert.Equal(50000.0, updatedCust.CurrentBalance);

        var updatedCash = service.GetAccountById(cashAcc.Id);
        Assert.NotNull(updatedCash);
        Assert.Equal(50000.0, updatedCash.CurrentBalance);
    }

    [Fact]
    public void TestVoucherDeletionRollsBackLedgerEntries()
    {
        var service = _inventoryService;

        var supp = new Account
        {
            AccountType = "Supplier",
            AccountName = "Gujarat Petrochemicals",
            OpeningBalance = 60000.0,
            OpeningBalanceType = "Cr"
        };
        int suppId = service.SaveAccount(supp);

        var bankAcc = service.GetAllAccounts().FirstOrDefault(a => a.AccountName.Equals("Bank Account", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(bankAcc);

        var voucher = new FinancialVoucher
        {
            VoucherType = "PAYMENT",
            VoucherNo = "PAY-DEL-0001",
            VoucherDate = DateTime.Today,
            DebitAccountId = suppId,
            DebitAccountName = supp.AccountName,
            CreditAccountId = bankAcc.Id,
            CreditAccountName = bankAcc.AccountName,
            Amount = 25000.0,
            PaymentMode = "Bank / Online / UPI"
        };

        int voucherId = service.SaveVoucher(voucher);
        Assert.True(voucherId > 0);

        // Delete voucher
        service.DeleteVoucher(voucherId);

        // Balance must be fully restored
        var updatedSupp = service.GetAccountById(suppId);
        Assert.NotNull(updatedSupp);
        Assert.Equal(-60000.0, updatedSupp.CurrentBalance);

        var suppLedger = service.GetAccountLedger(suppId);
        Assert.Empty(suppLedger);
    }

    [Fact]
    public void TestMultiLinePaymentVoucherPostingAndBalanceUpdates()
    {
        var service = _inventoryService;

        // 1. Setup Accounts: 1 Bank, 2 Suppliers, 1 Expense
        var bankAcc = service.GetAllAccounts().FirstOrDefault(a => a.AccountName.Equals("Bank Account", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(bankAcc);
        double bankOpening = bankAcc.CurrentBalance;

        var supp1 = new Account
        {
            AccountType = "Supplier",
            AccountName = "Shreeji Polymers",
            OpeningBalance = 50000.0,
            OpeningBalanceType = "Cr"
        };
        int s1Id = service.SaveAccount(supp1);

        var supp2 = new Account
        {
            AccountType = "Supplier",
            AccountName = "Apex Petrochem",
            OpeningBalance = 30000.0,
            OpeningBalanceType = "Cr"
        };
        int s2Id = service.SaveAccount(supp2);

        var exp = new Account
        {
            AccountType = "Expense",
            AccountName = "Cartage & Freight Inward",
            OpeningBalance = 0.0,
            OpeningBalanceType = "Dr"
        };
        int expId = service.SaveAccount(exp);

        // 2. Post Multi-Line Payment:
        // Bank Cr: ₹ 55,000
        // Supp1 Dr: ₹ 30,000
        // Supp2 Dr: ₹ 20,000
        // Freight Dr: ₹ 5,000
        var voucher = new FinancialVoucher
        {
            VoucherType = "PAYMENT",
            VoucherNo = "PAY-MULTI-0001",
            VoucherDate = DateTime.Today,
            PrimaryAccountId = bankAcc.Id,
            PrimaryAccountName = bankAcc.AccountName,
            PaymentMode = "Bank / Online / UPI",
            ReferenceNo = "NEFT-888999",
            Narration = "Bulk batch payment for vendors and freight",
            Items = new System.Collections.Generic.List<VoucherLineItem>
            {
                new VoucherLineItem { AccountId = s1Id, AccountName = supp1.AccountName, Amount = 30000.0, ReferenceNo = "INV-101", Remarks = "Part clearance" },
                new VoucherLineItem { AccountId = s2Id, AccountName = supp2.AccountName, Amount = 20000.0, ReferenceNo = "INV-202", Remarks = "On account" },
                new VoucherLineItem { AccountId = expId, AccountName = exp.AccountName, Amount = 5000.0, ReferenceNo = "LR-99", Remarks = "Unloading truck" }
            }
        };

        int vId = service.SaveVoucher(voucher);
        Assert.True(vId > 0);

        // 3. Verify Bank Balance decreased by 55,000
        var updatedBank = service.GetAccountById(bankAcc.Id);
        Assert.NotNull(updatedBank);
        Assert.Equal(bankOpening - 55000.0, updatedBank.CurrentBalance);

        // 4. Verify Supp1 Balance: -50,000 + 30,000 = -20,000 (Cr)
        var updatedS1 = service.GetAccountById(s1Id);
        Assert.NotNull(updatedS1);
        Assert.Equal(-20000.0, updatedS1.CurrentBalance);

        // 5. Verify Supp2 Balance: -30,000 + 20,000 = -10,000 (Cr)
        var updatedS2 = service.GetAccountById(s2Id);
        Assert.NotNull(updatedS2);
        Assert.Equal(-10000.0, updatedS2.CurrentBalance);

        // 6. Verify Expense Balance: 0 + 5,000 = 5,000 (Dr)
        var updatedExp = service.GetAccountById(expId);
        Assert.NotNull(updatedExp);
        Assert.Equal(5000.0, updatedExp.CurrentBalance);

        // 7. Verify Child Items are retrieved on GetById
        var retrieved = service.GetVoucherById(vId);
        Assert.NotNull(retrieved);
        Assert.Equal(3, retrieved.Items.Count);
        Assert.Equal(55000.0, retrieved.TotalAmount);
    }

    [Fact]
    public void TestMultiLineReceiptVoucherPostingAndBalanceUpdates()
    {
        var service = _inventoryService;

        var cashAcc = service.GetAllAccounts().FirstOrDefault(a => a.AccountName.Equals("Cash in Hand", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(cashAcc);
        double cashOpening = cashAcc.CurrentBalance;

        var cust1 = new Account
        {
            AccountType = "Customer",
            AccountName = "Modern Packaging Ltd",
            OpeningBalance = 70000.0,
            OpeningBalanceType = "Dr"
        };
        int c1Id = service.SaveAccount(cust1);

        var cust2 = new Account
        {
            AccountType = "Customer",
            AccountName = "Galaxy Polymers Corp",
            OpeningBalance = 40000.0,
            OpeningBalanceType = "Dr"
        };
        int c2Id = service.SaveAccount(cust2);

        // Post Multi-Line Receipt:
        // Cash Dr: ₹ 65,000
        // Cust1 Cr: ₹ 40,000
        // Cust2 Cr: ₹ 25,000
        var voucher = new FinancialVoucher
        {
            VoucherType = "RECEIPT",
            VoucherNo = "REC-MULTI-0001",
            VoucherDate = DateTime.Today,
            PrimaryAccountId = cashAcc.Id,
            PrimaryAccountName = cashAcc.AccountName,
            PaymentMode = "Cash",
            Narration = "Collection from customers counter",
            Items = new System.Collections.Generic.List<VoucherLineItem>
            {
                new VoucherLineItem { AccountId = c1Id, AccountName = cust1.AccountName, Amount = 40000.0, ReferenceNo = "RCP-01" },
                new VoucherLineItem { AccountId = c2Id, AccountName = cust2.AccountName, Amount = 25000.0, ReferenceNo = "RCP-02" }
            }
        };

        int vId = service.SaveVoucher(voucher);
        Assert.True(vId > 0);

        // Verify Cash: cashOpening + 65,000
        var updatedCash = service.GetAccountById(cashAcc.Id);
        Assert.NotNull(updatedCash);
        Assert.Equal(cashOpening + 65000.0, updatedCash.CurrentBalance);

        // Verify Cust1: 70,000 Dr - 40,000 Cr = 30,000 Dr
        var updatedC1 = service.GetAccountById(c1Id);
        Assert.NotNull(updatedC1);
        Assert.Equal(30000.0, updatedC1.CurrentBalance);

        // Verify Cust2: 40,000 Dr - 25,000 Cr = 15,000 Dr
        var updatedC2 = service.GetAccountById(c2Id);
        Assert.NotNull(updatedC2);
        Assert.Equal(15000.0, updatedC2.CurrentBalance);
    }

    [Fact]
    public void TestMultiLineVoucherModificationAndDeletion()
    {
        var service = _inventoryService;

        var bankAcc = service.GetAllAccounts().FirstOrDefault(a => a.AccountName.Equals("Bank Account", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(bankAcc);
        double initialBank = bankAcc.CurrentBalance;

        var supp = new Account
        {
            AccountType = "Supplier",
            AccountName = "Global Resins Trading",
            OpeningBalance = 100000.0,
            OpeningBalanceType = "Cr"
        };
        int sId = service.SaveAccount(supp);

        var voucher = new FinancialVoucher
        {
            VoucherType = "PAYMENT",
            VoucherNo = "PAY-MODDEL-0001",
            VoucherDate = DateTime.Today,
            PrimaryAccountId = bankAcc.Id,
            PrimaryAccountName = bankAcc.AccountName,
            PaymentMode = "Bank / Online / UPI",
            Items = new System.Collections.Generic.List<VoucherLineItem>
            {
                new VoucherLineItem { AccountId = sId, AccountName = supp.AccountName, Amount = 40000.0 }
            }
        };

        int vId = service.SaveVoucher(voucher);

        // Check after initial save:
        var b1 = service.GetAccountById(bankAcc.Id);
        Assert.Equal(initialBank - 40000.0, b1!.CurrentBalance);
        var s1 = service.GetAccountById(sId);
        Assert.Equal(-60000.0, s1!.CurrentBalance); // -100,000 + 40,000

        // Modify: change amount to 70,000
        voucher.Id = vId;
        voucher.Items[0].Amount = 70000.0;
        service.SaveVoucher(voucher);

        var b2 = service.GetAccountById(bankAcc.Id);
        Assert.Equal(initialBank - 70000.0, b2!.CurrentBalance);
        var s2 = service.GetAccountById(sId);
        Assert.Equal(-30000.0, s2!.CurrentBalance); // -100,000 + 70,000

        // Delete: verify rollback
        service.DeleteVoucher(vId);

        var bFinal = service.GetAccountById(bankAcc.Id);
        Assert.Equal(initialBank, bFinal!.CurrentBalance);
        var sFinal = service.GetAccountById(sId);
        Assert.Equal(-100000.0, sFinal!.CurrentBalance);
    }
}
