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

public class LedgerReportViewModel : ObservableObject
{
    private readonly InventoryService _inventoryService;

    private bool _isGroupReportSelected = true;

    // Group Summary filters
    private string _selectedGroupType = "All Groups";
    private DateTime? _groupFromDate;
    private DateTime? _groupToDate;
    private string _groupSearchText = string.Empty;
    private Account? _selectedGroupAccount;

    // Individual Statement filters
    private Account? _selectedAccount;
    private DateTime? _statementFromDate;
    private DateTime? _statementToDate;
    private string _dateRangePreset = "All Time";
    private LedgerStatementItem? _selectedStatementEntry;

    public LedgerReportViewModel(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;

        GroupTypes = new ObservableCollection<string>
        {
            "All Groups",
            "Customer",
            "Supplier",
            "Cash & Bank",
            "Transporter",
            "Broker",
            "General"
        };

        DateRangePresets = new ObservableCollection<string>
        {
            "All Time",
            "This Month",
            "Last Month",
            "This Financial Year"
        };

        AllAccounts = new ObservableCollection<Account>();
        GroupAccounts = new ObservableCollection<Account>();
        FilteredGroupAccounts = new ObservableCollection<Account>();
        StatementEntries = new ObservableCollection<LedgerStatementItem>();

        // Commands
        SwitchToGroupReportCommand = new RelayCommand(() => IsGroupReportSelected = true);
        SwitchToIndividualStatementCommand = new RelayCommand(() => IsGroupReportSelected = false);
        BackToGroupReportCommand = new RelayCommand(() => IsGroupReportSelected = true);
        RefreshCommand = new RelayCommand(LoadData);
        PrintStatementCommand = new RelayCommand(PrintStatement);
        DrillDownAccountCommand = new RelayCommand(param =>
        {
            if (param is Account acc)
            {
                DrillDownToAccount(acc);
            }
            else if (SelectedGroupAccount != null)
            {
                DrillDownToAccount(SelectedGroupAccount);
            }
        });
        OpenTransactionCommand = new RelayCommand(param =>
        {
            if (param is LedgerStatementItem item)
            {
                OpenTransaction(item);
            }
            else if (SelectedStatementEntry != null)
            {
                OpenTransaction(SelectedStatementEntry);
            }
        });

        SetPresetDates("All Time");
        LoadData();
    }

    public Action<string, string>? RequestOpenTransactionAction { get; set; }

    public bool IsGroupReportSelected
    {
        get => _isGroupReportSelected;
        set
        {
            if (SetProperty(ref _isGroupReportSelected, value))
            {
                OnPropertyChanged(nameof(IsIndividualStatementSelected));
                if (value) LoadGroupReport();
                else LoadIndividualStatement();
            }
        }
    }

    public bool IsIndividualStatementSelected
    {
        get => !_isGroupReportSelected;
        set => IsGroupReportSelected = !value;
    }

    public ObservableCollection<string> GroupTypes { get; }
    public ObservableCollection<string> DateRangePresets { get; }
    public ObservableCollection<Account> AllAccounts { get; }
    public ObservableCollection<Account> GroupAccounts { get; }
    public ObservableCollection<Account> FilteredGroupAccounts { get; }
    public ObservableCollection<LedgerStatementItem> StatementEntries { get; }

    public string SelectedGroupType
    {
        get => _selectedGroupType;
        set
        {
            if (SetProperty(ref _selectedGroupType, value))
            {
                LoadGroupReport();
            }
        }
    }

    public DateTime? GroupFromDate
    {
        get => _groupFromDate;
        set
        {
            if (SetProperty(ref _groupFromDate, value))
            {
                LoadGroupReport();
            }
        }
    }

    public DateTime? GroupToDate
    {
        get => _groupToDate;
        set
        {
            if (SetProperty(ref _groupToDate, value))
            {
                LoadGroupReport();
            }
        }
    }

    public string GroupSearchText
    {
        get => _groupSearchText;
        set
        {
            if (SetProperty(ref _groupSearchText, value))
            {
                FilterGroupAccounts();
            }
        }
    }

    public Account? SelectedGroupAccount
    {
        get => _selectedGroupAccount;
        set => SetProperty(ref _selectedGroupAccount, value);
    }

