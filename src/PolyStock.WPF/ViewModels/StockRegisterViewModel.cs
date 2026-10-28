using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using PolyStock.Core.Common;
using PolyStock.Core.Models;
using PolyStock.Services;
using PolyStock.WPF.Views;

namespace PolyStock.WPF.ViewModels;

public class StockRegisterViewModel : ObservableObject
{
    private readonly InventoryService _inventoryService;
    private StockItem? _selectedStockItem;
    private StockSummaryItem? _selectedSummaryItem;

    // View mode: Summary vs Detailed
    private bool _isDetailedView = true;
    private bool _isSummaryView = false;

    // Filter properties
    private string _selectedHeadGroup = string.Empty;
    private string _selectedBrand = string.Empty;
    private string _searchItemName = string.Empty;
    private Godown? _selectedGodown;
    private string _selectedStockStatus = "All Items";

    public StockRegisterViewModel(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
        StockList = new ObservableCollection<StockItem>();
        FilteredStockList = new ObservableCollection<StockItem>();
        FilteredSummaryList = new ObservableCollection<StockSummaryItem>();

        AvailableHeadGroups = new ObservableCollection<string>();
        AvailableBrands = new ObservableCollection<string>();
        AvailableGodowns = new ObservableCollection<Godown>();
        AvailableStockStatuses = new ObservableCollection<string>
        {
            "All Items",
            "In Stock Only (> 0)",
            "Zero Stock"
        };

        RefreshCommand = new RelayCommand(LoadData);
        ClearFiltersCommand = new RelayCommand(ClearFilters);
        PrintReportCommand = new RelayCommand(PrintReport);
        RebuildStockCommand = new RelayCommand(RebuildStock);

        SwitchToSummaryCommand = new RelayCommand(() => SetViewMode("Summary"));
        SwitchToDetailedCommand = new RelayCommand(() => SetViewMode("Detailed"));

        LoadData();
    }

    public ObservableCollection<StockItem> StockList { get; }
    public ObservableCollection<StockItem> FilteredStockList { get; }
    public ObservableCollection<StockSummaryItem> FilteredSummaryList { get; }

    public ObservableCollection<string> AvailableHeadGroups { get; }
    public ObservableCollection<string> AvailableBrands { get; }
    public ObservableCollection<Godown> AvailableGodowns { get; }
    public ObservableCollection<string> AvailableStockStatuses { get; }

    // View Mode Properties
    public bool IsDetailedView
    {
        get => _isDetailedView;
        set
        {
            if (SetProperty(ref _isDetailedView, value))
            {
                _isSummaryView = !value;
                OnPropertyChanged(nameof(IsSummaryView));
                OnPropertyChanged(nameof(ViewModeTitle));
                OnPropertyChanged(nameof(FilteredItemCount));
            }
        }
    }

    public bool IsSummaryView
    {
        get => _isSummaryView;
        set
        {
            if (SetProperty(ref _isSummaryView, value))
            {
                _isDetailedView = !value;
                OnPropertyChanged(nameof(IsDetailedView));
                OnPropertyChanged(nameof(ViewModeTitle));
                OnPropertyChanged(nameof(FilteredItemCount));
            }
        }
    }

    public string ViewModeTitle => IsSummaryView ? "Summary (Item-Wise)" : "Detailed (Bag-Wise)";

    public StockItem? SelectedStockItem
    {
        get => _selectedStockItem;
        set => SetProperty(ref _selectedStockItem, value);
    }

    public StockSummaryItem? SelectedSummaryItem
    {
        get => _selectedSummaryItem;
        set => SetProperty(ref _selectedSummaryItem, value);
    }

    // Filter Getters / Setters
    public string SelectedHeadGroup
    {
        get => _selectedHeadGroup;
        set
        {
            if (SetProperty(ref _selectedHeadGroup, value))
            {
                ApplyFilter();
            }
        }
    }

    public string SelectedBrand
    {
        get => _selectedBrand;
        set
        {
            if (SetProperty(ref _selectedBrand, value))
            {
                ApplyFilter();
            }
        }
    }

    public string SearchItemName
    {
        get => _searchItemName;
        set
        {
            if (SetProperty(ref _searchItemName, value))
            {
                ApplyFilter();
            }
        }
    }

