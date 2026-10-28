using System;
using System.Collections.Generic;
using PolyStock.Core.Models;
using PolyStock.Data;
using PolyStock.Data.Repositories;

namespace PolyStock.Services;

public class InventoryService
{
    private readonly HeadGroupRepository _headGroupRepo;
    private readonly BrandRepository _brandRepo;
    private readonly GodownRepository _godownRepo;
    private readonly AccountRepository _accountRepo;
    private readonly ItemRepository _itemRepo;
    private readonly StockRepository _stockRepo;
    private readonly PurchaseRepository _purchaseRepo;
    private readonly SaleRepository _saleRepo;
    private readonly VoucherRepository _voucherRepo;
    private readonly StockTransferRepository _stockTransferRepo;
    private readonly ProfitLossRepository _profitLossRepo;
    private readonly SettingsRepository _settingsRepo;
    public SecurityService Security { get; }

    public InventoryService(string? dbPath = null)
    {
        try
        {
            DatabaseInitializer.Initialize(dbPath);
        }
        catch
        {
            // Ignore if DB cannot be initialized or provider unavailable
        }

        _headGroupRepo = new HeadGroupRepository(dbPath);
        _brandRepo = new BrandRepository(dbPath);
        _godownRepo = new GodownRepository(dbPath);
        _accountRepo = new AccountRepository(dbPath);
        _itemRepo = new ItemRepository(dbPath);
        _stockRepo = new StockRepository(dbPath);
        _purchaseRepo = new PurchaseRepository(dbPath);
        _saleRepo = new SaleRepository(dbPath);
        _voucherRepo = new VoucherRepository(dbPath);
        _stockTransferRepo = new StockTransferRepository(dbPath);
        _profitLossRepo = new ProfitLossRepository(dbPath);
        _settingsRepo = new SettingsRepository(dbPath);
        Security = new SecurityService(dbPath);
    }

    // HeadGroups
    public List<HeadGroup> GetAllHeadGroups() => _headGroupRepo.GetAll();
    public int SaveHeadGroup(HeadGroup hg)
    {
        if (hg.Id == 0)
        {
            return _headGroupRepo.Add(hg);
        }
        _headGroupRepo.Update(hg);
        return hg.Id;
    }
    public void DeleteHeadGroup(int id) => _headGroupRepo.Delete(id);

    // Brands
    public List<Brand> GetAllBrands() => _brandRepo.GetAll();
    public int SaveBrand(Brand b)
    {
        if (b.Id == 0)
        {
            return _brandRepo.Add(b);
        }
        _brandRepo.Update(b);
        return b.Id;
    }
    public void DeleteBrand(int id) => _brandRepo.Delete(id);

    // Godowns
    public List<Godown> GetAllGodowns() => _godownRepo.GetAll();
    public int SaveGodown(Godown g)
    {
        if (g.Id == 0)
        {
            return _godownRepo.Add(g);
        }
        _godownRepo.Update(g);
        return g.Id;
    }
    public void DeleteGodown(int id) => _godownRepo.Delete(id);

    // Accounts
    public List<Account> GetAllAccounts() => _accountRepo.GetAll();
    public Account? GetAccountById(int id) => _accountRepo.GetById(id);
    public int SaveAccount(Account a)
    {
        if (a.Id == 0)
        {
            return _accountRepo.Add(a);
        }
        _accountRepo.Update(a);
        return a.Id;
    }
    public void DeleteAccount(int id) => _accountRepo.Delete(id);
    public List<AccountLedgerEntry> GetAccountLedger(int? accountId = null, string? accountName = null) =>
        _accountRepo.GetLedgerEntries(accountId, accountName);
    public List<LedgerStatementItem> GetAccountStatement(int accountId, DateTime? fromDate = null, DateTime? toDate = null) =>
        _accountRepo.GetAccountStatement(accountId, fromDate, toDate);
    public List<Account> GetGroupSummaryReport(string? groupType = null, DateTime? fromDate = null, DateTime? toDate = null) =>
        _accountRepo.GetGroupSummaryReport(groupType, fromDate, toDate);

    // Items
    public List<Item> GetAllItems() => _itemRepo.GetAll();
    public int SaveItem(Item item, IEnumerable<double>? packSizes = null)
    {
        if (item.Id == 0)
        {
            return _itemRepo.Add(item, packSizes);
        }
        _itemRepo.Update(item, packSizes);
        return item.Id;
    }
    public void DeleteItem(int id) => _itemRepo.Delete(id);

