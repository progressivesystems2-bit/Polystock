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

public class PurchaseRowViewModel : ObservableObject
{
    private Item? _item;
    private Godown? _selectedGodown;
    private double? _packSizeKg;
    private int _bags;
    private double _ratePerKg;
    private double _effectiveRatePerKg;
    private double _effectiveAmount;
    private int _priorityHeadGroupId;
    private int _priorityBrandId;
    private string? _searchPrefix;

    public int PriorityHeadGroupId
    {
        get => _priorityHeadGroupId;
        set => SetProperty(ref _priorityHeadGroupId, value);
    }

    public int PriorityBrandId
    {
        get => _priorityBrandId;
        set => SetProperty(ref _priorityBrandId, value);
    }

    public string? SearchPrefix
    {
        get => _searchPrefix;
        set => SetProperty(ref _searchPrefix, value);
    }

    public ObservableCollection<double> AvailablePackSizes { get; } = new();

    public Item? Item
    {
        get => _item;
        set
        {
            if (SetProperty(ref _item, value))
            {
                AvailablePackSizes.Clear();
                if (value != null)
                {
                    foreach (var s in value.PackSizes) AvailablePackSizes.Add(s);

                    if (_packSizeKg.HasValue && AvailablePackSizes.Contains(_packSizeKg.Value))
                    {
                        // Keep inherited pack size
                    }
                    else if (AvailablePackSizes.Count > 0)
                    {
                        _packSizeKg = AvailablePackSizes.First();
                    }
                    else
                    {
                        _packSizeKg = 25.0;
                    }

                    OnPropertyChanged(nameof(PackSizeKg));
                    OnPropertyChanged(nameof(PackSizeText));
                    if (_ratePerKg <= 0.0001)
                    {
                        RatePerKg = value.DefaultRatePerKg;
                    }
                }
                Recalculate();
            }
        }
    }

    public Godown? SelectedGodown
    {
        get => _selectedGodown;
        set => SetProperty(ref _selectedGodown, value);
    }

    public double? PackSizeKg
    {
        get => _packSizeKg;
        set
        {
            if (SetProperty(ref _packSizeKg, value))
            {
                OnPropertyChanged(nameof(PackSizeValue));
                OnPropertyChanged(nameof(PackSizeText));
                Recalculate();
            }
        }
    }

    public string PackSizeText
    {
        get => _packSizeKg.HasValue && _packSizeKg.Value > 0 ? _packSizeKg.Value.ToString("0.##") : string.Empty;
        set
        {
            if (double.TryParse(value, out var d) && d > 0)
            {
                PackSizeKg = d;
            }
            else if (string.IsNullOrWhiteSpace(value))
            {
                PackSizeKg = null;
            }
        }
    }

    public double PackSizeValue => _packSizeKg ?? 0.0;

