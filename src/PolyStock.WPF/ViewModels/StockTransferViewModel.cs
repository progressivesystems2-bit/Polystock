using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using PolyStock.Core.Common;
using PolyStock.Core.Models;
using PolyStock.Services;

namespace PolyStock.WPF.ViewModels;

public class StockTransferRowViewModel : ObservableObject
{
    private Item? _item;
    private double? _packSizeKg;
    private int _availableBags;
    private int _bags;
    private double _looseKg;
    private double _ratePerKg;
    private string? _remarks;

    public StockTransferRowViewModel()
    {
        AvailablePackSizes = new ObservableCollection<double>();
    }

    public ObservableCollection<double> AvailablePackSizes { get; }

    public Item? Item
    {
        get => _item;
        set
        {
            if (SetProperty(ref _item, value))
            {
                OnItemChanged();
            }
        }
    }

    public double? PackSizeKg
    {
        get => _packSizeKg;
        set
        {
            if (SetProperty(ref _packSizeKg, value))
            {
                Recalculate();
            }
        }
    }

    public double PackSizeValue => _packSizeKg ?? 0.0;

    public int AvailableBags
    {
        get => _availableBags;
        set
        {
            if (SetProperty(ref _availableBags, value))
            {
                OnPropertyChanged(nameof(IsOverselling));
                OnPropertyChanged(nameof(AvailableStockDisplay));
            }
        }
    }

    public string AvailableStockDisplay => $"{_availableBags} Bags available in Source Godown";

    public int Bags
    {
        get => _bags;
        set
        {
            if (SetProperty(ref _bags, value))
            {
                Recalculate();
                OnPropertyChanged(nameof(IsOverselling));
            }
        }
    }

    public double LooseKg
    {
        get => _looseKg;
        set
        {
            if (SetProperty(ref _looseKg, value))
            {
                Recalculate();
            }
        }
    }

    public double RatePerKg
    {
        get => _ratePerKg;
        set
        {
            if (SetProperty(ref _ratePerKg, value))
            {
                Recalculate();
            }
        }
    }

    public double TotalKg => (Bags * PackSizeValue) + LooseKg;
    public double Amount => TotalKg * RatePerKg;

    public string? Remarks
    {
        get => _remarks;
        set => SetProperty(ref _remarks, value);
    }

    public bool IsOverselling => Bags > AvailableBags && AvailableBags >= 0;

    public bool IsValid => Item != null && PackSizeValue > 0.001 && (Bags > 0 || LooseKg > 0.001);

    public event Action? ItemOrPackSizeChanged;

    public void Recalculate()
    {
        OnPropertyChanged(nameof(PackSizeValue));
        OnPropertyChanged(nameof(TotalKg));
        OnPropertyChanged(nameof(Amount));
        OnPropertyChanged(nameof(IsValid));
        ItemOrPackSizeChanged?.Invoke();
    }

    private void OnItemChanged()
    {
        AvailablePackSizes.Clear();
        if (_item != null)
        {
            RatePerKg = _item.DefaultRatePerKg;

            // Load registered pack sizes or standard defaults
            var packs = _item.PackSizes != null && _item.PackSizes.Count > 0
                ? _item.PackSizes.Distinct().OrderBy(x => x).ToList()
                : new List<double> { 20.0, 25.0, 30.0 };

            foreach (var p in packs)
            {
                AvailablePackSizes.Add(p);
            }

            PackSizeKg = AvailablePackSizes.FirstOrDefault();
        }
        else
        {
            PackSizeKg = null;
            RatePerKg = 0.0;
        }

        Recalculate();
    }
}

public class StockTransferViewModel : ObservableObject
{
    private readonly InventoryService _inventoryService;
    private string _transferNo = string.Empty;
    private DateTime _transferDate = DateTime.Today;
    private Godown? _fromGodown;
    private Godown? _toGodown;
    private string? _notes;
    private string _statusMessage = string.Empty;
    private string _searchHistoryText = string.Empty;
    private StockTransfer? _selectedTransfer;

