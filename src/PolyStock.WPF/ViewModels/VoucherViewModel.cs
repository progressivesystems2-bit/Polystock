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

public class VoucherViewModel : ObservableObject
{
    private readonly InventoryService _inventoryService;

    private int _editingVoucherId = 0;
    private string _voucherType = "RECEIPT"; // RECEIPT, PAYMENT, JOURNAL
    private string _voucherNo = string.Empty;
    private DateTime _voucherDate = DateTime.Today;

    // Primary Account (Cash / Bank Account in Tally Single-Entry mode)
    private Account? _selectedCashBankAccount;

    // Direct Dr / Cr accounts for Journal
    private Account? _selectedDrAccount;
    private Account? _selectedCrAccount;

    private double _totalAmount = 0.0;
    private string _paymentMode = "Cash";
    private string? _referenceNo;
    private DateTime? _referenceDate;
    private string? _narration;

    private FinancialVoucher? _selectedVoucher;
    private string _voucherTypeFilter = "All";
    private string _searchPartyText = string.Empty;

    public VoucherViewModel(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;

        RecentVouchers = new ObservableCollection<FinancialVoucher>();
        FilteredVouchers = new ObservableCollection<FinancialVoucher>();

        CashBankAccounts = new ObservableCollection<Account>();
        PartyAccounts = new ObservableCollection<Account>();
        AllAccounts = new ObservableCollection<Account>();
        LineItems = new ObservableCollection<VoucherLineItemViewModel>();

        PaymentModes = new ObservableCollection<string>
        {
            "Cash",
            "Bank / Online / UPI",
            "Cheque",
            "NEFT / RTGS",
            "Adjustment / Transfer"
        };

        VoucherTypeFilters = new ObservableCollection<string>
        {
            "All",
            "Receipts Only (F6)",
            "Payments Only (F5)",
            "Journal Only (F7)"
        };

        RecentPartyInvoices = new ObservableCollection<string>();

        // Commands
        SaveCommand = new RelayCommand(Save);
        NewCommand = new RelayCommand(ResetForm);
        DeleteCommand = new RelayCommand(Delete, () => EditingVoucherId > 0);
        PrintCommand = new RelayCommand(Print);

        AddLineItemCommand = new RelayCommand(AddLineItem);

        SetReceiptModeCommand = new RelayCommand(() => SetMode("RECEIPT"));
        SetPaymentModeCommand = new RelayCommand(() => SetMode("PAYMENT"));
        SetJournalModeCommand = new RelayCommand(() => SetMode("JOURNAL"));

        LoadData();
        ResetForm();
    }

    public ObservableCollection<FinancialVoucher> RecentVouchers { get; }
    public ObservableCollection<FinancialVoucher> FilteredVouchers { get; }
    public ObservableCollection<Account> CashBankAccounts { get; }
    public ObservableCollection<Account> PartyAccounts { get; }
    public ObservableCollection<Account> AllAccounts { get; }
    public ObservableCollection<VoucherLineItemViewModel> LineItems { get; }
    public ObservableCollection<string> PaymentModes { get; }
    public ObservableCollection<string> VoucherTypeFilters { get; }
    public ObservableCollection<string> RecentPartyInvoices { get; }

    public int EditingVoucherId
    {
        get => _editingVoucherId;
        set
        {
            if (SetProperty(ref _editingVoucherId, value))
            {
                OnPropertyChanged(nameof(IsEditing));
                OnPropertyChanged(nameof(FormHeaderTitle));
            }
        }
    }

    public bool IsEditing => _editingVoucherId > 0;

    public string VoucherType
    {
        get => _voucherType;
        set
        {
            if (SetProperty(ref _voucherType, value))
            {
                OnPropertyChanged(nameof(IsReceiptMode));
                OnPropertyChanged(nameof(IsPaymentMode));
                OnPropertyChanged(nameof(IsJournalMode));
                OnPropertyChanged(nameof(IsSingleEntryMode));
                OnPropertyChanged(nameof(FormHeaderTitle));
                OnPropertyChanged(nameof(PrimaryAccountLabel));
                OnPropertyChanged(nameof(ParticularsHeaderLabel));
                UpdateBalances();
            }
        }
    }