    public int Bags
    {
        get => _bags;
        set
        {
            if (SetProperty(ref _bags, value))
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

    public double TotalKg => Bags * PackSizeValue;
    public double Amount => TotalKg * RatePerKg;

    public double EffectiveRatePerKg
    {
        get => _effectiveRatePerKg > 0.0001 ? _effectiveRatePerKg : RatePerKg;
        set => SetProperty(ref _effectiveRatePerKg, value);
    }

    public double EffectiveAmount
    {
        get => _effectiveAmount > 0.0001 ? _effectiveAmount : Amount;
        set => SetProperty(ref _effectiveAmount, value);
    }

    public void Recalculate()
    {
        OnPropertyChanged(nameof(PackSizeValue));
        OnPropertyChanged(nameof(TotalKg));
        OnPropertyChanged(nameof(Amount));
        OnPropertyChanged(nameof(EffectiveRatePerKg));
        OnPropertyChanged(nameof(EffectiveAmount));
    }
}

public class PurchaseViewModel : ObservableObject
{
    private readonly InventoryService _inventoryService;
    private EntryFormMode _formMode = EntryFormMode.Browse;
    private int _editingPurchaseId = 0;
    private string _invoiceNo = string.Empty;
    private string _supplierName = string.Empty;
    private Godown? _selectedGodown;
    private DateTime _purchaseDate = DateTime.Today;
    private string? _notes;
    private Purchase? _selectedBill;

    private double _gstPercent = 0.0;
    private double _gstAmount = 0.0;
    private double _freight = 0.0;
    private double _loadingUnloading = 0.0;
    private bool _isUpdatingGst = false;

    public PurchaseViewModel(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
        AllItems = new ObservableCollection<Item>();
        AllAccounts = new ObservableCollection<Account>();
        AllGodowns = new ObservableCollection<Godown>();
        Lines = new ObservableCollection<PurchaseRowViewModel>();
        AllBills = new ObservableCollection<Purchase>();

        AddLineCommand = new RelayCommand(AddLine);
        RemoveLineCommand = new RelayCommand(param => RemoveLine(param as PurchaseRowViewModel));
        SaveCommand = new RelayCommand(Save);
        AddNewCommand = new RelayCommand(StartNewBill);
        NewCommand = new RelayCommand(StartNewBill);
        ModifyCommand = new RelayCommand(StartModify);
        CancelCommand = new RelayCommand(Cancel);
        PrintCommand = new RelayCommand(Print);
        ListCommand = new RelayCommand(OpenList);

        LoadItems();
        LoadAccounts();
        LoadGodowns();
        LoadBills();
        ResetForm();
        FormMode = EntryFormMode.Browse;
    }

    public ObservableCollection<Item> AllItems { get; }
    public ObservableCollection<Account> AllAccounts { get; }
    public ObservableCollection<Godown> AllGodowns { get; }
    public ObservableCollection<PurchaseRowViewModel> Lines { get; }
    public ObservableCollection<Purchase> AllBills { get; }

    public EntryFormMode FormMode
    {
        get => _formMode;
        set
        {
            if (SetProperty(ref _formMode, value))
            {
                OnPropertyChanged(nameof(IsFormActive));
                OnPropertyChanged(nameof(IsBrowseMode));
                OnPropertyChanged(nameof(CanModify));
                OnPropertyChanged(nameof(CanSave));
                OnPropertyChanged(nameof(CanCancel));
                OnPropertyChanged(nameof(BillStatusText));
            }
        }
    }

    public bool IsFormActive => _formMode != EntryFormMode.Browse;
    public bool IsBrowseMode => _formMode == EntryFormMode.Browse;
    public bool CanModify => _formMode == EntryFormMode.Browse && _editingPurchaseId > 0;
    public bool CanSave => IsFormActive;
    public bool CanCancel => IsFormActive;

    public int EditingPurchaseId
    {
        get => _editingPurchaseId;
        set
        {
            if (SetProperty(ref _editingPurchaseId, value))
            {
                OnPropertyChanged(nameof(IsEditingExistingBill));
                OnPropertyChanged(nameof(CanModify));
                OnPropertyChanged(nameof(BillStatusText));
            }
        }
    }

    public bool IsEditingExistingBill => _editingPurchaseId > 0;

    public string BillStatusText
    {
        get
        {
            return _formMode switch
            {
                EntryFormMode.New => "✨ NEW PURCHASE: Inward Mode",
                EntryFormMode.Edit => $"✏ MODIFICATION MODE: Editing Bill #{InvoiceNo}",
                _ => _editingPurchaseId > 0
                    ? $"🔒 VIEW MODE: Purchase #{InvoiceNo} (Click Modify to edit)"
                    : "🔒 VIEW MODE: Press Ctrl+N to add new purchase or load a bill above"
            };
        }
    }

    public Purchase? SelectedBill
    {
        get => _selectedBill;
        set
        {
            if (SetProperty(ref _selectedBill, value))
            {
                if (value != null)
                {
                    LoadBill(value);
                }
            }
        }
    }

    public string InvoiceNo
    {
        get => _invoiceNo;
        set
        {
            if (SetProperty(ref _invoiceNo, value))
            {
                OnPropertyChanged(nameof(BillStatusText));
            }
        }
    }

    public string SupplierName
    {
        get => _supplierName;
        set => SetProperty(ref _supplierName, value);
    }

    public Godown? SelectedGodown
    {
        get => _selectedGodown;
        set
        {
            if (SetProperty(ref _selectedGodown, value))
            {
                // Auto-copy bill-level godown to all existing lines in the bill
                if (value != null)
                {
                    foreach (var line in Lines)
                    {
                        line.SelectedGodown = value;
                    }
                }
            }
        }
    }

    public DateTime PurchaseDate
    {
        get => _purchaseDate;
        set => SetProperty(ref _purchaseDate, value);
    }

    public string? Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    // Bill-Level Calculations
    public double SubTotal => Lines.Sum(l => l.Amount);

    public double GstPercent
    {
        get => _gstPercent;
        set
        {
            if (!_isUpdatingGst)
            {
                _isUpdatingGst = true;
                _gstPercent = value;
                _gstAmount = Math.Round(SubTotal * _gstPercent / 100.0, 2);
                OnPropertyChanged(nameof(GstPercent));
                OnPropertyChanged(nameof(GstAmount));
                OnPropertyChanged(nameof(PartyAmount));
                OnPropertyChanged(nameof(GrandTotal));
                OnPropertyChanged(nameof(TotalAmount));
                _isUpdatingGst = false;
                RecalculateTotals();
            }
            else
            {
                SetProperty(ref _gstPercent, value);
            }
        }
    }

    public double GstAmount
    {
        get => _gstAmount;
        set
        {
            if (!_isUpdatingGst)
            {
                _isUpdatingGst = true;
                _gstAmount = value;
                _gstPercent = SubTotal > 0.0001 ? Math.Round((_gstAmount / SubTotal) * 100.0, 2) : 0.0;
                OnPropertyChanged(nameof(GstPercent));
                OnPropertyChanged(nameof(GstAmount));
                OnPropertyChanged(nameof(PartyAmount));
                OnPropertyChanged(nameof(GrandTotal));
                OnPropertyChanged(nameof(TotalAmount));
                _isUpdatingGst = false;
                RecalculateTotals();
            }
            else
            {
                SetProperty(ref _gstAmount, value);
            }
        }
    }

    public double Freight
    {
        get => _freight;
        set
        {
            if (SetProperty(ref _freight, value))
            {
                OnPropertyChanged(nameof(GrandTotal));
                OnPropertyChanged(nameof(TotalAmount));
                RecalculateTotals();
            }
        }
    }

    public double LoadingUnloading
    {
        get => _loadingUnloading;
        set
        {
            if (SetProperty(ref _loadingUnloading, value))
            {
                OnPropertyChanged(nameof(GrandTotal));
                OnPropertyChanged(nameof(TotalAmount));
                RecalculateTotals();
            }
        }
    }

    public double TotalAdditionalCost => GstAmount + Freight + LoadingUnloading;
    public double AverageEffectiveRatePerKg => TotalWeightKg > 0.0001 ? GrandTotal / TotalWeightKg : 0.0;

    public double PartyAmount => SubTotal + GstAmount;
    public double GrandTotal => SubTotal + GstAmount + Freight + LoadingUnloading;

    public int TotalBags => Lines.Sum(l => l.Bags);
    public double TotalWeightKg => Lines.Sum(l => l.TotalKg);
    public double TotalAmount => GrandTotal > 0 ? GrandTotal : (SubTotal > 0 ? SubTotal : 0.0);

    public ICommand AddLineCommand { get; }
    public ICommand RemoveLineCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand AddNewCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand ModifyCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand ListCommand { get; }

    public Action? RequestFocusPartyAction { get; set; }
    public Action? RequestOpenHistoryAction { get; set; }

    public void LoadItems()
    {
        AllItems.Clear();
        foreach (var item in _inventoryService.GetAllItems()) AllItems.Add(item);
    }

    public void LoadAccounts()
    {
        AllAccounts.Clear();
        foreach (var a in _inventoryService.GetAllAccounts()) AllAccounts.Add(a);
    }

    public void LoadGodowns()
    {
        AllGodowns.Clear();
        foreach (var g in _inventoryService.GetAllGodowns()) AllGodowns.Add(g);
    }

    public void LoadBills()
    {
        AllBills.Clear();
        var purchases = _inventoryService.GetAllPurchases();
        foreach (var p in purchases) AllBills.Add(p);
    }

    public void LoadBill(Purchase bill)
    {
        var fullPurchase = _inventoryService.GetPurchaseById(bill.Id) ?? bill;
        EditingPurchaseId = fullPurchase.Id;
        InvoiceNo = fullPurchase.InvoiceNo;
        SupplierName = fullPurchase.SupplierName;
        SelectedGodown = AllGodowns.FirstOrDefault(g => g.Id == fullPurchase.GodownId);
        PurchaseDate = fullPurchase.PurchaseDate;
        Notes = fullPurchase.Notes;

        _isUpdatingGst = true;
        _gstPercent = fullPurchase.GstPercent;
        _gstAmount = fullPurchase.GstAmount;
        _freight = fullPurchase.Freight;
        _loadingUnloading = fullPurchase.LoadingUnloading;
        _isUpdatingGst = false;

        OnPropertyChanged(nameof(SupplierName));
        OnPropertyChanged(nameof(SelectedGodown));
        OnPropertyChanged(nameof(PurchaseDate));
        OnPropertyChanged(nameof(Notes));
        OnPropertyChanged(nameof(GstPercent));
        OnPropertyChanged(nameof(GstAmount));
        OnPropertyChanged(nameof(Freight));
        OnPropertyChanged(nameof(LoadingUnloading));

        Lines.Clear();
        foreach (var item in fullPurchase.Items)
        {
            var matchedItem = AllItems.FirstOrDefault(i => i.Id == item.ItemId);
            var itemGodown = AllGodowns.FirstOrDefault(g => g.Id == item.GodownId) ?? SelectedGodown;

            var row = new PurchaseRowViewModel
            {
                Item = matchedItem,
                SelectedGodown = itemGodown,
                PackSizeKg = item.PackSizeKg,
                Bags = item.Bags,
                RatePerKg = item.RatePerKg,
                EffectiveRatePerKg = item.LandedCost > 0.0001 ? item.LandedCost : item.RatePerKg
            };
            row.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(PurchaseRowViewModel.Item))
                {
                    UpdateRowPriorities();
                }
                RecalculateTotals();
            };
            Lines.Add(row);
        }

        UpdateRowPriorities();
        RecalculateTotals();
        FormMode = EntryFormMode.Browse;
        OnPropertyChanged(nameof(CanModify));
    }