    public StockTransferViewModel(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;

        AllGodowns = new ObservableCollection<Godown>();
        AllItems = new ObservableCollection<Item>();
        Lines = new ObservableCollection<StockTransferRowViewModel>();
        AllTransfers = new ObservableCollection<StockTransfer>();
        FilteredTransfers = new ObservableCollection<StockTransfer>();

        NewTransferCommand = new RelayCommand(StartNewTransfer);
        SaveTransferCommand = new RelayCommand(SaveTransfer);
        AddLineCommand = new RelayCommand(AddLine);
        RemoveLineCommand = new RelayCommand(param => RemoveLine(param as StockTransferRowViewModel));
        PrintTransferCommand = new RelayCommand(PrintTransfer);
        RefreshHistoryCommand = new RelayCommand(LoadHistory);
        LoadTransferCommand = new RelayCommand(param => LoadTransfer(param as StockTransfer ?? SelectedTransfer));

        LoadMasterData();
        StartNewTransfer();
    }

    public ObservableCollection<Godown> AllGodowns { get; }
    public ObservableCollection<Item> AllItems { get; }
    public ObservableCollection<StockTransferRowViewModel> Lines { get; }
    public ObservableCollection<StockTransfer> AllTransfers { get; }
    public ObservableCollection<StockTransfer> FilteredTransfers { get; }

    public string TransferNo
    {
        get => _transferNo;
        set => SetProperty(ref _transferNo, value);
    }

    public DateTime TransferDate
    {
        get => _transferDate;
        set => SetProperty(ref _transferDate, value);
    }

    public Godown? FromGodown
    {
        get => _fromGodown;
        set
        {
            if (SetProperty(ref _fromGodown, value))
            {
                OnGodownSelectionChanged();
            }
        }
    }

    public Godown? ToGodown
    {
        get => _toGodown;
        set
        {
            if (SetProperty(ref _toGodown, value))
            {
                OnGodownSelectionChanged();
            }
        }
    }

    public string? Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string SearchHistoryText
    {
        get => _searchHistoryText;
        set
        {
            if (SetProperty(ref _searchHistoryText, value))
            {
                FilterHistory();
            }
        }
    }

    public StockTransfer? SelectedTransfer
    {
        get => _selectedTransfer;
        set
        {
            if (SetProperty(ref _selectedTransfer, value) && value != null)
            {
                LoadTransfer(value);
            }
        }
    }

    // Validation & Routing Info
    public bool HasGodownConflict => FromGodown != null && ToGodown != null && FromGodown.Id == ToGodown.Id;
    public string GodownRouteDisplay => (FromGodown != null && ToGodown != null)
        ? $"{FromGodown.Name} ➔ {ToGodown.Name}"
        : "Select Source & Destination Godowns";

    // Summary Totals
    public int TotalBags => Lines.Sum(l => l.Bags);
    public double TotalLooseKg => Lines.Sum(l => l.LooseKg);
    public double TotalWeightKg => Lines.Sum(l => l.TotalKg);
    public double TotalAmount => Lines.Sum(l => l.Amount);
    public int ItemCount => Lines.Count(l => l.Item != null);

    // Commands
    public ICommand NewTransferCommand { get; }
    public ICommand SaveTransferCommand { get; }
    public ICommand AddLineCommand { get; }
    public ICommand RemoveLineCommand { get; }
    public ICommand PrintTransferCommand { get; }
    public ICommand RefreshHistoryCommand { get; }
    public ICommand LoadTransferCommand { get; }

    public void LoadMasterData()
    {
        AllGodowns.Clear();
        foreach (var g in _inventoryService.GetAllGodowns().Where(g => g.IsActive).OrderBy(g => g.Name))
        {
            AllGodowns.Add(g);
        }

        AllItems.Clear();
        foreach (var i in _inventoryService.GetAllItems().OrderBy(i => i.ItemName))
        {
            AllItems.Add(i);
        }

        LoadHistory();
    }

    public void LoadHistory()
    {
        AllTransfers.Clear();
        var list = _inventoryService.GetAllStockTransfers();
        foreach (var t in list)
        {
            AllTransfers.Add(t);
        }
        FilterHistory();
    }