    public Godown? SelectedGodown
    {
        get => _selectedGodown;
        set
        {
            if (SetProperty(ref _selectedGodown, value))
            {
                ApplyFilter();
            }
        }
    }

    public string SelectedStockStatus
    {
        get => _selectedStockStatus;
        set
        {
            if (SetProperty(ref _selectedStockStatus, value))
            {
                ApplyFilter();
            }
        }
    }

    // Dynamic Filtered Summaries
    public double TotalInventoryKg => FilteredStockList.Sum(s => s.TotalKg);
    public int TotalFullBags => FilteredStockList.Sum(s => s.FullBags);
    public double TotalValuation => FilteredStockList.Sum(s => s.Valuation);
    public int FilteredItemCount => IsSummaryView ? FilteredSummaryList.Count : FilteredStockList.Count;

    public ICommand RefreshCommand { get; }
    public ICommand ClearFiltersCommand { get; }
    public ICommand PrintReportCommand { get; }
    public ICommand RebuildStockCommand { get; }
    public ICommand SwitchToSummaryCommand { get; }
    public ICommand SwitchToDetailedCommand { get; }

    public void SetViewMode(string mode)
    {
        if (mode == "Summary")
        {
            IsSummaryView = true;
        }
        else
        {
            IsDetailedView = true;
        }
    }

    public void LoadData()
    {
        StockList.Clear();
        var list = _inventoryService.GetAllStock();
        foreach (var item in list) StockList.Add(item);

        // Load filter dropdown sources
        AvailableHeadGroups.Clear();
        AvailableHeadGroups.Add(""); // Blank = All
        foreach (var hg in _inventoryService.GetAllHeadGroups().Select(h => h.Name).OrderBy(x => x))
        {
            if (!string.IsNullOrWhiteSpace(hg)) AvailableHeadGroups.Add(hg);
        }

        AvailableBrands.Clear();
        AvailableBrands.Add(""); // Blank = All
        foreach (var b in _inventoryService.GetAllBrands().Select(b => b.Name).OrderBy(x => x))
        {
            if (!string.IsNullOrWhiteSpace(b)) AvailableBrands.Add(b);
        }

        AvailableGodowns.Clear();
        var allGodownItem = new Godown { Id = 0, Name = "(All Godowns)" };
        AvailableGodowns.Add(allGodownItem);
        foreach (var g in _inventoryService.GetAllGodowns().OrderBy(g => g.Name))
        {
            AvailableGodowns.Add(g);
        }

        ApplyFilter();
    }

    public void ClearFilters()
    {
        _selectedHeadGroup = string.Empty;
        _selectedBrand = string.Empty;
        _searchItemName = string.Empty;
        _selectedGodown = AvailableGodowns.FirstOrDefault();
        _selectedStockStatus = "All Items";

        OnPropertyChanged(nameof(SelectedHeadGroup));
        OnPropertyChanged(nameof(SelectedBrand));
        OnPropertyChanged(nameof(SearchItemName));
        OnPropertyChanged(nameof(SelectedGodown));
        OnPropertyChanged(nameof(SelectedStockStatus));

        ApplyFilter();
    }