    public bool IsReceiptMode => _voucherType.Equals("RECEIPT", StringComparison.OrdinalIgnoreCase);
    public bool IsPaymentMode => _voucherType.Equals("PAYMENT", StringComparison.OrdinalIgnoreCase);
    public bool IsJournalMode => _voucherType.Equals("JOURNAL", StringComparison.OrdinalIgnoreCase);
    public bool IsSingleEntryMode => !IsJournalMode;

    public string FormHeaderTitle => IsEditing
        ? $"✏ EDIT {VoucherType} VOUCHER #{VoucherNo}"
        : IsReceiptMode
            ? "🟢 NEW RECEIPT VOUCHER (F6)"
            : IsPaymentMode
                ? "🔴 NEW PAYMENT VOUCHER (F5)"
                : "🔵 NEW JOURNAL VOUCHER (F7)";

    public string PrimaryAccountLabel => IsReceiptMode
        ? "Account (Cash / Bank Deposit) *"
        : IsPaymentMode
            ? "Account (Cash / Bank Payment) *"
            : "Receiving (+) Account (Debit) *";

    public string ParticularsHeaderLabel => IsReceiptMode
        ? "Received From Particulars (Customers / Incomes)"
        : IsPaymentMode
            ? "Paid To Particulars (Vendors / Expenses)"
            : "Giving (-) Account Particulars (Credit)";

    // Legacy label aliases for XAML compatibility
    public string CashBankLabel => PrimaryAccountLabel;
    public string PartyAccountLabel => ParticularsHeaderLabel;

    public string VoucherNo
    {
        get => _voucherNo;
        set => SetProperty(ref _voucherNo, value);
    }

    public DateTime VoucherDate
    {
        get => _voucherDate;
        set => SetProperty(ref _voucherDate, value);
    }

    public Account? SelectedCashBankAccount
    {
        get => _selectedCashBankAccount;
        set
        {
            if (SetProperty(ref _selectedCashBankAccount, value))
            {
                UpdateBalances();
            }
        }
    }

    // For Journal (Direct Dr & Cr accounts)
    public Account? SelectedDrAccount
    {
        get => _selectedDrAccount;
        set
        {
            if (SetProperty(ref _selectedDrAccount, value))
            {
                UpdateBalances();
            }
        }
    }

    public Account? SelectedCrAccount
    {
        get => _selectedCrAccount;
        set
        {
            if (SetProperty(ref _selectedCrAccount, value))
            {
                UpdateBalances();
            }
        }
    }

    public double TotalAmount
    {
        get => _totalAmount;
        set
        {
            if (SetProperty(ref _totalAmount, value))
            {
                OnPropertyChanged(nameof(Amount));
                OnPropertyChanged(nameof(AmountText));
                OnPropertyChanged(nameof(FormattedTotalAmount));
                UpdateBalances();
            }
        }
    }

    // Backward compatibility property for Amount
    public double Amount
    {
        get => _totalAmount;
        set => TotalAmount = value;
    }

    public string FormattedTotalAmount => $"₹ {TotalAmount:N2}";

    public string AmountText
    {
        get => _totalAmount > 0 ? _totalAmount.ToString("0.##") : string.Empty;
        set
        {
            if (double.TryParse(value, out var d) && d >= 0)
            {
                TotalAmount = d;
            }
            else if (string.IsNullOrWhiteSpace(value))
            {
                TotalAmount = 0.0;
            }
        }
    }

    public string PaymentMode
    {
        get => _paymentMode;
        set => SetProperty(ref _paymentMode, value);
    }

    public string? ReferenceNo
    {
        get => _referenceNo;
        set => SetProperty(ref _referenceNo, value);
    }

    public DateTime? ReferenceDate
    {
        get => _referenceDate;
        set => SetProperty(ref _referenceDate, value);
    }

    public string? Narration
    {
        get => _narration;
        set => SetProperty(ref _narration, value);
    }

