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

public class SaleRowViewModel : ObservableObject
{
    private readonly InventoryService _inventoryService;
    private Item? _item;
    private Godown? _selectedGodown;
    private double? _packSizeKg;
    private int _bagsSold;
    private double _looseKgSold;
    private double _ratePerKg;
    private StockItem? _availableStock;
    private int _priorityHeadGroupId;
    private int _priorityBrandId;
    private double _thresholdKg;

    public SaleRowViewModel(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    public double ThresholdKg
    {
        get => _thresholdKg;
        set
        {
            if (SetProperty(ref _thresholdKg, value))
            {
                OnPropertyChanged(nameof(IsBelowInventoryThreshold));
                OnPropertyChanged(nameof(IsStockInsufficient));
                OnPropertyChanged(nameof(StockStatusText));
                OnPropertyChanged(nameof(MorphedBagPreviewText));
            }
        }
    }

    public bool IsBelowInventoryThreshold => ThresholdKg > 0.0001 && TotalKgSold > 0.0001 && TotalKgSold < ThresholdKg;

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
                    // Fetch all known pack sizes from stock for this item
                    var stockPacks = _inventoryService.GetAllStock()
                        .Where(s => s.ItemId == value.Id && s.FullBags > 0)
                        .Select(s => s.PackSizeKg)
                        .Distinct()
                        .OrderByDescending(x => x)
                        .ToList();

                    foreach (var s in stockPacks) AvailablePackSizes.Add(s);

                    if (AvailablePackSizes.Count == 0 && value.PackSizes != null)
                    {
                        foreach (var ps in value.PackSizes) AvailablePackSizes.Add(ps);
                    }

                    // Preserve inherited pack size if valid for this item, otherwise choose first available
                    if (_packSizeKg.HasValue && AvailablePackSizes.Contains(_packSizeKg.Value))
                    {
                        // Retain inherited pack size (e.g. 30kg)
                    }
                    else if (AvailablePackSizes.Count > 0)
                    {
                        _packSizeKg = AvailablePackSizes.First();
                    }
                    else
                    {
                        _packSizeKg = 25.0; // fallback default
                        AvailablePackSizes.Add(25.0);
                    }

                    OnPropertyChanged(nameof(PackSizeKg));
                    OnPropertyChanged(nameof(PackSizeText));
                    if (_ratePerKg <= 0.0001)
                    {
                        RatePerKg = value.DefaultRatePerKg;
                    }
                }
                RefreshAvailableStock();
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
                OnPropertyChanged(nameof(PackSizeText));
                RefreshAvailableStock();
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

    public int BagsSold
    {
        get => _bagsSold;
        set
        {
            if (SetProperty(ref _bagsSold, value))
            {
                Recalculate();
            }
        }
    }