    private void FilterHistory()
    {
        FilteredTransfers.Clear();
        var q = AllTransfers.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(SearchHistoryText))
        {
            string term = SearchHistoryText.Trim();
            q = q.Where(t =>
                t.TransferNo.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                t.FromGodownName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                t.ToGodownName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(t.Notes) && t.Notes.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        foreach (var item in q)
        {
            FilteredTransfers.Add(item);
        }
    }

    public void StartNewTransfer()
    {
        TransferNo = _inventoryService.GetNextStockTransferNo();
        TransferDate = DateTime.Today;
        Notes = string.Empty;
        StatusMessage = "Ready for new stock transfer";

        if (AllGodowns.Count >= 2)
        {
            FromGodown = AllGodowns[0];
            ToGodown = AllGodowns[1];
        }
        else if (AllGodowns.Count == 1)
        {
            FromGodown = AllGodowns[0];
            ToGodown = null;
        }

        Lines.Clear();
        AddLine();
        RecalculateTotals();
    }

    public void AddLine()
    {
        var row = new StockTransferRowViewModel();
        row.ItemOrPackSizeChanged += () =>
        {
            UpdateRowAvailableStock(row);
            RecalculateTotals();
        };

        if (AllItems.Count > 0)
        {
            row.Item = AllItems.FirstOrDefault();
        }

        Lines.Add(row);
        UpdateRowAvailableStock(row);
        RecalculateTotals();
    }

    public void RemoveLine(StockTransferRowViewModel? row)
    {
        if (row != null && Lines.Contains(row))
        {
            Lines.Remove(row);
            if (Lines.Count == 0)
            {
                AddLine();
            }
            RecalculateTotals();
        }
    }

    private void OnGodownSelectionChanged()
    {
        OnPropertyChanged(nameof(HasGodownConflict));
        OnPropertyChanged(nameof(GodownRouteDisplay));

        // Refresh available stock for all rows based on new source godown
        foreach (var row in Lines)
        {
            UpdateRowAvailableStock(row);
        }
    }

    private void UpdateRowAvailableStock(StockTransferRowViewModel row)
    {
        if (row.Item == null || row.PackSizeKg == null || FromGodown == null)
        {
            row.AvailableBags = 0;
            return;
        }

        var stock = _inventoryService.GetStock(row.Item.Id, row.PackSizeKg.Value, FromGodown.Id);
        row.AvailableBags = stock?.FullBags ?? 0;
    }

    public void RecalculateTotals()
    {
        OnPropertyChanged(nameof(TotalBags));
        OnPropertyChanged(nameof(TotalLooseKg));
        OnPropertyChanged(nameof(TotalWeightKg));
        OnPropertyChanged(nameof(TotalAmount));
        OnPropertyChanged(nameof(ItemCount));
    }

    public void SaveTransfer()
    {
        if (FromGodown == null)
        {
            MessageBox.Show("Please select a Source Godown (From Godown).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (ToGodown == null)
        {
            MessageBox.Show("Please select a Destination Godown (To Godown).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (FromGodown.Id == ToGodown.Id)
        {
            MessageBox.Show("Source and Destination Godowns cannot be the same Godown.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var validLines = Lines.Where(l => l.IsValid).ToList();
        if (validLines.Count == 0)
        {
            MessageBox.Show("Please enter at least one item with valid bags or loose quantity.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Check if any row exceeds available bags
        var oversellingRow = validLines.FirstOrDefault(l => l.IsOverselling);
        if (oversellingRow != null)
        {
            var msg = $"Insufficient stock for {oversellingRow.Item?.ItemName} ({oversellingRow.PackSizeValue} kg).\n\n" +
                      $"Requested: {oversellingRow.Bags} Bags\n" +
                      $"Available in {FromGodown.Name}: {oversellingRow.AvailableBags} Bags\n\n" +
                      $"Do you still want to proceed with negative balance transfer?";
            var confirm = MessageBox.Show(msg, "Stock Warning", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }
        }

        try
        {
            var transfer = new StockTransfer
            {
                TransferNo = TransferNo,
                TransferDate = TransferDate,
                FromGodownId = FromGodown.Id,
                FromGodownName = FromGodown.Name,
                ToGodownId = ToGodown.Id,
                ToGodownName = ToGodown.Name,
                TotalBags = TotalBags,
                TotalLooseKg = TotalLooseKg,
                TotalWeightKg = TotalWeightKg,
                TotalAmount = TotalAmount,
                Notes = Notes,
                CreatedAt = DateTime.Now
            };

            foreach (var line in validLines)
            {
                transfer.Items.Add(new StockTransferItem
                {
                    ItemId = line.Item!.Id,
                    ItemName = line.Item.ItemName,
                    PackSizeKg = line.PackSizeValue,
                    Bags = line.Bags,
                    LooseKg = line.LooseKg,
                    TotalKg = line.TotalKg,
                    RatePerKg = line.RatePerKg,
                    Amount = line.Amount,
                    Remarks = line.Remarks
                });
            }

            int transferId = _inventoryService.SaveStockTransfer(transfer);
            StatusMessage = $"Transfer #{transfer.TransferNo} saved successfully!";

            string summary = string.Join("\n", transfer.Items.Select(i => $"  • {i.ItemName} ({i.PackSizeKg}kg): {i.Bags} Bags ({i.TotalKg:F1} kg)"));
            MessageBox.Show(
                $"Stock Transfer #{transfer.TransferNo} completed successfully!\n\n" +
                $"Route: {FromGodown.Name} ➔ {ToGodown.Name}\n" +
                $"Total: {transfer.TotalBags} Bags ({transfer.TotalWeightKg:F1} kg)\n" +
                $"Items:\n{summary}",
                "Transfer Completed",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            LoadHistory();
            StartNewTransfer();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save stock transfer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void LoadTransfer(StockTransfer? transfer)
    {
        if (transfer == null) return;

        var fullTransfer = _inventoryService.GetStockTransferById(transfer.Id);
        if (fullTransfer == null) return;

        TransferNo = fullTransfer.TransferNo;
        TransferDate = fullTransfer.TransferDate;
        FromGodown = AllGodowns.FirstOrDefault(g => g.Id == fullTransfer.FromGodownId);
        ToGodown = AllGodowns.FirstOrDefault(g => g.Id == fullTransfer.ToGodownId);
        Notes = fullTransfer.Notes;

        Lines.Clear();
        foreach (var item in fullTransfer.Items)
        {
            var row = new StockTransferRowViewModel();
            row.ItemOrPackSizeChanged += () =>
            {
                UpdateRowAvailableStock(row);
                RecalculateTotals();
            };

            row.Item = AllItems.FirstOrDefault(i => i.Id == item.ItemId);
            row.PackSizeKg = item.PackSizeKg;
            row.Bags = item.Bags;
            row.LooseKg = item.LooseKg;
            row.RatePerKg = item.RatePerKg;
            row.Remarks = item.Remarks;
            Lines.Add(row);
            UpdateRowAvailableStock(row);
        }

        RecalculateTotals();
        StatusMessage = $"Loaded Transfer #{fullTransfer.TransferNo}";
    }

    public void PrintTransfer()
    {
        var validLines = Lines.Where(l => l.IsValid).ToList();
        if (validLines.Count == 0)
        {
            MessageBox.Show("No items to print.", "Print Transfer", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string linesReport = string.Join("\n", validLines.Select((l, idx) =>
            $"{idx + 1}. {l.Item?.ItemName} | Pack: {l.PackSizeValue} kg | Bags: {l.Bags} | Loose: {l.LooseKg:F1} kg | Total: {l.TotalKg:F1} kg"));

        string challan =
            $"========================================================\n" +
            $"            STOCK TRANSFER DELIVERY CHALLAN\n" +
            $"========================================================\n" +
            $"Transfer No   : {TransferNo}\n" +
            $"Date          : {TransferDate:dd-MMM-yyyy}\n" +
            $"Source Godown : {FromGodown?.Name ?? "—"}\n" +
            $"Target Godown : {ToGodown?.Name ?? "—"}\n" +
            $"Vehicle / Memo: {Notes ?? "—"}\n" +
            $"--------------------------------------------------------\n" +
            $"ITEMS:\n{linesReport}\n" +
            $"--------------------------------------------------------\n" +
            $"TOTAL BAGS    : {TotalBags} Bags\n" +
            $"TOTAL WEIGHT  : {TotalWeightKg:N2} kg ({TotalWeightKg / 1000.0:F3} MT)\n" +
            $"TOTAL VALUE   : ₹ {TotalAmount:N2}\n" +
            $"========================================================\n" +
            $"\nDispatched By: _______________   Received By: _______________";

        MessageBox.Show(challan, $"Stock Transfer Challan - {TransferNo}", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