    public Account? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            if (SetProperty(ref _selectedAccount, value))
            {
                LoadIndividualStatement();
            }
        }
    }

    public DateTime? StatementFromDate
    {
        get => _statementFromDate;
        set
        {
            if (SetProperty(ref _statementFromDate, value))
            {
                LoadIndividualStatement();
            }
        }
    }

    public DateTime? StatementToDate
    {
        get => _statementToDate;
        set
        {
            if (SetProperty(ref _statementToDate, value))
            {
                LoadIndividualStatement();
            }
        }
    }

    public string DateRangePreset
    {
        get => _dateRangePreset;
        set
        {
            if (SetProperty(ref _dateRangePreset, value))
            {
                SetPresetDates(value);
            }
        }
    }

    public LedgerStatementItem? SelectedStatementEntry
    {
        get => _selectedStatementEntry;
        set
        {
            if (SetProperty(ref _selectedStatementEntry, value))
            {
                OnPropertyChanged(nameof(CanOpenSelectedTransaction));
            }
        }
    }

    public bool CanOpenSelectedTransaction => SelectedStatementEntry != null && !SelectedStatementEntry.TransactionType.Equals("OPENING", StringComparison.OrdinalIgnoreCase);

    // Group Summary Totals
    public double TotalGroupDebit { get; private set; }
    public double TotalGroupCredit { get; private set; }
    public double TotalGroupClosingBalance { get; private set; }
    public string FormattedTotalGroupClosing => Math.Abs(TotalGroupClosingBalance) > 0.0001
        ? $"{(TotalGroupClosingBalance >= 0 ? "+" : "-")} ₹ {Math.Abs(TotalGroupClosingBalance):N2}"
        : "₹ 0.00";

    // Individual Statement Totals
    public double StatementOpeningBalance { get; private set; }
    public string StatementOpeningBalanceType { get; private set; } = "+";
    public double StatementTotalDebit { get; private set; }
    public double StatementTotalCredit { get; private set; }
    public double StatementClosingBalance { get; private set; }
    public string StatementClosingBalanceType => StatementClosingBalance >= 0 ? "+" : "-";

    public string FormattedStatementOpening => Math.Abs(StatementOpeningBalance) > 0.0001
        ? $"{(StatementOpeningBalanceType == "-" || StatementOpeningBalanceType == "Cr" ? "-" : "+")} ₹ {Math.Abs(StatementOpeningBalance):N2}"
        : "₹ 0.00";

    public string FormattedStatementClosing => Math.Abs(StatementClosingBalance) > 0.0001
        ? $"{(StatementClosingBalance >= 0 ? "+" : "-")} ₹ {Math.Abs(StatementClosingBalance):N2}"
        : "₹ 0.00";

    public ICommand SwitchToGroupReportCommand { get; }
    public ICommand SwitchToIndividualStatementCommand { get; }
    public ICommand BackToGroupReportCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand PrintStatementCommand { get; }
    public ICommand DrillDownAccountCommand { get; }
    public ICommand OpenTransactionCommand { get; }

    public void OpenTransaction(LedgerStatementItem? item)
    {
        if (item == null) return;

        if (item.TransactionType.Equals("OPENING", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(
                $"Opening balance of {item.FormattedRunningBalance} is defined in Account Master.\nTo modify it, go to Master Data ➔ Account Master.",
                "Opening Balance",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(item.RefNo) || item.RefNo == "-")
        {
            MessageBox.Show("No voucher or reference number associated with this transaction.", "Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (RequestOpenTransactionAction != null)
        {
            RequestOpenTransactionAction(item.TransactionType, item.RefNo);
        }
    }

    public void LoadData()
    {
        LoadAllAccountsList();
        if (IsGroupReportSelected)
        {
            LoadGroupReport();
        }
        else
        {
            LoadIndividualStatement();
        }
    }

    public void LoadAllAccountsList()
    {
        var accounts = _inventoryService.GetAllAccounts();
        AllAccounts.Clear();
        foreach (var acc in accounts)
        {
            AllAccounts.Add(acc);
        }

        if (SelectedAccount == null && AllAccounts.Count > 0)
        {
            SelectedAccount = AllAccounts.FirstOrDefault(a => a.AccountType.Equals("Customer", StringComparison.OrdinalIgnoreCase))
                ?? AllAccounts.FirstOrDefault();
        }
    }

    public void LoadGroupReport()
    {
        var list = _inventoryService.GetGroupSummaryReport(SelectedGroupType, GroupFromDate, GroupToDate);
        GroupAccounts.Clear();
        foreach (var acc in list)
        {
            GroupAccounts.Add(acc);
        }

        FilterGroupAccounts();
    }

    private void FilterGroupAccounts()
    {
        FilteredGroupAccounts.Clear();
        var query = GroupAccounts.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(GroupSearchText))
        {
            var text = GroupSearchText.Trim();
            query = query.Where(a => a.AccountName.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                                     a.AccountType.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                                     (!string.IsNullOrWhiteSpace(a.Mobile) && a.Mobile.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
                                     (!string.IsNullOrWhiteSpace(a.GSTIN) && a.GSTIN.Contains(text, StringComparison.OrdinalIgnoreCase)));
        }

        var resultList = query.ToList();
        foreach (var a in resultList)
        {
            FilteredGroupAccounts.Add(a);
        }

        TotalGroupDebit = resultList.Sum(a => a.PeriodDebit);
        TotalGroupCredit = resultList.Sum(a => a.PeriodCredit);
        TotalGroupClosingBalance = resultList.Sum(a => a.PeriodClosingBalance);

        OnPropertyChanged(nameof(TotalGroupDebit));
        OnPropertyChanged(nameof(TotalGroupCredit));
        OnPropertyChanged(nameof(TotalGroupClosingBalance));
        OnPropertyChanged(nameof(FormattedTotalGroupClosing));
    }

    public void LoadIndividualStatement()
    {
        StatementEntries.Clear();
        if (SelectedAccount == null) return;

        var entries = _inventoryService.GetAccountStatement(SelectedAccount.Id, StatementFromDate, StatementToDate);
        foreach (var item in entries)
        {
            StatementEntries.Add(item);
        }

        var openingRow = entries.FirstOrDefault(e => e.TransactionType == "OPENING");
        if (openingRow != null)
        {
            StatementOpeningBalance = openingRow.RunningBalance;
            StatementOpeningBalanceType = openingRow.RunningBalanceType;
        }
        else
        {
            StatementOpeningBalance = 0.0;
            StatementOpeningBalanceType = "+";
        }

        // Debits & credits excluding initial opening row
        var periodRows = entries.Where(e => e.TransactionType != "OPENING").ToList();
        StatementTotalDebit = periodRows.Sum(e => e.Debit);
        StatementTotalCredit = periodRows.Sum(e => e.Credit);

        var lastRow = entries.LastOrDefault();
        if (lastRow != null)
        {
            StatementClosingBalance = lastRow.RunningBalance;
        }
        else
        {
            StatementClosingBalance = StatementOpeningBalance;
        }

        OnPropertyChanged(nameof(StatementOpeningBalance));
        OnPropertyChanged(nameof(StatementOpeningBalanceType));
        OnPropertyChanged(nameof(StatementTotalDebit));
        OnPropertyChanged(nameof(StatementTotalCredit));
        OnPropertyChanged(nameof(StatementClosingBalance));
        OnPropertyChanged(nameof(StatementClosingBalanceType));
        OnPropertyChanged(nameof(FormattedStatementOpening));
        OnPropertyChanged(nameof(FormattedStatementClosing));
    }

    public void DrillDownToAccount(Account acc)
    {
        SelectedAccount = AllAccounts.FirstOrDefault(a => a.Id == acc.Id) ?? acc;
        IsGroupReportSelected = false;
    }

    private void SetPresetDates(string preset)
    {
        DateTime today = DateTime.Today;
        switch (preset)
        {
            case "This Month":
                StatementFromDate = new DateTime(today.Year, today.Month, 1);
                StatementToDate = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
                break;
            case "Last Month":
                var lastMonth = today.AddMonths(-1);
                StatementFromDate = new DateTime(lastMonth.Year, lastMonth.Month, 1);
                StatementToDate = new DateTime(lastMonth.Year, lastMonth.Month, DateTime.DaysInMonth(lastMonth.Year, lastMonth.Month));
                break;
            case "This Financial Year":
                int startYear = today.Month >= 4 ? today.Year : today.Year - 1;
                StatementFromDate = new DateTime(startYear, 4, 1);
                StatementToDate = new DateTime(startYear + 1, 3, 31);
                break;
            default: // All Time
                StatementFromDate = null;
                StatementToDate = null;
                break;
        }
    }

    public void PrintStatement()
    {
        if (SelectedAccount == null)
        {
            MessageBox.Show("Please select an Account to print the ledger statement.", "Print Ledger", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var printWin = new PrintLedgerStatementWindow(SelectedAccount, StatementEntries.ToList(), StatementFromDate, StatementToDate, StatementOpeningBalance, StatementOpeningBalanceType, StatementTotalDebit, StatementTotalCredit, StatementClosingBalance);
            printWin.Owner = Application.Current.MainWindow;
            printWin.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to display statement print preview: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
