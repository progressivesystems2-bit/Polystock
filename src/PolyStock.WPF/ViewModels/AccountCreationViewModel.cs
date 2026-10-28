using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using PolyStock.Core.Common;
using PolyStock.Core.Models;
using PolyStock.Services;

namespace PolyStock.WPF.ViewModels;

public class AccountCreationViewModel : ObservableObject
{
    private readonly InventoryService _inventoryService;
    private Account? _selectedAccount;
    private string _accountType = "Customer";
    private string _accountName = string.Empty;
    private string? _address;
    private string? _mobile;
    private string? _gstin;
    private double _openingBalance = 0.0;
    private string _openingBalanceType = "+ (To Receive)";
    private string? _description;
    private string _searchText = string.Empty;

    public AccountCreationViewModel(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
        Accounts = new ObservableCollection<Account>();
        FilteredAccounts = new ObservableCollection<Account>();

        AccountTypes = new ObservableCollection<string>
        {
            "Customer",
            "Supplier",
            "Bank",
            "Cash",
            "Transporter",
            "Broker",
            "General"
        };

        OpeningBalanceTypes = new ObservableCollection<string>
        {
            "+ (To Receive)",
            "- (To Pay)"
        };

        AccountLedgerEntries = new ObservableCollection<AccountLedgerEntry>();

        SaveCommand = new RelayCommand(Save);
        NewCommand = new RelayCommand(ClearForm);
        DeleteCommand = new RelayCommand(Delete, CanDelete);

        LoadData();
    }

    public ObservableCollection<Account> Accounts { get; }
    public ObservableCollection<Account> FilteredAccounts { get; }
    public ObservableCollection<string> AccountTypes { get; }
    public ObservableCollection<string> OpeningBalanceTypes { get; }
    public ObservableCollection<AccountLedgerEntry> AccountLedgerEntries { get; }

    public Account? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            if (SetProperty(ref _selectedAccount, value))
            {
                AccountLedgerEntries.Clear();
                if (value != null)
                {
                    AccountType = value.AccountType;
                    AccountName = value.AccountName;
                    Address = value.Address;
                    Mobile = value.Mobile;
                    GSTIN = value.GSTIN;
                    OpeningBalance = value.OpeningBalance;
                    OpeningBalanceType = (value.OpeningBalanceType == "-" || value.OpeningBalanceType == "Cr")
                        ? "- (To Pay)"
                        : "+ (To Receive)";
                    Description = value.Description;

                    var ledger = _inventoryService.GetAccountLedger(value.Id, value.AccountName);
                    foreach (var entry in ledger)
                    {
                        AccountLedgerEntries.Add(entry);
                    }
                }
            }
        }
    }

    public string AccountType
    {
        get => _accountType;
        set
        {
            if (SetProperty(ref _accountType, value))
            {
                if (SelectedAccount == null)
                {
                    OpeningBalanceType = value.Equals("Supplier", StringComparison.OrdinalIgnoreCase)
                        ? "- (To Pay)"
                        : "+ (To Receive)";
                }
            }
        }
    }

    public string AccountName
    {
        get => _accountName;
        set => SetProperty(ref _accountName, value);
    }

    public string? Address
    {
        get => _address;
        set => SetProperty(ref _address, value);
    }

    public string? Mobile
    {
        get => _mobile;
        set => SetProperty(ref _mobile, value);
    }

    public string? GSTIN
    {
        get => _gstin;
        set => SetProperty(ref _gstin, value);
    }

    public double OpeningBalance
    {
        get => _openingBalance;
        set
        {
            if (SetProperty(ref _openingBalance, value))
            {
                OnPropertyChanged(nameof(OpeningBalanceText));
            }
        }
    }

    public string OpeningBalanceText
    {
        get => _openingBalance > 0.0001 ? _openingBalance.ToString("0.##") : string.Empty;
        set
        {
            if (double.TryParse(value, out var d) && d >= 0)
            {
                OpeningBalance = d;
            }
            else if (string.IsNullOrWhiteSpace(value))
            {
                OpeningBalance = 0.0;
            }
        }
    }

    public string OpeningBalanceType
    {
        get => _openingBalanceType;
        set => SetProperty(ref _openingBalanceType, value);
    }

    public string? Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public ICommand SaveCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand DeleteCommand { get; }

    public void LoadData()
    {
        Accounts.Clear();
        foreach (var acc in _inventoryService.GetAllAccounts())
        {
            Accounts.Add(acc);
        }
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredAccounts.Clear();
        var q = SearchText.Trim().ToLowerInvariant();
        var result = string.IsNullOrEmpty(q)
            ? Accounts
            : Accounts.Where(a => a.AccountName.ToLowerInvariant().Contains(q) ||
                                 a.AccountType.ToLowerInvariant().Contains(q) ||
                                 (a.Mobile?.ToLowerInvariant().Contains(q) ?? false) ||
                                 (a.Address?.ToLowerInvariant().Contains(q) ?? false) ||
                                 (a.GSTIN?.ToLowerInvariant().Contains(q) ?? false));

        foreach (var a in result)
        {
            FilteredAccounts.Add(a);
        }
    }

    private bool CanDelete() => SelectedAccount != null;

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(AccountName))
        {
            MessageBox.Show("Please enter Account Name.", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(AccountType))
        {
            MessageBox.Show("Please select an Account Type.", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var acc = SelectedAccount ?? new Account();
            acc.AccountType = AccountType.Trim();
            acc.AccountName = AccountName.Trim();
            acc.Address = Address?.Trim();
            acc.Mobile = Mobile?.Trim();
            acc.Email = SelectedAccount?.Email;
            acc.GSTIN = GSTIN?.Trim();
            acc.OpeningBalance = OpeningBalance;
            acc.OpeningBalanceType = OpeningBalanceType.StartsWith("-") ? "-" : "+";
            acc.Description = Description?.Trim();
            acc.CreatedAt = DateTime.Now;

            _inventoryService.SaveAccount(acc);
            LoadData();
            ClearForm();
            MessageBox.Show($"Account '{acc.AccountName}' saved successfully!", "PolyStock", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving Account: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearForm()
    {
        _selectedAccount = null;
        OnPropertyChanged(nameof(SelectedAccount));
        AccountType = "Customer";
        AccountName = string.Empty;
        Address = string.Empty;
        Mobile = string.Empty;
        GSTIN = string.Empty;
        OpeningBalance = 0.0;
        OpeningBalanceType = "+ (To Receive)";
        Description = string.Empty;
    }

    private void Delete()
    {
        if (SelectedAccount == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete Account '{SelectedAccount.AccountName}' ({SelectedAccount.AccountType})?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                _inventoryService.DeleteAccount(SelectedAccount.Id);
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cannot delete Account: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public void SelectAccountByName(string accountName)
    {
        var match = Accounts.FirstOrDefault(a => a.AccountName.Equals(accountName, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            SelectedAccount = match;
        }
    }

    public void SelectAccountById(int id)
    {
        var match = Accounts.FirstOrDefault(a => a.Id == id);
        if (match != null)
        {
            SelectedAccount = match;
        }
    }
}
