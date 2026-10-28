using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using PolyStock.Core.Common;
using PolyStock.Core.Models;
using PolyStock.Services;
using PolyStock.WPF.Views;

namespace PolyStock.WPF.ViewModels;

public class ProfitLossReportViewModel : ObservableObject
{
    private readonly InventoryService _inventoryService;

    // Filters
    private DateTime _fromDate;
    private DateTime _toDate;
    private string _selectedDatePreset = "This Month";
    private string _selectedHeadGroup = "All Categories";
    private string _selectedBrand = "All Brands";
    private string _searchText = string.Empty;
    private string _selectedProfitabilityFilter = "All Items";
    private bool _showOnlySoldItems = true;
    private ProfitLossRow? _selectedRow;

    // Data Cache
    private List<ProfitLossRow> _allReportRows = new();
    private ProfitLossSummary _summary = new();

    public ProfitLossReportViewModel(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;

        FilteredRows = new ObservableCollection<ProfitLossRow>();
        AvailableHeadGroups = new ObservableCollection<string> { "All Categories" };
        AvailableBrands = new ObservableCollection<string> { "All Brands" };
        ProfitabilityFilters = new ObservableCollection<string>
        {
            "All Items",
            "Profitable Only (> 0)",
            "Loss Making Only (< 0)"
        };

        DatePresets = new ObservableCollection<string>
        {
            "Today",
            "This Week",
            "This Month",
            "Last Month",
            "This Financial Year",
            "All Time",
            "Custom"
        };

        RefreshCommand = new RelayCommand(LoadData);
        ClearFiltersCommand = new RelayCommand(ClearFilters);
        ExportCsvCommand = new RelayCommand(ExportCsv, () => CanPrint);
        PrintReportCommand = new RelayCommand(PrintReport, () => CanPrint);

        _inventoryService.Security.UserChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(CanView));
            OnPropertyChanged(nameof(CanPrint));
            if (CanView)
            {
                LoadData();
            }
            else
            {
                _allReportRows.Clear();
                FilteredRows.Clear();
                Summary = new ProfitLossSummary();
            }
        };

        // Set default date range to "This Month"
        ApplyDatePreset("This Month");
        LoadMasterFilters();
        if (CanView)
        {
            LoadData();
        }
    }

    public bool CanView => _inventoryService.Security.CanView(PermissionModules.ProfitLossReport);
    public bool CanPrint => _inventoryService.Security.CanPrint(PermissionModules.ProfitLossReport);

    public ObservableCollection<ProfitLossRow> FilteredRows { get; }
    public ObservableCollection<string> AvailableHeadGroups { get; }
    public ObservableCollection<string> AvailableBrands { get; }
    public ObservableCollection<string> ProfitabilityFilters { get; }
    public ObservableCollection<string> DatePresets { get; }

    public ICommand RefreshCommand { get; }
    public ICommand ClearFiltersCommand { get; }
    public ICommand ExportCsvCommand { get; }
    public ICommand PrintReportCommand { get; }

    public DateTime FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value))
            {
                OnPropertyChanged(nameof(DateRangeDisplay));
                if (_selectedDatePreset != "Custom")
                {
                    _selectedDatePreset = "Custom";
                    OnPropertyChanged(nameof(SelectedDatePreset));
                }
            }
        }
    }

    public DateTime ToDate
    {
        get => _toDate;
        set
        {
            if (SetProperty(ref _toDate, value))
            {
                OnPropertyChanged(nameof(DateRangeDisplay));
                if (_selectedDatePreset != "Custom")
                {
                    _selectedDatePreset = "Custom";
                    OnPropertyChanged(nameof(SelectedDatePreset));
                }
            }
        }
    }

    public string SelectedDatePreset
    {
        get => _selectedDatePreset;
        set
        {
            if (SetProperty(ref _selectedDatePreset, value))
            {
                if (value != "Custom")
                {
                    ApplyDatePreset(value);
                    LoadData();
                }
            }
        }
    }

    public string SelectedHeadGroup
    {
        get => _selectedHeadGroup;
        set
        {
            if (SetProperty(ref _selectedHeadGroup, value))
            {
                ApplyClientFilters();
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
                ApplyClientFilters();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyClientFilters();
            }
        }
    }

    public string SelectedProfitabilityFilter
    {
        get => _selectedProfitabilityFilter;
        set
        {
            if (SetProperty(ref _selectedProfitabilityFilter, value))
            {
                ApplyClientFilters();
            }
        }
    }

    public bool ShowOnlySoldItems
    {
        get => _showOnlySoldItems;
        set
        {
            if (SetProperty(ref _showOnlySoldItems, value))
            {
                LoadData();
            }
        }
    }

    public ProfitLossRow? SelectedRow
    {
        get => _selectedRow;
        set => SetProperty(ref _selectedRow, value);
    }

    public ProfitLossSummary Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    // Display helpers for KPI cards & header
    public string DateRangeDisplay => $"{FromDate:dd-MMM-yyyy}  to  {ToDate:dd-MMM-yyyy}";
    public int FilteredItemCount => FilteredRows.Count;

    public string FormattedTotalRevenue => Summary.FormattedTotalRevenue;
    public string FormattedTotalCogs => Summary.FormattedTotalCogs;
    public string FormattedNetProfitLoss => Summary.FormattedNetProfitLossSigned;
    public string FormattedMarginPercent => Summary.FormattedOverallMargin;
    public string FormattedAvgProfitPerKg => Summary.FormattedAverageProfitPerKg;
    public string FormattedTotalSoldWeight => $"{Summary.TotalSoldKg:N2} kg ({(Summary.TotalSoldKg / 1000.0):N2} MT)";
    public string FormattedTotalPurchasedWeight => $"{Summary.TotalPurchasedKg:N2} kg ({(Summary.TotalPurchasedKg / 1000.0):N2} MT)";
    public bool IsNetProfitable => Summary.IsNetProfitable;

    public void LoadMasterFilters()
    {
        try
        {
            var hgs = _inventoryService.GetAllHeadGroups();
            AvailableHeadGroups.Clear();
            AvailableHeadGroups.Add("All Categories");
            foreach (var hg in hgs.OrderBy(h => h.Name))
            {
                AvailableHeadGroups.Add(hg.Name);
            }

            var brands = _inventoryService.GetAllBrands();
            AvailableBrands.Clear();
            AvailableBrands.Add("All Brands");
            foreach (var b in brands.OrderBy(br => br.Name))
            {
                AvailableBrands.Add(b.Name);
            }
        }
        catch
        {
            // Ignore filter loading errors
        }
    }

    public void LoadData()
    {
        if (!CanView)
        {
            _allReportRows.Clear();
            FilteredRows.Clear();
            Summary = new ProfitLossSummary();
            return;
        }

        try
        {
            DateTime? from = FromDate;
            DateTime? to = ToDate;

            if (_selectedDatePreset == "All Time")
            {
                from = null;
                to = null;
            }

            var (rows, summary) = _inventoryService.GetProfitLossReport(
                from,
                to,
                headGroupId: null,
                brandId: null,
                itemId: null,
                onlySoldItems: ShowOnlySoldItems);

            _allReportRows = rows;
            ApplyClientFilters();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to load Profit & Loss Report:\n{ex.Message}",
                "Report Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ApplyClientFilters()
    {
        IEnumerable<ProfitLossRow> query = _allReportRows;

        // HeadGroup Filter
        if (!string.IsNullOrWhiteSpace(SelectedHeadGroup) && SelectedHeadGroup != "All Categories")
        {
            query = query.Where(r => string.Equals(r.HeadGroupName, SelectedHeadGroup, StringComparison.OrdinalIgnoreCase));
        }

        // Brand Filter
        if (!string.IsNullOrWhiteSpace(SelectedBrand) && SelectedBrand != "All Brands")
        {
            query = query.Where(r => string.Equals(r.BrandName, SelectedBrand, StringComparison.OrdinalIgnoreCase));
        }

        // Profitability Filter
        if (SelectedProfitabilityFilter == "Profitable Only (> 0)")
        {
            query = query.Where(r => r.ProfitLossAmount > 0.001);
        }
        else if (SelectedProfitabilityFilter == "Loss Making Only (< 0)")
        {
            query = query.Where(r => r.ProfitLossAmount < -0.001);
        }

        // Search Text
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string term = SearchText.Trim();
            query = query.Where(r =>
                r.ItemName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.ItemCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.HeadGroupName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.BrandName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var list = query.ToList();
        FilteredRows.Clear();
        foreach (var r in list)
        {
            FilteredRows.Add(r);
        }

        // Recompute summary for filtered subset
        Summary = new ProfitLossSummary
        {
            TotalSalesRevenue = Math.Round(list.Sum(r => r.TotalSalesAmount), 2),
            TotalCogs = Math.Round(list.Sum(r => r.CogsAmount), 2),
            TotalSoldKg = Math.Round(list.Sum(r => r.SoldKg), 3),
            TotalSoldBags = list.Sum(r => r.SoldBags),
            TotalPurchasedKg = Math.Round(list.Sum(r => r.PurchasedKg), 3),
            TotalPurchasedBags = list.Sum(r => r.PurchasedBags),
            ProfitableItemsCount = list.Count(r => r.ProfitLossAmount > 0.001),
            LossItemsCount = list.Count(r => r.ProfitLossAmount < -0.001),
            BreakEvenItemsCount = list.Count(r => Math.Abs(r.ProfitLossAmount) <= 0.001),
            TotalTradedItemsCount = list.Count
        };

        OnPropertyChanged(nameof(FilteredItemCount));
        OnPropertyChanged(nameof(FormattedTotalRevenue));
        OnPropertyChanged(nameof(FormattedTotalCogs));
        OnPropertyChanged(nameof(FormattedNetProfitLoss));
        OnPropertyChanged(nameof(FormattedMarginPercent));
        OnPropertyChanged(nameof(FormattedAvgProfitPerKg));
        OnPropertyChanged(nameof(FormattedTotalSoldWeight));
        OnPropertyChanged(nameof(FormattedTotalPurchasedWeight));
        OnPropertyChanged(nameof(IsNetProfitable));
    }

    public void ClearFilters()
    {
        _selectedHeadGroup = "All Categories";
        _selectedBrand = "All Brands";
        _searchText = string.Empty;
        _selectedProfitabilityFilter = "All Items";

        OnPropertyChanged(nameof(SelectedHeadGroup));
        OnPropertyChanged(nameof(SelectedBrand));
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(SelectedProfitabilityFilter));

        ApplyClientFilters();
    }

    private void ApplyDatePreset(string preset)
    {
        DateTime today = DateTime.Today;

        switch (preset)
        {
            case "Today":
                _fromDate = today;
                _toDate = today;
                break;

            case "This Week":
                int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                _fromDate = today.AddDays(-1 * diff);
                _toDate = _fromDate.AddDays(6);
                break;

            case "This Month":
                _fromDate = new DateTime(today.Year, today.Month, 1);
                _toDate = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
                break;

            case "Last Month":
                var lastMonth = today.AddMonths(-1);
                _fromDate = new DateTime(lastMonth.Year, lastMonth.Month, 1);
                _toDate = new DateTime(lastMonth.Year, lastMonth.Month, DateTime.DaysInMonth(lastMonth.Year, lastMonth.Month));
                break;

            case "This Financial Year":
                int fyStartYear = today.Month >= 4 ? today.Year : today.Year - 1;
                _fromDate = new DateTime(fyStartYear, 4, 1);
                _toDate = new DateTime(fyStartYear + 1, 3, 31);
                break;

            case "All Time":
                _fromDate = new DateTime(2020, 1, 1);
                _toDate = today.AddYears(1);
                break;
        }

        OnPropertyChanged(nameof(FromDate));
        OnPropertyChanged(nameof(ToDate));
        OnPropertyChanged(nameof(DateRangeDisplay));
    }

    public void ExportCsv()
    {
        if (!CanPrint)
        {
            MessageBox.Show("Access Denied: Your account role does not have permission to export this report.", "Permission Restricted", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (FilteredRows.Count == 0)
        {
            MessageBox.Show("There are no records to export.", "Export CSV", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var saveDialog = new SaveFileDialog
        {
            Filter = "CSV (Comma delimited) (*.csv)|*.csv",
            FileName = $"PolyStock_PL_Report_{FromDate:yyyyMMdd}_to_{ToDate:yyyyMMdd}.csv",
            Title = "Export Profit & Loss Report"
        };

        if (saveDialog.ShowDialog() == true)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("PolyStock Polymer Inventory Management");
                sb.AppendLine($"Profit & Loss (P/L) Report: {DateRangeDisplay}");
                sb.AppendLine($"Generated On: {DateTime.Now:dd-MMM-yyyy HH:mm:ss}");
                sb.AppendLine();

                // Summary Ribbon
                sb.AppendLine($"Total Sales Revenue,Rs. {Summary.TotalSalesRevenue:F2}");
                sb.AppendLine($"Total Cost of Goods Sold (COGS),Rs. {Summary.TotalCogs:F2}");
                sb.AppendLine($"Net Profit / Loss,Rs. {Summary.NetProfitLoss:F2}");
                sb.AppendLine($"Overall Margin %,{Summary.OverallMarginPercent:F2}%");
                sb.AppendLine($"Total Sold Bags,{Summary.TotalSoldBags}");
                sb.AppendLine($"Total Sold Weight (kg),{Summary.TotalSoldKg:F2}");
                sb.AppendLine($"Avg Profit / kg,Rs. {Summary.AverageProfitPerKg:F2}/kg");
                sb.AppendLine();

                // Table Header
                sb.AppendLine("Item Code,Item Name,Category,Brand,Sold Bags,Sold Weight (kg),Avg Sale Rate (Rs/kg),Total Sales (Rs),Cost Rate (Rs/kg),Rate Source,COGS (Rs),Profit/Loss (Rs),Profit/kg (Rs),Margin %");

                foreach (var r in FilteredRows)
                {
                    string safeItem = $"\"{r.ItemName.Replace("\"", "\"\"")}\"";
                    string safeHg = $"\"{r.HeadGroupName.Replace("\"", "\"\"")}\"";
                    string safeBrand = $"\"{r.BrandName.Replace("\"", "\"\"")}\"";

                    sb.AppendLine($"{r.ItemCode},{safeItem},{safeHg},{safeBrand},{r.SoldBags},{r.SoldKg:F2},{r.AvgSaleRatePerKg:F2},{r.TotalSalesAmount:F2},{r.EffectivePurchaseRatePerKg:F2},{r.PurchaseRateSource},{r.CogsAmount:F2},{r.ProfitLossAmount:F2},{r.ProfitPerKg:F2},{r.MarginPercent:F2}%");
                }

                // Summary Row
                sb.AppendLine($"TOTAL,,,Summary,{Summary.TotalSoldBags},{Summary.TotalSoldKg:F2},,{Summary.TotalSalesRevenue:F2},,,{Summary.TotalCogs:F2},{Summary.NetProfitLoss:F2},{Summary.AverageProfitPerKg:F2},{Summary.OverallMarginPercent:F2}%");

                File.WriteAllText(saveDialog.FileName, sb.ToString(), Encoding.UTF8);

                MessageBox.Show(
                    $"Report successfully exported to:\n{saveDialog.FileName}",
                    "Export Successful",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to export report:\n{ex.Message}",
                    "Export Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    public void PrintReport()
    {
        if (!CanPrint)
        {
            MessageBox.Show("Access Denied: Your account role does not have permission to print this report.", "Permission Restricted", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (FilteredRows.Count == 0)
        {
            MessageBox.Show("There are no records to print.", "Print Report", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var printWnd = new PrintProfitLossWindow(this);
        printWnd.Owner = Application.Current.MainWindow;
        printWnd.ShowDialog();
    }
}