    // Real-Time Balance Projections
    public string PrimaryCurrentBalanceText { get; private set; } = "₹ 0.00";
    public string PrimaryProjectedBalanceText { get; private set; } = "₹ 0.00";

    // Legacy aliases for views
    public string CashBankCurrentBalanceText => PrimaryCurrentBalanceText;
    public string CashBankProjectedBalanceText => PrimaryProjectedBalanceText;
    public string PartyCurrentBalanceText { get; private set; } = "₹ 0.00";
    public string PartyProjectedBalanceText { get; private set; } = "₹ 0.00";

    public FinancialVoucher? SelectedVoucher
    {
        get => _selectedVoucher;
        set
        {
            if (SetProperty(ref _selectedVoucher, value))
            {
                if (value != null)
                {
                    LoadVoucher(value);
                }
            }
        }
    }

    public string VoucherTypeFilter
    {
        get => _voucherTypeFilter;
        set
        {
            if (SetProperty(ref _voucherTypeFilter, value))
            {
                ApplyFilter();
            }
        }
    }

    public string SearchPartyText
    {
        get => _searchPartyText;
        set
        {
            if (SetProperty(ref _searchPartyText, value))
            {
                ApplyFilter();
            }
        }
    }

    public ICommand SaveCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand AddLineItemCommand { get; }
    public ICommand SetReceiptModeCommand { get; }
    public ICommand SetPaymentModeCommand { get; }
    public ICommand SetJournalModeCommand { get; }

    public void AddLineItem()
    {
        var item = new VoucherLineItemViewModel(OnLineItemChanged, RemoveLineItem)
        {
            LineNumber = LineItems.Count + 1,
            SelectedAccount = null,
            Amount = 0.0
        };

        LineItems.Add(item);
        RecalculateTotals();
    }

    public void EnsureTrailingEmptyRow()
    {
        var last = LineItems.LastOrDefault();
        if (last == null || last.HasData)
        {
            var item = new VoucherLineItemViewModel(OnLineItemChanged, RemoveLineItem)
            {
                LineNumber = LineItems.Count + 1,
                SelectedAccount = null,
                Amount = 0.0
            };
            LineItems.Add(item);
        }
    }

    public void RemoveLineItem(VoucherLineItemViewModel item)
    {
        LineItems.Remove(item);
        RenumberLineItems();
        EnsureTrailingEmptyRow();
        RecalculateTotals();
    }

    private void RenumberLineItems()
    {
        for (int i = 0; i < LineItems.Count; i++)
        {
            LineItems[i].LineNumber = i + 1;
        }
    }

    private void OnLineItemChanged()
    {
        EnsureTrailingEmptyRow();
        RecalculateTotals();
        LoadPartyInvoices();
    }

    public void RecalculateTotals()
    {
        double sum = LineItems.Sum(i => i.Amount);
        _totalAmount = sum;
        OnPropertyChanged(nameof(TotalAmount));
        OnPropertyChanged(nameof(Amount));
        OnPropertyChanged(nameof(AmountText));
        OnPropertyChanged(nameof(FormattedTotalAmount));
        UpdateBalances();
    }

    public void SetMode(string mode)
    {
        VoucherType = mode.ToUpperInvariant();
        if (!IsEditing)
        {
            VoucherNo = _inventoryService.GetNextVoucherNo(VoucherType);
        }
        UpdateBalances();
    }

    public void LoadData()
    {
        LoadAccounts();
        LoadVouchers();
    }

    public void LoadAccounts()
    {
        var accounts = _inventoryService.GetAllAccounts();

        AllAccounts.Clear();
        CashBankAccounts.Clear();
        PartyAccounts.Clear();

        foreach (var acc in accounts)
        {
            AllAccounts.Add(acc);

            if (acc.AccountType.Equals("Cash", StringComparison.OrdinalIgnoreCase) ||
                acc.AccountType.Equals("Bank", StringComparison.OrdinalIgnoreCase))
            {
                CashBankAccounts.Add(acc);
            }
            else
            {
                PartyAccounts.Add(acc);
            }
        }

        if (CashBankAccounts.Count == 0)
        {
            foreach (var a in AllAccounts) CashBankAccounts.Add(a);
        }
    }