    public double LooseKgSold
    {
        get => _looseKgSold;
        set
        {
            if (SetProperty(ref _looseKgSold, value))
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

    public StockItem? AvailableStock
    {
        get => _availableStock;
        private set => SetProperty(ref _availableStock, value);
    }

    public double TotalKgSold => (BagsSold * PackSizeValue) + LooseKgSold;
    public double Amount => TotalKgSold * RatePerKg;

    public string StockStatusText
    {
        get
        {
            if (IsBelowInventoryThreshold)
            {
                return $"📦 Below {ThresholdKg:0.##} kg threshold (No stock deduction)";
            }

            if (AvailableStock == null || AvailableStock.FullBags <= 0)
            {
                return $"Stock: 0 Bags of {PackSizeValue:0.##} kg (Out of Stock)";
            }

            return $"Available: {AvailableStock.FullBags} Bag(s) ({AvailableStock.TotalKg:F1} kg)";
        }
    }

    public string MorphedBagPreviewText
    {
        get
        {
            if (IsBelowInventoryThreshold)
            {
                return "No inventory deduction";
            }

            if (LooseKgSold > 0.0001 && PackSizeValue > LooseKgSold)
            {
                double rem = Math.Round(PackSizeValue - LooseKgSold, 2);
                return $"👉 Will create 1 Bag of {rem:0.##} kg";
            }
            return string.Empty;
        }
    }

    public bool IsStockInsufficient
    {
        get
        {
            if (LooseKgSold >= PackSizeValue && PackSizeValue > 0) return true;
            if (IsBelowInventoryThreshold) return false;
            if (AvailableStock == null) return TotalKgSold > 0.0001;
            int bagsNeeded = BagsSold + (LooseKgSold > 0.0001 ? 1 : 0);
            return bagsNeeded > AvailableStock.FullBags;
        }
    }

    public void RefreshAvailableStock()
    {
        if (Item != null && PackSizeValue > 0)
        {
            AvailableStock = _inventoryService.GetStock(Item.Id, PackSizeValue);
        }
        else
        {
            AvailableStock = null;
        }
        OnPropertyChanged(nameof(IsBelowInventoryThreshold));
        OnPropertyChanged(nameof(StockStatusText));
        OnPropertyChanged(nameof(MorphedBagPreviewText));
        OnPropertyChanged(nameof(IsStockInsufficient));
    }

    public void Recalculate()
    {
        OnPropertyChanged(nameof(PackSizeValue));
        OnPropertyChanged(nameof(TotalKgSold));
        OnPropertyChanged(nameof(Amount));
        OnPropertyChanged(nameof(IsBelowInventoryThreshold));
        OnPropertyChanged(nameof(StockStatusText));
        OnPropertyChanged(nameof(MorphedBagPreviewText));
        OnPropertyChanged(nameof(IsStockInsufficient));
    }
}

public class SalesViewModel : ObservableObject
{
    private readonly InventoryService _inventoryService;
    private EntryFormMode _formMode = EntryFormMode.Browse;
    private int _editingSaleId = 0;
    private string _invoiceNo = string.Empty;
    private string _customerName = string.Empty;
    private Godown? _selectedGodown;
    private DateTime _saleDate = DateTime.Today;
    private string? _notes;
    private Sale? _selectedBill;

    private double _gstPercent = 0.0;
    private double _gstAmount = 0.0;
    private double _freight = 0.0;
    private double _loadingUnloading = 0.0;
    private bool _isUpdatingGst = false;

    // Payment Settlement fields
    private string _paymentMode = "Credit"; // Credit, Cash, UPI, Split
    private Account? _selectedBankUpiAccount;
    private string? _utrRefNo;
    private double _cashReceived = 0.0;
    private double _upiReceived = 0.0;
    private double _creditAmount = 0.0;

    public SalesViewModel(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
        AllItems = new ObservableCollection<Item>();
        AllGodowns = new ObservableCollection<Godown>();
        AllAccounts = new ObservableCollection<Account>();
        BankUpiAccounts = new ObservableCollection<Account>();
        Lines = new ObservableCollection<SaleRowViewModel>();
        AllBills = new ObservableCollection<Sale>();

        AddLineCommand = new RelayCommand(AddLine);
        RemoveLineCommand = new RelayCommand(param => RemoveLine(param as SaleRowViewModel));
        SaveCommand = new RelayCommand(Save);
        AddNewCommand = new RelayCommand(StartNewBill);
        ModifyCommand = new RelayCommand(StartModify);
        CancelCommand = new RelayCommand(Cancel);
        PrintCommand = new RelayCommand(Print);
        ListCommand = new RelayCommand(OpenList);
        OpenSplitDialogCommand = new RelayCommand(OpenSplitDialog);

        LoadItems();
        LoadGodowns();
        LoadAccounts();
        LoadBankUpiAccounts();
        LoadBills();
        RefreshThreshold();
        ResetForm();
        FormMode = EntryFormMode.Browse;
    }

    private double _thresholdKg;
    public double ThresholdKg
    {
        get => _thresholdKg;
        set
        {
            if (SetProperty(ref _thresholdKg, value))
            {
                foreach (var line in Lines)
                {
                    line.ThresholdKg = value;
                }
            }
        }
    }

    public void RefreshThreshold()
    {
        ThresholdKg = _inventoryService.GetSalesDeductionThresholdKg();
    }

    public ObservableCollection<Item> AllItems { get; }
    public ObservableCollection<Account> BankUpiAccounts { get; }
    public ObservableCollection<Godown> AllGodowns { get; }
    public ObservableCollection<Account> AllAccounts { get; }
    public ObservableCollection<SaleRowViewModel> Lines { get; }
    public ObservableCollection<Sale> AllBills { get; }

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
    public bool CanModify => _formMode == EntryFormMode.Browse && _editingSaleId > 0;
    public bool CanSave => IsFormActive;
    public bool CanCancel => IsFormActive;

    public int EditingSaleId
    {
        get => _editingSaleId;
        set
        {
            if (SetProperty(ref _editingSaleId, value))
            {
                OnPropertyChanged(nameof(IsEditingExistingBill));
                OnPropertyChanged(nameof(CanModify));
                OnPropertyChanged(nameof(BillStatusText));
            }
        }
    }

    public bool IsEditingExistingBill => _editingSaleId > 0;

    public string BillStatusText
    {
        get
        {
            return _formMode switch
            {
                EntryFormMode.New => "✨ NEW BILL: Entry Mode",
                EntryFormMode.Edit => $"✏ MODIFICATION MODE: Editing Bill #{InvoiceNo}",
                _ => _editingSaleId > 0
                    ? $"🔒 VIEW MODE: Bill #{InvoiceNo} (Click Modify to edit)"
                    : "🔒 VIEW MODE: Press Ctrl+N to add new bill or load a bill above"
            };
        }
    }

    public Sale? SelectedBill
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

    public string CustomerName
    {
        get => _customerName;
        set => SetProperty(ref _customerName, value);
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

    public DateTime SaleDate
    {
        get => _saleDate;
        set => SetProperty(ref _saleDate, value);
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
            }
        }
    }

    public double PartyAmount => SubTotal + GstAmount;
    public double GrandTotal => SubTotal + GstAmount + Freight + LoadingUnloading;

    public int TotalBags => Lines.Sum(l => l.BagsSold);
    public double TotalLooseKg => Lines.Sum(l => l.LooseKgSold);
    public double TotalWeightKg => Lines.Sum(l => l.TotalKgSold);
    public double TotalAmount => GrandTotal > 0 ? GrandTotal : (SubTotal > 0 ? SubTotal : 0.0);

    public ICommand AddLineCommand { get; }
    public ICommand RemoveLineCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand AddNewCommand { get; }
    public ICommand ModifyCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand ListCommand { get; }
    public ICommand OpenSplitDialogCommand { get; }

    public Action? RequestOpenHistoryAction { get; set; }
    public Action? RequestFocusPartyAction { get; set; }

    public string PaymentMode
    {
        get => _paymentMode;
        set
        {
            if (SetProperty(ref _paymentMode, value))
            {
                OnPropertyChanged(nameof(IsCreditMode));
                OnPropertyChanged(nameof(IsCashMode));
                OnPropertyChanged(nameof(IsUpiMode));
                OnPropertyChanged(nameof(IsSplitMode));
                OnPropertyChanged(nameof(SplitSummaryText));
                OnPropertyChanged(nameof(PaymentModeDescription));
            }
        }
    }

    public bool IsCreditMode
    {
        get => _paymentMode == "Credit";
        set { if (value) SetPaymentMode("Credit"); }
    }

    public bool IsCashMode
    {
        get => _paymentMode == "Cash";
        set { if (value) SetPaymentMode("Cash"); }
    }

    public bool IsUpiMode
    {
        get => _paymentMode == "UPI";
        set { if (value) SetPaymentMode("UPI"); }
    }

    public bool IsSplitMode
    {
        get => _paymentMode == "Split";
        set { if (value) SetPaymentMode("Split"); }
    }

    public Account? SelectedBankUpiAccount
    {
        get => _selectedBankUpiAccount;
        set => SetProperty(ref _selectedBankUpiAccount, value);
    }

    public string? UtrRefNo
    {
        get => _utrRefNo;
        set => SetProperty(ref _utrRefNo, value);
    }

    public double CashReceived
    {
        get => _cashReceived;
        set
        {
            if (SetProperty(ref _cashReceived, value))
            {
                OnPropertyChanged(nameof(SplitSummaryText));
                OnPropertyChanged(nameof(PaymentModeDescription));
            }
        }
    }

    public double UpiReceived
    {
        get => _upiReceived;
        set
        {
            if (SetProperty(ref _upiReceived, value))
            {
                OnPropertyChanged(nameof(SplitSummaryText));
                OnPropertyChanged(nameof(PaymentModeDescription));
            }
        }
    }

    public double CreditAmount
    {
        get => _creditAmount;
        set
        {
            if (SetProperty(ref _creditAmount, value))
            {
                OnPropertyChanged(nameof(SplitSummaryText));
                OnPropertyChanged(nameof(PaymentModeDescription));
            }
        }
    }

    public string SplitSummaryText =>
        $"💵 ₹{CashReceived:N0} + 📱 ₹{UpiReceived:N0} + 💳 ₹{CreditAmount:N0}";

    public string PaymentModeDescription => _paymentMode switch
    {
        "Cash" => "✓ Full amount received in Cash Drawer (Customer balance cleared)",
        "UPI" => "✓ Online payment into selected UPI / Bank Account",
        "Split" => $"⚖ Split: 💵 ₹ {CashReceived:N2} Cash • 📱 ₹ {UpiReceived:N2} UPI • 💳 ₹ {CreditAmount:N2} Balance",
        _ => "ℹ Standard Credit Sale posted as receivable to Customer ledger"
    };

    public void SetPaymentMode(string mode)
    {
        _paymentMode = mode;
        if (mode == "Credit")
        {
            _cashReceived = 0.0;
            _upiReceived = 0.0;
            _creditAmount = GrandTotal;
        }
        else if (mode == "Cash")
        {
            _cashReceived = GrandTotal;
            _upiReceived = 0.0;
            _creditAmount = 0.0;
        }
        else if (mode == "UPI")
        {
            _cashReceived = 0.0;
            _upiReceived = GrandTotal;
            _creditAmount = 0.0;
            if (SelectedBankUpiAccount == null && BankUpiAccounts.Count > 0)
            {
                SelectedBankUpiAccount = BankUpiAccounts.First();
            }
        }
        else if (mode == "Split")
        {
            OpenSplitDialog();
        }

        OnPropertyChanged(nameof(PaymentMode));
        OnPropertyChanged(nameof(IsCreditMode));
        OnPropertyChanged(nameof(IsCashMode));
        OnPropertyChanged(nameof(IsUpiMode));
        OnPropertyChanged(nameof(IsSplitMode));
        OnPropertyChanged(nameof(CashReceived));
        OnPropertyChanged(nameof(UpiReceived));
        OnPropertyChanged(nameof(CreditAmount));
        OnPropertyChanged(nameof(SplitSummaryText));
        OnPropertyChanged(nameof(PaymentModeDescription));
    }

    public void OpenSplitDialog()
    {
        if (GrandTotal <= 0.0001)
        {
            MessageBox.Show("Please add sale items first to calculate the bill total before setting up split payment.", "Split Payment", MessageBoxButton.OK, MessageBoxImage.Information);
            _paymentMode = "Credit";
            OnPropertyChanged(nameof(PaymentMode));
            OnPropertyChanged(nameof(IsCreditMode));
            OnPropertyChanged(nameof(IsSplitMode));
            return;
        }

        var dlg = new Views.SplitPaymentWindow(
            GrandTotal,
            BankUpiAccounts,
            CashReceived > 0 ? CashReceived : 0,
            UpiReceived > 0 ? UpiReceived : 0,
            SelectedBankUpiAccount,
            UtrRefNo);

        dlg.Owner = Application.Current.MainWindow;
        if (dlg.ShowDialog() == true)
        {
            _paymentMode = "Split";
            _cashReceived = dlg.CashAmount;
            _upiReceived = dlg.UpiAmount;
            _creditAmount = dlg.RemainingCredit;
            _selectedBankUpiAccount = dlg.SelectedUpiAccount;
            _utrRefNo = dlg.UtrRefNo;

            OnPropertyChanged(nameof(PaymentMode));
            OnPropertyChanged(nameof(IsCreditMode));
            OnPropertyChanged(nameof(IsCashMode));
            OnPropertyChanged(nameof(IsUpiMode));
            OnPropertyChanged(nameof(IsSplitMode));
            OnPropertyChanged(nameof(CashReceived));
            OnPropertyChanged(nameof(UpiReceived));
            OnPropertyChanged(nameof(CreditAmount));
            OnPropertyChanged(nameof(SelectedBankUpiAccount));
            OnPropertyChanged(nameof(UtrRefNo));
            OnPropertyChanged(nameof(SplitSummaryText));
            OnPropertyChanged(nameof(PaymentModeDescription));
        }
        else
        {
            if (CashReceived <= 0.0001 && UpiReceived <= 0.0001)
            {
                SetPaymentMode("Credit");
            }
        }
    }

    public void LoadBankUpiAccounts()
    {
        BankUpiAccounts.Clear();
        var all = _inventoryService.GetAllAccounts();
        var banks = all.Where(a => a.AccountType.Equals("Bank", StringComparison.OrdinalIgnoreCase) ||
                                  a.AccountType.Equals("UPI", StringComparison.OrdinalIgnoreCase)).ToList();

        if (banks.Count == 0)
        {
            var fallback = all.FirstOrDefault(a => a.AccountName.Contains("Bank", StringComparison.OrdinalIgnoreCase) || a.AccountName.Contains("UPI", StringComparison.OrdinalIgnoreCase));
            if (fallback != null)
            {
                banks.Add(fallback);
            }
            else
            {
                var created = _inventoryService.SaveAccount(new Account
                {
                    AccountName = "Primary UPI / Bank Account",
                    AccountType = "Bank",
                    Description = "Primary UPI QR & Online Collections"
                });
                var createdAcc = _inventoryService.GetAccountById(created);
                if (createdAcc != null) banks.Add(createdAcc);
            }
        }

        foreach (var b in banks) BankUpiAccounts.Add(b);

        if (SelectedBankUpiAccount == null || !BankUpiAccounts.Any(a => a.Id == SelectedBankUpiAccount.Id))
        {
            SelectedBankUpiAccount = BankUpiAccounts.FirstOrDefault();
        }
    }

    public void LoadItems()
    {
        AllItems.Clear();
        foreach (var item in _inventoryService.GetAllItems()) AllItems.Add(item);
    }

    public void LoadGodowns()
    {
        AllGodowns.Clear();
        foreach (var g in _inventoryService.GetAllGodowns()) AllGodowns.Add(g);
    }

    public void LoadAccounts()
    {
        AllAccounts.Clear();
        foreach (var a in _inventoryService.GetAllAccounts()) AllAccounts.Add(a);
        LoadBankUpiAccounts();
    }

    public void LoadBills()
    {
        AllBills.Clear();
        var sales = _inventoryService.GetAllSales();
        foreach (var s in sales) AllBills.Add(s);
    }

    public void LoadBill(Sale bill)
    {
        var fullSale = _inventoryService.GetSaleById(bill.Id) ?? bill;
        EditingSaleId = fullSale.Id;
        InvoiceNo = fullSale.InvoiceNo;
        CustomerName = fullSale.CustomerName;
        SelectedGodown = AllGodowns.FirstOrDefault(g => g.Id == fullSale.GodownId);
        SaleDate = fullSale.SaleDate;
        Notes = fullSale.Notes;

        _isUpdatingGst = true;
        _gstPercent = fullSale.GstPercent;
        _gstAmount = fullSale.GstAmount;
        _freight = fullSale.Freight;
        _loadingUnloading = fullSale.LoadingUnloading;
        _isUpdatingGst = false;

        _paymentMode = string.IsNullOrWhiteSpace(fullSale.PaymentMode) ? "Credit" : fullSale.PaymentMode;
        _selectedBankUpiAccount = BankUpiAccounts.FirstOrDefault(b => b.Id == fullSale.BankAccountId) ??
                                 BankUpiAccounts.FirstOrDefault(b => b.AccountName == fullSale.BankAccountName) ??
                                 BankUpiAccounts.FirstOrDefault();
        _utrRefNo = fullSale.UtrRefNo;
        _cashReceived = fullSale.CashReceived;
        _upiReceived = fullSale.UpiReceived;
        _creditAmount = fullSale.CreditAmount > 0.0001 ? fullSale.CreditAmount :
                        (_paymentMode == "Cash" || _paymentMode == "UPI" ? 0.0 : fullSale.GrandTotal);

        OnPropertyChanged(nameof(CustomerName));
        OnPropertyChanged(nameof(SelectedGodown));
        OnPropertyChanged(nameof(SaleDate));
        OnPropertyChanged(nameof(Notes));
        OnPropertyChanged(nameof(GstPercent));
        OnPropertyChanged(nameof(GstAmount));
        OnPropertyChanged(nameof(Freight));
        OnPropertyChanged(nameof(LoadingUnloading));
        OnPropertyChanged(nameof(PaymentMode));
        OnPropertyChanged(nameof(IsCreditMode));
        OnPropertyChanged(nameof(IsCashMode));
        OnPropertyChanged(nameof(IsUpiMode));
        OnPropertyChanged(nameof(IsSplitMode));
        OnPropertyChanged(nameof(SelectedBankUpiAccount));
        OnPropertyChanged(nameof(UtrRefNo));
        OnPropertyChanged(nameof(CashReceived));
        OnPropertyChanged(nameof(UpiReceived));
        OnPropertyChanged(nameof(CreditAmount));
        OnPropertyChanged(nameof(SplitSummaryText));
        OnPropertyChanged(nameof(PaymentModeDescription));

        RefreshThreshold();
        Lines.Clear();
        foreach (var item in fullSale.Items)
        {
            var matchedItem = AllItems.FirstOrDefault(i => i.Id == item.ItemId);
            var itemGodown = AllGodowns.FirstOrDefault(g => g.Id == item.GodownId) ?? SelectedGodown;

            var row = new SaleRowViewModel(_inventoryService)
            {
                ThresholdKg = ThresholdKg,
                Item = matchedItem,
                SelectedGodown = itemGodown,
                PackSizeKg = item.PackSizeKg,
                BagsSold = item.BagsSold,
                LooseKgSold = item.LooseKgSold,
                RatePerKg = item.RatePerKg
            };
            row.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SaleRowViewModel.Item))
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
        var row = new SaleRowViewModel(_inventoryService)
        {
            ThresholdKg = ThresholdKg,
            SelectedGodown = prevRow?.SelectedGodown ?? SelectedGodown,
            PackSizeKg = prevRow?.PackSizeKg,
            RatePerKg = prevRow?.RatePerKg ?? 0.0
        };
        row.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SaleRowViewModel.Item))
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
        if (last == null || last.Item != null || last.BagsSold > 0 || last.LooseKgSold > 0)
        {
            AddLine();
        }
    }

    public void RemoveLine(SaleRowViewModel? row)
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

        // Adjust payment breakdown according to active mode
        if (_paymentMode == "Credit")
        {
            _cashReceived = 0.0;
            _upiReceived = 0.0;
            _creditAmount = GrandTotal;
        }
        else if (_paymentMode == "Cash")
        {
            _cashReceived = GrandTotal;
            _upiReceived = 0.0;
            _creditAmount = 0.0;
        }
        else if (_paymentMode == "UPI")
        {
            _cashReceived = 0.0;
            _upiReceived = GrandTotal;
            _creditAmount = 0.0;
        }
        else if (_paymentMode == "Split")
        {
            _creditAmount = Math.Max(0.0, Math.Round(GrandTotal - _cashReceived - _upiReceived, 2));
        }

        OnPropertyChanged(nameof(TotalBags));
        OnPropertyChanged(nameof(TotalLooseKg));
        OnPropertyChanged(nameof(TotalWeightKg));
        OnPropertyChanged(nameof(SubTotal));
        OnPropertyChanged(nameof(GstPercent));
        OnPropertyChanged(nameof(GstAmount));
        OnPropertyChanged(nameof(Freight));
        OnPropertyChanged(nameof(LoadingUnloading));
        OnPropertyChanged(nameof(PartyAmount));
        OnPropertyChanged(nameof(GrandTotal));
        OnPropertyChanged(nameof(TotalAmount));
        OnPropertyChanged(nameof(CashReceived));
        OnPropertyChanged(nameof(UpiReceived));
        OnPropertyChanged(nameof(CreditAmount));
        OnPropertyChanged(nameof(SplitSummaryText));
        OnPropertyChanged(nameof(PaymentModeDescription));
    }

    public void ResetForm()
    {
        _editingSaleId = 0;
        _selectedBill = null;
        OnPropertyChanged(nameof(SelectedBill));
        OnPropertyChanged(nameof(EditingSaleId));
        OnPropertyChanged(nameof(IsEditingExistingBill));
        OnPropertyChanged(nameof(CanModify));
        OnPropertyChanged(nameof(BillStatusText));

        LoadGodowns();
        LoadAccounts();
        LoadBills();
        int nextSeq = AllBills.Count + 1;
        InvoiceNo = $"SAL-{DateTime.Now:yyyyMMdd}-{nextSeq:D4}";
        CustomerName = string.Empty;
        SelectedGodown = AllGodowns.FirstOrDefault(g => g.IsActive) ?? AllGodowns.FirstOrDefault();
        SaleDate = DateTime.Today;
        Notes = string.Empty;
        _isUpdatingGst = true;
        _gstPercent = 0.0;
        _gstAmount = 0.0;
        _freight = 0.0;
        _loadingUnloading = 0.0;
        _isUpdatingGst = false;

        _paymentMode = "Credit";
        _cashReceived = 0.0;
        _upiReceived = 0.0;
        _creditAmount = 0.0;
        _utrRefNo = string.Empty;
        _selectedBankUpiAccount = BankUpiAccounts.FirstOrDefault();
        OnPropertyChanged(nameof(PaymentMode));
        OnPropertyChanged(nameof(IsCreditMode));
        OnPropertyChanged(nameof(IsCashMode));
        OnPropertyChanged(nameof(IsUpiMode));
        OnPropertyChanged(nameof(IsSplitMode));
        OnPropertyChanged(nameof(CashReceived));
        OnPropertyChanged(nameof(UpiReceived));
        OnPropertyChanged(nameof(CreditAmount));
        OnPropertyChanged(nameof(SelectedBankUpiAccount));
        OnPropertyChanged(nameof(UtrRefNo));
        OnPropertyChanged(nameof(SplitSummaryText));
        OnPropertyChanged(nameof(PaymentModeDescription));

        Lines.Clear();
        LoadItems();
        RefreshThreshold();

        // Add 1 fresh, empty line ready for user input
        var row = new SaleRowViewModel(_inventoryService)
        {
            ThresholdKg = ThresholdKg,
            SelectedGodown = SelectedGodown
        };
        row.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SaleRowViewModel.Item))
            {
                UpdateRowPriorities();
            }
            RecalculateTotals();
        };
        Lines.Add(row);
        UpdateRowPriorities();

        RecalculateTotals();
        OnPropertyChanged(nameof(CustomerName));
        OnPropertyChanged(nameof(SelectedGodown));
        OnPropertyChanged(nameof(Notes));
        OnPropertyChanged(nameof(SaleDate));
        OnPropertyChanged(nameof(InvoiceNo));
    }

    public void StartNewBill()
    {
        ResetForm();
        FormMode = EntryFormMode.New;
        RequestFocusPartyAction?.Invoke();
    }

    public void StartModify()
    {
        if (EditingSaleId == 0)
        {
            MessageBox.Show("Please select an existing bill from 'Load Bill' first to modify it.", "Modify Bill", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        FormMode = EntryFormMode.Edit;
        RequestFocusPartyAction?.Invoke();
    }

    public void Cancel()
    {
        if (_editingSaleId > 0 && SelectedBill != null)
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

        if (EditingSaleId > 0)
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
            MessageBox.Show("Please enter an Invoice Number.", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(CustomerName))
        {
            MessageBox.Show("Please enter Customer Name.", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var validLines = Lines.Where(l => l.Item != null && l.TotalKgSold > 0.0001).ToList();
        if (validLines.Count == 0)
        {
            MessageBox.Show("Please add at least one line item with quantity / weight > 0.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_paymentMode == "UPI" && SelectedBankUpiAccount == null)
        {
            MessageBox.Show("Please select a Bank / UPI account to receive the payment.", "Account Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_paymentMode == "Split" && _upiReceived > 0.0001 && SelectedBankUpiAccount == null)
        {
            MessageBox.Show("Please select a Bank / UPI account for the UPI portion of the payment.", "Account Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            // Check stock sufficiency
            if (isNew)
            {
                foreach (var line in validLines)
                {
                    line.RefreshAvailableStock();
                    if (line.IsStockInsufficient)
                    {
                        MessageBox.Show(
                            $"Cannot proceed: Insufficient stock for {line.Item!.ItemName} ({line.PackSizeValue}kg pack).\nAvailable: {line.AvailableStock?.TotalKg ?? 0:F2} kg, Attempting to sell: {line.TotalKgSold:F2} kg.",
                            "Insufficient Stock",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }
                }
            }

            var sale = new Sale
            {
                Id = isNew ? 0 : EditingSaleId,
                InvoiceNo = InvoiceNo.Trim(),
                CustomerName = CustomerName.Trim(),
                GodownId = SelectedGodown?.Id,
                GodownName = SelectedGodown?.Name,
                SaleDate = SaleDate,
                TotalBags = validLines.Sum(l => l.BagsSold),
                TotalLooseKg = validLines.Sum(l => l.LooseKgSold),
                TotalWeightKg = validLines.Sum(l => l.TotalKgSold),
                SubTotal = SubTotal,
                GstPercent = GstPercent,
                GstAmount = GstAmount,
                Freight = Freight,
                LoadingUnloading = LoadingUnloading,
                GrandTotal = GrandTotal,
                TotalAmount = GrandTotal,
                PaymentMode = _paymentMode,
                BankAccountId = (_paymentMode == "UPI" || (_paymentMode == "Split" && _upiReceived > 0)) ? SelectedBankUpiAccount?.Id : null,
                BankAccountName = (_paymentMode == "UPI" || (_paymentMode == "Split" && _upiReceived > 0)) ? SelectedBankUpiAccount?.AccountName : null,
                UtrRefNo = (_paymentMode == "UPI" || (_paymentMode == "Split" && _upiReceived > 0)) ? _utrRefNo?.Trim() : null,
                CashReceived = _paymentMode == "Cash" ? GrandTotal : (_paymentMode == "Split" ? _cashReceived : 0.0),
                UpiReceived = _paymentMode == "UPI" ? GrandTotal : (_paymentMode == "Split" ? _upiReceived : 0.0),
                CreditAmount = _paymentMode == "Credit" ? GrandTotal : (_paymentMode == "Split" ? _creditAmount : 0.0),
                Notes = Notes?.Trim(),
                CreatedAt = DateTime.Now
            };

            foreach (var l in validLines)
            {
                sale.Items.Add(new SaleItem
                {
                    ItemId = l.Item!.Id,
                    ItemName = l.Item.ItemName,
                    GodownId = l.SelectedGodown?.Id ?? SelectedGodown?.Id,
                    GodownName = l.SelectedGodown?.Name ?? SelectedGodown?.Name,
                    PackSizeKg = l.PackSizeValue,
                    BagsSold = l.BagsSold,
                    LooseKgSold = l.LooseKgSold,
                    TotalKgSold = l.TotalKgSold,
                    RatePerKg = l.RatePerKg,
                    Amount = l.Amount
                });
            }

            var id = _inventoryService.SaveSale(sale);
            LoadBills();
            LoadAccounts();

            // Construct summary
            var openedLines = sale.Items.Where(i => i.BagsOpened > 0).ToList();
            string openedSummary = openedLines.Count > 0
                ? "\n\nAuto-Cut Bag Operations:\n" + string.Join("\n", openedLines.Select(o => $"• {o.ItemName} ({o.PackSizeKg}kg): Opened {o.BagsOpened} full bag(s) for loose sale."))
                : "";

            var skippedLines = sale.Items.Where(i => i.SkipStockDeduction).ToList();
            string thresholdNotice = skippedLines.Count > 0
                ? $"\n\nℹ Inventory Threshold Notice:\n• {skippedLines.Count} line item(s) below {_thresholdKg:0.##} kg were NOT subtracted from warehouse inventory."
                : "";

            string paymentSummary = _paymentMode switch
            {
                "Cash" => $"• Payment: ₹ {sale.GrandTotal:N2} (CASH received in Drawer)",
                "UPI" => $"• Payment: ₹ {sale.GrandTotal:N2} (UPI into {sale.BankAccountName})" + (string.IsNullOrWhiteSpace(sale.UtrRefNo) ? "" : $" [Ref: {sale.UtrRefNo}]"),
                "Split" => $"• Payment: Split (💵 Cash: ₹ {sale.CashReceived:N2}, 📱 UPI: ₹ {sale.UpiReceived:N2}, 💳 Credit: ₹ {sale.CreditAmount:N2})",
                _ => $"• Payment: ₹ {sale.GrandTotal:N2} (CREDIT - added to Customer Ledger)"
            };

            string actionName = isNew ? "saved" : "updated";
            MessageBox.Show(
                $"Invoice #{sale.InvoiceNo} {actionName} successfully!\n\n" +
                $"• Items Subtotal: ₹ {sale.SubTotal:N2}\n" +
                $"• GST ({sale.GstPercent}%): ₹ {sale.GstAmount:N2}\n" +
                $"• Freight: ₹ {sale.Freight:N2}\n" +
                $"• Loading/Unloading: ₹ {sale.LoadingUnloading:N2}\n" +
                $"• Grand Total: ₹ {sale.GrandTotal:N2}\n" +
                $"{paymentSummary}\n" +
                $"• Customer Balance Impact: ₹ {sale.CreditAmount:N2}{openedSummary}{thresholdNotice}",
                "Invoice Saved",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            var updatedSale = _inventoryService.GetSaleById(id);
            if (updatedSale != null)
            {
                LoadBill(updatedSale);
            }
            else
            {
                ResetForm();
                FormMode = EntryFormMode.Browse;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving invoice: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Print()
    {
        if (Lines.Count == 0 || Lines.All(l => l.TotalKgSold <= 0.0001))
        {
            MessageBox.Show("There are no bill lines to print. Please add items or load a bill first.", "Print Invoice", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var printWin = new PrintInvoiceWindow(this);
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