    public void UpdateRowPriorities()
    {
        var primaryItem = Lines.FirstOrDefault(l => l.Item != null)?.Item;
        int hgId = primaryItem?.HeadGroupId ?? 0;
        int bId = primaryItem?.BrandId ?? 0;
        string prefix = primaryItem != null ? Controls.SearchableItemWrapper.GetPrefixUpToHeadGroupAndBrand(primaryItem) : string.Empty;

        for (int i = 0; i < Lines.Count; i++)
        {
            if (i > 0)
            {
                Lines[i].PriorityHeadGroupId = hgId;
                Lines[i].PriorityBrandId = bId;
                if (Lines[i].Item == null)
                {
                    Lines[i].SearchPrefix = prefix;
                }
            }
            else
            {
                Lines[i].PriorityHeadGroupId = 0;
                Lines[i].PriorityBrandId = 0;
                Lines[i].SearchPrefix = null;
            }
        }
    }

    public void AddLine()
    {
        if (!IsFormActive) return;
        var prevRow = Lines.LastOrDefault(l => l.Item != null);
        var row = new PurchaseRowViewModel
        {
            SelectedGodown = prevRow?.SelectedGodown ?? SelectedGodown,
            PackSizeKg = prevRow?.PackSizeKg,
            RatePerKg = prevRow?.RatePerKg ?? 0.0
        };
        row.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(PurchaseRowViewModel.Item))
            {
                UpdateRowPriorities();
            }
            RecalculateTotals();
        };
        Lines.Add(row);
        UpdateRowPriorities();
        RecalculateTotals();
    }

    public void EnsureTrailingEmptyRow()
    {
        if (!IsFormActive) return;
        var last = Lines.LastOrDefault();
        if (last == null || last.Item != null || last.Bags > 0)
        {
            AddLine();
        }
    }

    public void RemoveLine(PurchaseRowViewModel? row)
    {
        if (!IsFormActive) return;
        if (row != null)
        {
            Lines.Remove(row);
            if (Lines.Count == 0)
            {
                AddLine();
            }
            UpdateRowPriorities();
            RecalculateTotals();
        }
    }

    public void RecalculateTotals()
    {
        if (!_isUpdatingGst)
        {
            _isUpdatingGst = true;
            if (_gstPercent > 0.0001)
            {
                _gstAmount = Math.Round(SubTotal * _gstPercent / 100.0, 2);
            }
            else if (_gstAmount > 0.0001 && SubTotal > 0.0001)
            {
                _gstPercent = Math.Round((_gstAmount / SubTotal) * 100.0, 2);
            }
            _isUpdatingGst = false;
        }

        double billSubTotal = SubTotal;
        double additionalCost = TotalAdditionalCost;

        foreach (var line in Lines)
        {
            if (billSubTotal > 0.0001 && line.TotalKg > 0.0001)
            {
                double lineAdditional = (line.Amount / billSubTotal) * additionalCost;
                line.EffectiveAmount = Math.Round(line.Amount + lineAdditional, 2);
                line.EffectiveRatePerKg = Math.Round(line.EffectiveAmount / line.TotalKg, 2);
            }
            else
            {
                line.EffectiveAmount = line.Amount;
                line.EffectiveRatePerKg = line.RatePerKg;
            }
        }

        OnPropertyChanged(nameof(TotalBags));
        OnPropertyChanged(nameof(TotalWeightKg));
        OnPropertyChanged(nameof(SubTotal));
        OnPropertyChanged(nameof(GstPercent));
        OnPropertyChanged(nameof(GstAmount));
        OnPropertyChanged(nameof(Freight));
        OnPropertyChanged(nameof(LoadingUnloading));
        OnPropertyChanged(nameof(TotalAdditionalCost));
        OnPropertyChanged(nameof(AverageEffectiveRatePerKg));
        OnPropertyChanged(nameof(PartyAmount));
        OnPropertyChanged(nameof(GrandTotal));
        OnPropertyChanged(nameof(TotalAmount));
    }

    public void ResetForm()
    {
        _editingPurchaseId = 0;
        _selectedBill = null;
        OnPropertyChanged(nameof(SelectedBill));
        OnPropertyChanged(nameof(EditingPurchaseId));
        OnPropertyChanged(nameof(IsEditingExistingBill));
        OnPropertyChanged(nameof(CanModify));
        OnPropertyChanged(nameof(BillStatusText));

        LoadGodowns();
        LoadAccounts();
        LoadBills();
        int nextSeq = AllBills.Count + 1;
        InvoiceNo = $"PUR-{DateTime.Now:yyyyMMdd}-{nextSeq:D4}";
        SupplierName = string.Empty;
        SelectedGodown = AllGodowns.FirstOrDefault(g => g.IsActive) ?? AllGodowns.FirstOrDefault();
        PurchaseDate = DateTime.Today;
        Notes = string.Empty;
        _isUpdatingGst = true;
        _gstPercent = 0.0;
        _gstAmount = 0.0;
        _freight = 0.0;
        _loadingUnloading = 0.0;
        _isUpdatingGst = false;
        Lines.Clear();
        LoadItems();

        var row = new PurchaseRowViewModel
        {
            SelectedGodown = SelectedGodown
        };
        row.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(PurchaseRowViewModel.Item))
            {
                UpdateRowPriorities();
            }
            RecalculateTotals();
        };
        Lines.Add(row);
        UpdateRowPriorities();

        RecalculateTotals();
        OnPropertyChanged(nameof(SupplierName));
        OnPropertyChanged(nameof(SelectedGodown));
        OnPropertyChanged(nameof(Notes));
        OnPropertyChanged(nameof(PurchaseDate));
        OnPropertyChanged(nameof(InvoiceNo));
        OnPropertyChanged(nameof(GstPercent));
        OnPropertyChanged(nameof(GstAmount));
        OnPropertyChanged(nameof(Freight));
        OnPropertyChanged(nameof(LoadingUnloading));
    }

    public void StartNewBill()
    {
        ResetForm();
        FormMode = EntryFormMode.New;
        RequestFocusPartyAction?.Invoke();
    }

    public void StartModify()
    {
        if (EditingPurchaseId == 0)
        {
            MessageBox.Show("Please select an existing bill from 'Load Bill' first to modify it.", "Modify Bill", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        FormMode = EntryFormMode.Edit;
        RequestFocusPartyAction?.Invoke();
    }

    public void Cancel()
    {
        if (_editingPurchaseId > 0 && SelectedBill != null)
        {
            LoadBill(SelectedBill);
        }
        else
        {
            ResetForm();
            FormMode = EntryFormMode.Browse;
        }
    }

    private void Save()
    {
        if (!IsFormActive) return;

        if (EditingPurchaseId > 0)
        {
            ExecuteSave(isNew: false);
        }
        else
        {
            ExecuteSave(isNew: true);
        }
    }

    private void ExecuteSave(bool isNew)
    {
        if (string.IsNullOrWhiteSpace(InvoiceNo))
        {
            MessageBox.Show("Please enter an Invoice / Bill Number.", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(SupplierName))
        {
            MessageBox.Show("Please enter Supplier Name.", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var validLines = Lines.Where(l => l.Item != null && l.Bags > 0).ToList();
        if (validLines.Count == 0)
        {
            MessageBox.Show("Please add at least one line item with Bags > 0.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var purchase = new Purchase
            {
                Id = isNew ? 0 : EditingPurchaseId,
                InvoiceNo = InvoiceNo.Trim(),
                SupplierName = SupplierName.Trim(),
                GodownId = SelectedGodown?.Id,
                GodownName = SelectedGodown?.Name,
                PurchaseDate = PurchaseDate,
                TotalBags = validLines.Sum(l => l.Bags),
                TotalWeightKg = validLines.Sum(l => l.TotalKg),
                SubTotal = SubTotal,
                GstPercent = GstPercent,
                GstAmount = GstAmount,
                Freight = Freight,
                LoadingUnloading = LoadingUnloading,
                GrandTotal = GrandTotal,
                TotalAmount = GrandTotal,
                Notes = Notes?.Trim(),
                CreatedAt = DateTime.Now
            };

            foreach (var l in validLines)
            {
                purchase.Items.Add(new PurchaseItem
                {
                    ItemId = l.Item!.Id,
                    ItemName = l.Item.ItemName,
                    GodownId = l.SelectedGodown?.Id ?? SelectedGodown?.Id,
                    GodownName = l.SelectedGodown?.Name ?? SelectedGodown?.Name,
                    PackSizeKg = l.PackSizeValue,
                    Bags = l.Bags,
                    TotalKg = l.TotalKg,
                    RatePerKg = l.RatePerKg,
                    Amount = l.Amount,
                    LandedCost = l.EffectiveRatePerKg > 0.0001 ? l.EffectiveRatePerKg : l.RatePerKg
                });
            }

            var id = _inventoryService.SavePurchase(purchase);
            LoadBills();
            LoadAccounts();

            string actionName = isNew ? "saved" : "updated";
            MessageBox.Show(
                $"Purchase #{purchase.InvoiceNo} {actionName} successfully!\nTotal Bags: {purchase.TotalBags}\nTotal Weight: {purchase.TotalWeightKg:F2} kg\nGrand Total: ₹ {purchase.GrandTotal:N2}",
                "Purchase Inward Saved",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            var updatedPurchase = _inventoryService.GetPurchaseById(id);
            if (updatedPurchase != null)
            {
                LoadBill(updatedPurchase);
            }
            else
            {
                ResetForm();
                FormMode = EntryFormMode.Browse;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving purchase: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Print()
    {
        if (Lines.Count == 0 || Lines.All(l => l.TotalKg <= 0.0001))
        {
            MessageBox.Show("There are no purchase lines to print. Please add items or load a bill first.", "Print Purchase", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var printWin = new PrintPurchaseWindow(this);
            printWin.Owner = Application.Current.MainWindow;
            printWin.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to display print preview: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenList()
    {
        RequestOpenHistoryAction?.Invoke();
    }
}