    public void LoadVouchers()
    {
        RecentVouchers.Clear();
        var list = _inventoryService.GetAllVouchers();
        foreach (var v in list)
        {
            RecentVouchers.Add(v);
        }

        ApplyFilter();
    }

    public void ApplyFilter()
    {
        FilteredVouchers.Clear();
        var query = RecentVouchers.AsEnumerable();

        if (VoucherTypeFilter.Contains("Receipt", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(v => v.VoucherType.Equals("RECEIPT", StringComparison.OrdinalIgnoreCase));
        }
        else if (VoucherTypeFilter.Contains("Payment", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(v => v.VoucherType.Equals("PAYMENT", StringComparison.OrdinalIgnoreCase));
        }
        else if (VoucherTypeFilter.Contains("Journal", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(v => v.VoucherType.Equals("JOURNAL", StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SearchPartyText))
        {
            var text = SearchPartyText.Trim();
            query = query.Where(v =>
                v.PrimaryAccountName.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                v.DebitAccountName.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                v.CreditAccountName.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                v.VoucherNo.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                (v.Items != null && v.Items.Any(i => i.AccountName.Contains(text, StringComparison.OrdinalIgnoreCase))) ||
                (!string.IsNullOrWhiteSpace(v.ReferenceNo) && v.ReferenceNo.Contains(text, StringComparison.OrdinalIgnoreCase)));
        }

        foreach (var v in query)
        {
            FilteredVouchers.Add(v);
        }
    }

    private void UpdateBalances()
    {
        if (IsJournalMode)
        {
            if (SelectedDrAccount != null)
            {
                PrimaryCurrentBalanceText = FormatBalance(SelectedDrAccount.CurrentBalance);
                double projectedDr = SelectedDrAccount.CurrentBalance + TotalAmount;
                PrimaryProjectedBalanceText = FormatBalance(projectedDr);
            }
            else
            {
                PrimaryCurrentBalanceText = "₹ 0.00";
                PrimaryProjectedBalanceText = "₹ 0.00";
            }

            if (SelectedCrAccount != null)
            {
                PartyCurrentBalanceText = FormatBalance(SelectedCrAccount.CurrentBalance);
                double projectedCr = SelectedCrAccount.CurrentBalance - TotalAmount;
                PartyProjectedBalanceText = FormatBalance(projectedCr);
            }
            else
            {
                PartyCurrentBalanceText = "₹ 0.00";
                PartyProjectedBalanceText = "₹ 0.00";
            }
        }
        else
        {
            if (SelectedCashBankAccount != null)
            {
                PrimaryCurrentBalanceText = FormatBalance(SelectedCashBankAccount.CurrentBalance);
                double projected = IsReceiptMode
                    ? SelectedCashBankAccount.CurrentBalance + TotalAmount
                    : SelectedCashBankAccount.CurrentBalance - TotalAmount;

                PrimaryProjectedBalanceText = FormatBalance(projected);
            }
            else
            {
                PrimaryCurrentBalanceText = "₹ 0.00";
                PrimaryProjectedBalanceText = "₹ 0.00";
            }

            var firstLine = LineItems.FirstOrDefault();
            if (firstLine?.SelectedAccount != null)
            {
                PartyCurrentBalanceText = FormatBalance(firstLine.SelectedAccount.CurrentBalance);
                double projected = IsReceiptMode
                    ? firstLine.SelectedAccount.CurrentBalance - firstLine.Amount
                    : firstLine.SelectedAccount.CurrentBalance + firstLine.Amount;
                PartyProjectedBalanceText = FormatBalance(projected);
            }
            else
            {
                PartyCurrentBalanceText = "₹ 0.00";
                PartyProjectedBalanceText = "₹ 0.00";
            }
        }

        OnPropertyChanged(nameof(PrimaryCurrentBalanceText));
        OnPropertyChanged(nameof(PrimaryProjectedBalanceText));
        OnPropertyChanged(nameof(CashBankCurrentBalanceText));
        OnPropertyChanged(nameof(CashBankProjectedBalanceText));
        OnPropertyChanged(nameof(PartyCurrentBalanceText));
        OnPropertyChanged(nameof(PartyProjectedBalanceText));
    }

    private void LoadPartyInvoices()
    {
        RecentPartyInvoices.Clear();
        var firstItem = LineItems.FirstOrDefault(i => i.SelectedAccount != null);
        if (firstItem?.SelectedAccount == null) return;

        var accName = firstItem.SelectedAccount.AccountName;

        if (IsReceiptMode)
        {
            var sales = _inventoryService.GetAllSales()
                .Where(s => s.CustomerName.Equals(accName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(s => s.SaleDate)
                .Take(5);

            foreach (var s in sales)
            {
                RecentPartyInvoices.Add($"Bill #{s.InvoiceNo} (₹ {s.GrandTotal:N2} on {s.SaleDate:dd-MMM})");
            }
        }
        else if (IsPaymentMode)
        {
            var purchases = _inventoryService.GetAllPurchases()
                .Where(p => p.SupplierName.Equals(accName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p.PurchaseDate)
                .Take(5);

            foreach (var p in purchases)
            {
                RecentPartyInvoices.Add($"Bill #{p.InvoiceNo} (₹ {p.GrandTotal:N2} on {p.PurchaseDate:dd-MMM})");
            }
        }
    }

    private static string FormatBalance(double balance)
    {
        if (Math.Abs(balance) < 0.0001) return "₹ 0.00 (Settled)";
        string sign = balance >= 0 ? "+" : "-";
        return $"{sign} ₹ {Math.Abs(balance):N2}";
    }

    public void LoadVoucher(FinancialVoucher v)
    {
        EditingVoucherId = v.Id;
        VoucherType = v.VoucherType;
        VoucherNo = v.VoucherNo;
        VoucherDate = v.VoucherDate;
        PaymentMode = v.PaymentMode;
        ReferenceNo = v.ReferenceNo;
        ReferenceDate = v.ReferenceDate;
        Narration = v.Narration;

        LoadAccounts();

        if (IsJournalMode)
        {
            SelectedDrAccount = AllAccounts.FirstOrDefault(a => a.AccountName.Equals(v.DebitAccountName, StringComparison.OrdinalIgnoreCase));
            SelectedCrAccount = AllAccounts.FirstOrDefault(a => a.AccountName.Equals(v.CreditAccountName, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            string primaryName = !string.IsNullOrWhiteSpace(v.PrimaryAccountName)
                ? v.PrimaryAccountName
                : (IsReceiptMode ? v.DebitAccountName : v.CreditAccountName);

            SelectedCashBankAccount = CashBankAccounts.FirstOrDefault(a => a.AccountName.Equals(primaryName, StringComparison.OrdinalIgnoreCase))
                ?? AllAccounts.FirstOrDefault(a => a.AccountName.Equals(primaryName, StringComparison.OrdinalIgnoreCase));
        }

        LineItems.Clear();
        if (v.Items != null && v.Items.Count > 0)
        {
            int idx = 1;
            foreach (var item in v.Items)
            {
                var row = new VoucherLineItemViewModel(OnLineItemChanged, RemoveLineItem)
                {
                    LineNumber = idx++,
                    SelectedAccount = AllAccounts.FirstOrDefault(a => a.AccountName.Equals(item.AccountName, StringComparison.OrdinalIgnoreCase))
                        ?? PartyAccounts.FirstOrDefault(a => a.AccountName.Equals(item.AccountName, StringComparison.OrdinalIgnoreCase)),
                    Amount = item.Amount,
                    ReferenceNo = item.ReferenceNo,
                    Remarks = item.Remarks
                };
                LineItems.Add(row);
            }
        }
        else
        {
            string partyName = IsReceiptMode ? v.CreditAccountName : v.DebitAccountName;
            var row = new VoucherLineItemViewModel(OnLineItemChanged, RemoveLineItem)
            {
                LineNumber = 1,
                SelectedAccount = AllAccounts.FirstOrDefault(a => a.AccountName.Equals(partyName, StringComparison.OrdinalIgnoreCase)),
                Amount = v.Amount,
                ReferenceNo = v.ReferenceNo,
                Remarks = v.Narration
            };
            LineItems.Add(row);
        }

        EnsureTrailingEmptyRow();
        RecalculateTotals();
    }

    public void ResetForm()
    {
        _editingVoucherId = 0;
        _selectedVoucher = null;
        OnPropertyChanged(nameof(SelectedVoucher));
        OnPropertyChanged(nameof(EditingVoucherId));
        OnPropertyChanged(nameof(IsEditing));

        VoucherDate = DateTime.Today;
        PaymentMode = "Cash";
        ReferenceNo = string.Empty;
        ReferenceDate = null;
        Narration = string.Empty;

        LoadAccounts();

        SelectedCashBankAccount = CashBankAccounts.FirstOrDefault(a => a.AccountName.Contains("Cash", StringComparison.OrdinalIgnoreCase))
            ?? CashBankAccounts.FirstOrDefault();

        SelectedDrAccount = AllAccounts.FirstOrDefault();
        SelectedCrAccount = AllAccounts.LastOrDefault();

        VoucherNo = _inventoryService.GetNextVoucherNo(VoucherType);

        LineItems.Clear();
        AddLineItem();

        RecalculateTotals();
    }

    public void Save()
    {
        if (IsJournalMode)
        {
            if (SelectedDrAccount == null || SelectedCrAccount == null)
            {
                MessageBox.Show("Please select both Debit (By/Dr) and Credit (To/Cr) accounts for Journal voucher.", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (SelectedDrAccount.Id == SelectedCrAccount.Id && SelectedDrAccount.Id > 0)
            {
                MessageBox.Show("Debit and Credit accounts cannot be the same account.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        else
        {
            if (SelectedCashBankAccount == null)
            {
                MessageBox.Show("Please select a Cash / Bank primary account.", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        var validItems = LineItems.Where(i => i.SelectedAccount != null && i.Amount > 0.0001).ToList();
        if (validItems.Count == 0)
        {
            MessageBox.Show("Please enter at least one line item in Particulars with a selected Account and Amount greater than ₹ 0.00.", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        double total = validItems.Sum(i => i.Amount);
        if (total <= 0.0001)
        {
            MessageBox.Show("Total voucher amount must be greater than ₹ 0.00.", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            string primaryName = IsJournalMode ? SelectedDrAccount!.AccountName : SelectedCashBankAccount!.AccountName;
            int? primaryId = IsJournalMode ? SelectedDrAccount!.Id : SelectedCashBankAccount!.Id;

            var voucher = new FinancialVoucher
            {
                Id = EditingVoucherId,
                VoucherType = VoucherType,
                VoucherNo = VoucherNo.Trim(),
                VoucherDate = VoucherDate,
                PrimaryAccountId = primaryId,
                PrimaryAccountName = primaryName,
                TotalAmount = total,
                Amount = total,
                PaymentMode = PaymentMode,
                ReferenceNo = ReferenceNo?.Trim(),
                ReferenceDate = ReferenceDate,
                Narration = Narration?.Trim(),
                CreatedAt = DateTime.Now,
                Items = validItems.Select(i => new VoucherLineItem
                {
                    AccountId = i.SelectedAccount?.Id,
                    AccountName = i.SelectedAccount?.AccountName ?? string.Empty,
                    Amount = i.Amount,
                    ReferenceNo = i.ReferenceNo?.Trim(),
                    Remarks = i.Remarks?.Trim()
                }).ToList()
            };

            if (IsReceiptMode)
            {
                voucher.DebitAccountId = primaryId;
                voucher.DebitAccountName = primaryName;
                voucher.CreditAccountId = validItems[0].SelectedAccount?.Id;
                voucher.CreditAccountName = validItems[0].SelectedAccount?.AccountName ?? string.Empty;
            }
            else if (IsPaymentMode)
            {
                voucher.CreditAccountId = primaryId;
                voucher.CreditAccountName = primaryName;
                voucher.DebitAccountId = validItems[0].SelectedAccount?.Id;
                voucher.DebitAccountName = validItems[0].SelectedAccount?.AccountName ?? string.Empty;
            }
            else
            {
                voucher.DebitAccountId = SelectedDrAccount?.Id;
                voucher.DebitAccountName = SelectedDrAccount?.AccountName ?? string.Empty;
                voucher.CreditAccountId = SelectedCrAccount?.Id;
                voucher.CreditAccountName = SelectedCrAccount?.AccountName ?? string.Empty;
            }

            int id = _inventoryService.SaveVoucher(voucher);
            LoadData();

            string action = IsEditing ? "updated" : "saved";
            string itemsSummary = string.Join("\n", validItems.Select(i => $"  • {i.SelectedAccount?.AccountName}: ₹ {i.Amount:N2}"));

            MessageBox.Show(
                $"{voucher.VoucherType} Voucher #{voucher.VoucherNo} {action} successfully!\n\nAccount: {voucher.PrimaryAccountName}\nTotal: ₹ {voucher.TotalAmount:N2}\n\nParticulars ({validItems.Count} lines):\n{itemsSummary}",
                "Voucher Saved",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            ResetForm();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving voucher: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void Delete()
    {
        if (EditingVoucherId == 0) return;

        var confirm = MessageBox.Show(
            $"Are you sure you want to delete {VoucherType} Voucher #{VoucherNo}?\nThis will automatically reverse all related ledger entries across all accounts.",
            "Delete Confirmation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            _inventoryService.DeleteVoucher(EditingVoucherId);
            LoadData();
            ResetForm();
            MessageBox.Show("Voucher deleted successfully and ledger postings reversed.", "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error deleting voucher: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void Print()
    {
        if (TotalAmount <= 0.0001 && EditingVoucherId == 0)
        {
            MessageBox.Show("Please enter or select a voucher to print.", "Print Voucher", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var validItems = LineItems.Where(i => i.SelectedAccount != null && i.Amount > 0.0001).ToList();
            string primaryName = IsJournalMode ? (SelectedDrAccount?.AccountName ?? "") : (SelectedCashBankAccount?.AccountName ?? "");

            var voucher = new FinancialVoucher
            {
                Id = EditingVoucherId,
                VoucherType = VoucherType,
                VoucherNo = VoucherNo,
                VoucherDate = VoucherDate,
                PrimaryAccountId = IsJournalMode ? SelectedDrAccount?.Id : SelectedCashBankAccount?.Id,
                PrimaryAccountName = primaryName,
                DebitAccountName = IsJournalMode ? (SelectedDrAccount?.AccountName ?? "") : (IsReceiptMode ? primaryName : (validItems.FirstOrDefault()?.SelectedAccount?.AccountName ?? "")),
                CreditAccountName = IsJournalMode ? (SelectedCrAccount?.AccountName ?? "") : (IsReceiptMode ? (validItems.FirstOrDefault()?.SelectedAccount?.AccountName ?? "") : primaryName),
                TotalAmount = TotalAmount,
                Amount = TotalAmount,
                PaymentMode = PaymentMode,
                ReferenceNo = ReferenceNo,
                ReferenceDate = ReferenceDate,
                Narration = Narration,
                Items = validItems.Select(i => new VoucherLineItem
                {
                    AccountId = i.SelectedAccount?.Id,
                    AccountName = i.SelectedAccount?.AccountName ?? string.Empty,
                    CurrentBalance = i.SelectedAccount?.CurrentBalance ?? 0.0,
                    Amount = i.Amount,
                    ReferenceNo = i.ReferenceNo,
                    Remarks = i.Remarks
                }).ToList()
            };

            var printWin = new PrintVoucherWindow(voucher);
            printWin.Owner = Application.Current.MainWindow;
            printWin.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to display print preview: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
