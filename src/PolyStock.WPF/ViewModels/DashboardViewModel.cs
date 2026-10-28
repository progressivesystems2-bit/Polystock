using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using PolyStock.Core.Common;
using PolyStock.Core.Models;
using PolyStock.Services;

namespace PolyStock.WPF.ViewModels;

public class DashboardRecentBillItem
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty; // SALE or PURCHASE
    public string TypeDisplay => Type == "SALE" ? "🛒 SALE" : "📥 PURCHASE";
    public string InvoiceNo { get; set; } = string.Empty;
    public string PartyName { get; set; } = string.Empty;
    public string GodownName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int TotalBags { get; set; }
    public double TotalWeightKg { get; set; }
    public double GrandTotal { get; set; }
    public bool IsSale => Type == "SALE";
}

public class DashboardViewModel : ObservableObject
{
    private readonly InventoryService _inventoryService;
    private List<StockItem> _allStock = new();
    private string _searchItemText = string.Empty;
    private DashboardRecentBillItem? _selectedRecentBill;

    public DashboardViewModel(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
        QuickStockList = new ObservableCollection<StockItem>();
        RecentBills = new ObservableCollection<DashboardRecentBillItem>();

        RefreshCommand = new RelayCommand(LoadData);

        // Action shortcuts
        NavigateSalesCommand = new RelayCommand(() => RequestOpenTabAction?.Invoke("Sales"));
        NavigatePurchaseCommand = new RelayCommand(() => RequestOpenTabAction?.Invoke("Purchase"));
        NavigateStockRegisterCommand = new RelayCommand(() => RequestOpenTabAction?.Invoke("StockRegister"));
        NavigateInvoicesCommand = new RelayCommand(() => RequestOpenTabAction?.Invoke("Invoices"));
        NavigateItemsCommand = new RelayCommand(() => RequestOpenTabAction?.Invoke("ItemCreation"));
        NavigateAccountsCommand = new RelayCommand(() => RequestOpenTabAction?.Invoke("AccountCreation"));
        NavigateVouchersCommand = new RelayCommand(() => RequestOpenTabAction?.Invoke("Vouchers"));
        OpenBillCommand = new RelayCommand(param =>
        {
            if (param is DashboardRecentBillItem bill)
            {
                RequestOpenBillAction?.Invoke(bill.Type, bill.Id);
            }
        });

        LoadData();
    }

    public ObservableCollection<StockItem> QuickStockList { get; }
    public ObservableCollection<DashboardRecentBillItem> RecentBills { get; }

    public Action<string>? RequestOpenTabAction { get; set; }
    public Action<string, int>? RequestOpenBillAction { get; set; }

    public double TotalStockKg { get; private set; }
    public int TotalFullBags { get; private set; }
    public double TotalInventoryValuation { get; private set; }
    public int TotalItemsCount { get; private set; }

    public string SearchItemText
    {
        get => _searchItemText;
        set
        {
            if (SetProperty(ref _searchItemText, value))
            {
                ApplyStockSearch();
            }
        }
    }

    public DashboardRecentBillItem? SelectedRecentBill
    {
        get => _selectedRecentBill;
        set
        {
            if (SetProperty(ref _selectedRecentBill, value))
            {
                if (value != null)
                {
                    RequestOpenBillAction?.Invoke(value.Type, value.Id);
                }
            }
        }
    }

    public ICommand RefreshCommand { get; }
    public ICommand NavigateSalesCommand { get; }
    public ICommand NavigatePurchaseCommand { get; }
    public ICommand NavigateStockRegisterCommand { get; }
    public ICommand NavigateInvoicesCommand { get; }
    public ICommand NavigateItemsCommand { get; }
    public ICommand NavigateAccountsCommand { get; }
    public ICommand NavigateVouchersCommand { get; }
    public ICommand OpenBillCommand { get; }

    public void LoadData()
    {
        _allStock = _inventoryService.GetAllStock();
        var allItems = _inventoryService.GetAllItems();

        TotalStockKg = _allStock.Sum(s => s.TotalKg);
        TotalFullBags = _allStock.Sum(s => s.FullBags);
        TotalInventoryValuation = _allStock.Sum(s => s.Valuation);
        TotalItemsCount = allItems.Count;

        OnPropertyChanged(nameof(TotalStockKg));
        OnPropertyChanged(nameof(TotalFullBags));
        OnPropertyChanged(nameof(TotalInventoryValuation));
        OnPropertyChanged(nameof(TotalItemsCount));

        // Populate Recent Bills (Sales + Purchases combined)
        RecentBills.Clear();
        var billsList = new List<DashboardRecentBillItem>();

        var sales = _inventoryService.GetAllSales();
        foreach (var s in sales)
        {
            billsList.Add(new DashboardRecentBillItem
            {
                Id = s.Id,
                Type = "SALE",
                InvoiceNo = s.InvoiceNo,
                PartyName = s.CustomerName,
                GodownName = s.GodownName ?? "(Default)",
                Date = s.SaleDate,
                TotalBags = s.TotalBags,
                TotalWeightKg = s.TotalWeightKg,
                GrandTotal = s.GrandTotal
            });
        }

        var purchases = _inventoryService.GetAllPurchases();
        foreach (var p in purchases)
        {
            billsList.Add(new DashboardRecentBillItem
            {
                Id = p.Id,
                Type = "PURCHASE",
                InvoiceNo = p.InvoiceNo,
                PartyName = p.SupplierName,
                GodownName = p.GodownName ?? "(Default)",
                Date = p.PurchaseDate,
                TotalBags = p.TotalBags,
                TotalWeightKg = p.TotalWeightKg,
                GrandTotal = p.GrandTotal
            });
        }

        foreach (var b in billsList.OrderByDescending(b => b.Date).ThenByDescending(b => b.Id).Take(12))
        {
            RecentBills.Add(b);
        }

        ApplyStockSearch();
    }

    private void ApplyStockSearch()
    {
        QuickStockList.Clear();
        var query = _allStock.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchItemText))
        {
            var terms = SearchItemText.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            query = query.Where(s => terms.All(t =>
                s.ItemName.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                s.HeadGroupName.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                s.BrandName.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(s.GodownName) && s.GodownName.Contains(t, StringComparison.OrdinalIgnoreCase))));
        }

        foreach (var s in query.OrderBy(s => s.ItemName).ThenBy(s => s.GodownName).Take(25))
        {
            QuickStockList.Add(s);
        }
    }
}