    // Stock
    public List<StockItem> GetAllStock() => _stockRepo.GetAllStock();
    public StockItem? GetStock(int itemId, double packSizeKg) => _stockRepo.GetStock(itemId, packSizeKg);
    public StockItem? GetStock(int itemId, double packSizeKg, int? godownId) => _stockRepo.GetStock(itemId, packSizeKg, godownId);
    public List<StockItem> GetStockByGodown(int godownId) => _stockRepo.GetStockByGodown(godownId);
    public List<StockLedgerEntry> GetLedger(int? itemId = null, double? packSizeKg = null) => _stockRepo.GetLedgerEntries(itemId, packSizeKg);
    public RebuildStockResult RebuildStockFromVouchers() => _stockRepo.RebuildStockFromVouchers();

    // Stock Transfers
    public List<StockTransfer> GetAllStockTransfers() => _stockTransferRepo.GetAllTransfers();
    public StockTransfer? GetStockTransferById(int id) => _stockTransferRepo.GetTransferById(id);
    public StockTransfer? GetStockTransferByNo(string transferNo) => _stockTransferRepo.GetTransferByNo(transferNo);
    public string GetNextStockTransferNo() => _stockTransferRepo.GetNextTransferNo();
    public int SaveStockTransfer(StockTransfer transfer) => _stockTransferRepo.SaveTransfer(transfer);

    // Purchases
    public List<Purchase> GetAllPurchases() => _purchaseRepo.GetAll();
    public Purchase? GetPurchaseById(int id) => _purchaseRepo.GetById(id);
    public int SavePurchase(Purchase purchase)
    {
        if (purchase.Id == 0)
        {
            return _purchaseRepo.SavePurchase(purchase);
        }
        _purchaseRepo.UpdatePurchase(purchase);
        return purchase.Id;
    }

    // Purchases
    public Purchase? GetPurchaseByInvoiceNo(string invoiceNo) => _purchaseRepo.GetByInvoiceNo(invoiceNo);

    // Sales
    public List<Sale> GetAllSales() => _saleRepo.GetAll();
    public Sale? GetSaleById(int id) => _saleRepo.GetById(id);
    public Sale? GetSaleByInvoiceNo(string invoiceNo) => _saleRepo.GetByInvoiceNo(invoiceNo);
    public int SaveSale(Sale sale)
    {
        if (sale.Id == 0)
        {
            return _saleRepo.SaveSale(sale);
        }
        _saleRepo.UpdateSale(sale);
        return sale.Id;
    }

    // Financial Vouchers (Payments, Receipts, Journal)
    public List<FinancialVoucher> GetAllVouchers() => _voucherRepo.GetAll();
    public FinancialVoucher? GetVoucherById(int id) => _voucherRepo.GetById(id);
    public FinancialVoucher? GetVoucherByVoucherNo(string voucherNo) => _voucherRepo.GetByVoucherNo(voucherNo);
    public List<FinancialVoucher> GetVouchersByType(string type) => _voucherRepo.GetByType(type);
    public string GetNextVoucherNo(string type) => _voucherRepo.GetNextVoucherNo(type);
    public int SaveVoucher(FinancialVoucher voucher)
    {
        if (voucher.Id == 0)
        {
            return _voucherRepo.SaveVoucher(voucher);
        }
        _voucherRepo.UpdateVoucher(voucher);
        return voucher.Id;
    }
    public void DeleteVoucher(int id) => _voucherRepo.DeleteVoucher(id);

    // Profit & Loss Report
    public (List<ProfitLossRow> Rows, ProfitLossSummary Summary) GetProfitLossReport(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? headGroupId = null,
        int? brandId = null,
        int? itemId = null,
        bool onlySoldItems = true)
    {
        return _profitLossRepo.GetProfitLossReport(fromDate, toDate, headGroupId, brandId, itemId, onlySoldItems);
    }

    // System Settings
    public double GetSalesDeductionThresholdKg() => _settingsRepo.GetSalesDeductionThresholdKg();
    public void SetSalesDeductionThresholdKg(double thresholdKg) => _settingsRepo.SetSalesDeductionThresholdKg(thresholdKg);
    public AppSettings GetAppSettings() => _settingsRepo.GetAppSettings();
    public void SaveAppSettings(AppSettings settings) => _settingsRepo.SaveAppSettings(settings);
}