    public void ApplyFilter()
    {
        FilteredStockList.Clear();
        FilteredSummaryList.Clear();

        var query = StockList.AsEnumerable();

        // 1. HeadGroup filter (exact or partial match)
        if (!string.IsNullOrWhiteSpace(SelectedHeadGroup))
        {
            query = query.Where(s => s.HeadGroupName.Contains(SelectedHeadGroup.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        // 2. Brand filter (exact or partial match)
        if (!string.IsNullOrWhiteSpace(SelectedBrand))
        {
            query = query.Where(s => s.BrandName.Contains(SelectedBrand.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        // 3. ItemName filter (supports partial typing like 8x10, Balaji 7, etc.)
        if (!string.IsNullOrWhiteSpace(SearchItemName))
        {
            var searchTerms = SearchItemName.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            query = query.Where(s => searchTerms.All(term =>
                s.ItemName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                s.HeadGroupName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                s.BrandName.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        // 4. Godown filter
        if (SelectedGodown != null && SelectedGodown.Id > 0)
        {
            query = query.Where(s => s.GodownId == SelectedGodown.Id ||
                                     string.Equals(s.GodownName, SelectedGodown.Name, StringComparison.OrdinalIgnoreCase));
        }

        // 5. Stock Status filter
        if (SelectedStockStatus == "In Stock Only (> 0)")
        {
            query = query.Where(s => s.TotalKg > 0.001);
        }
        else if (SelectedStockStatus == "Zero Stock")
        {
            query = query.Where(s => s.TotalKg <= 0.001);
        }

        var matchingList = query.ToList();

        // Populate Detailed (Bag-wise)
        foreach (var item in matchingList)
        {
            FilteredStockList.Add(item);
        }

        // Populate Summary (Item-wise aggregated)
        var summaryGroups = matchingList
            .GroupBy(s => new { s.ItemId, s.ItemName, s.HeadGroupName, s.BrandName, s.RatePerKg })
            .OrderBy(g => g.Key.ItemName);

        foreach (var grp in summaryGroups)
        {
            int totalBags = grp.Sum(x => x.FullBags);
            double totalKg = grp.Sum(x => x.TotalKg);

            var bagParts = grp
                .Where(x => x.FullBags > 0)
                .OrderByDescending(x => x.PackSizeKg)
                .Select(x => $"{x.FullBags}x{x.PackSizeKg:0.##}kg")
                .ToList();

            string breakdown = bagParts.Count > 0 ? string.Join(", ", bagParts) : "0 Bags";

            FilteredSummaryList.Add(new StockSummaryItem
            {
                ItemId = grp.Key.ItemId,
                ItemName = grp.Key.ItemName,
                HeadGroupName = grp.Key.HeadGroupName,
                BrandName = grp.Key.BrandName,
                RatePerKg = grp.Key.RatePerKg,
                TotalBags = totalBags,
                TotalKg = totalKg,
                BagBreakdownSummary = breakdown
            });
        }

        OnPropertyChanged(nameof(TotalInventoryKg));
        OnPropertyChanged(nameof(TotalFullBags));
        OnPropertyChanged(nameof(TotalValuation));
        OnPropertyChanged(nameof(FilteredItemCount));

        if (SelectedStockItem != null && !FilteredStockList.Contains(SelectedStockItem))
        {
            SelectedStockItem = FilteredStockList.FirstOrDefault();
        }
        else if (SelectedStockItem == null)
        {
            SelectedStockItem = FilteredStockList.FirstOrDefault();
        }

        if (SelectedSummaryItem != null && !FilteredSummaryList.Contains(SelectedSummaryItem))
        {
            SelectedSummaryItem = FilteredSummaryList.FirstOrDefault();
        }
        else if (SelectedSummaryItem == null)
        {
            SelectedSummaryItem = FilteredSummaryList.FirstOrDefault();
        }
    }

    private void PrintReport()
    {
        if (FilteredStockList.Count == 0)
        {
            MessageBox.Show("No stock records match the current filter criteria to print.", "Print Stock Report", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var printWin = new PrintStockReportWindow(this);
            printWin.Owner = Application.Current.MainWindow;
            printWin.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to display stock report preview: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RebuildStock()
    {
        var confirm = MessageBox.Show(
            "This will completely recalculate all live stock on hand directly from your recorded Purchase Inwards and Sales Vouchers.\n\nUse this to fix any discrepancies caused by manual database changes or edits.\n\nDo you want to proceed with stock recalculation?",
            "Recompute & Regenerate Stock",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            var res = _inventoryService.RebuildStockFromVouchers();
            LoadData();

            MessageBox.Show(
                $"Stock Reconciliation Complete!\n\n• Purchases Processed: {res.PurchasesProcessed}\n• Sales Processed: {res.SalesProcessed}\n• Live Stock Rows Updated: {res.StockRowsCreated}\n• Audit Ledger Entries: {res.LedgerEntriesCreated}\n\nAll current stock balances are now 100% reconciled with your vouchers.",
                "Stock Regenerated Successfully",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error regenerating stock: {ex.Message}", "Reconciliation Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
