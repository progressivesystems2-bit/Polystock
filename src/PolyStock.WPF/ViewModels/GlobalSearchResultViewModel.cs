using System;
using PolyStock.Core.Common;
using PolyStock.Core.Models;

namespace PolyStock.WPF.ViewModels;

public class AccountQuickDetails
{
    public string AccountName { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public string? GSTIN { get; set; }
    public string? Address { get; set; }
    public double OpeningBalance { get; set; }
    public string OpeningBalanceType { get; set; } = "+";

    public string BalanceDisplay => OpeningBalance > 0 
        ? $"{(OpeningBalanceType == "-" || OpeningBalanceType == "Cr" ? "-" : "+")} ₹ {OpeningBalance:N2}" 
        : "₹ 0.00";

    public bool HasMobile => !string.IsNullOrWhiteSpace(Mobile);
    public bool HasEmail => !string.IsNullOrWhiteSpace(Email);
    public bool HasGSTIN => !string.IsNullOrWhiteSpace(GSTIN);
    public bool HasAddress => !string.IsNullOrWhiteSpace(Address);
}

public class ItemQuickDetails
{
    public string ItemName { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string? BrandName { get; set; }
    public string? HeadGroupName { get; set; }
    public double DefaultRatePerKg { get; set; }
    public string? Description { get; set; }

    public string RateDisplay => $"₹ {DefaultRatePerKg:N2} / kg";
    public bool HasBrand => !string.IsNullOrWhiteSpace(BrandName);
    public bool HasHeadGroup => !string.IsNullOrWhiteSpace(HeadGroupName);
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
}

public class GlobalSearchResultViewModel : ObservableObject
{
    private string _title = string.Empty;
    private string _detail = string.Empty;
    private string _icon = "🔍";
    private string _badge = "MENU";
    private string _badgeBrushHex = "#64748B";
    private string _navigationTarget = "Dashboard";
    private object? _navigationParam;
    private AccountQuickDetails? _accountDetails;
    private ItemQuickDetails? _itemDetails;

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Detail
    {
        get => _detail;
        set => SetProperty(ref _detail, value);
    }

    public string Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public string Badge
    {
        get => _badge;
        set => SetProperty(ref _badge, value);
    }

    public string BadgeBrushHex
    {
        get => _badgeBrushHex;
        set => SetProperty(ref _badgeBrushHex, value);
    }

    public string NavigationTarget
    {
        get => _navigationTarget;
        set => SetProperty(ref _navigationTarget, value);
    }

    public object? NavigationParam
    {
        get => _navigationParam;
        set => SetProperty(ref _navigationParam, value);
    }

    public AccountQuickDetails? AccountDetails
    {
        get => _accountDetails;
        set
        {
            if (SetProperty(ref _accountDetails, value))
            {
                OnPropertyChanged(nameof(HasAccountDetails));
                OnPropertyChanged(nameof(HasAnyDetails));
            }
        }
    }

    public ItemQuickDetails? ItemDetails
    {
        get => _itemDetails;
        set
        {
            if (SetProperty(ref _itemDetails, value))
            {
                OnPropertyChanged(nameof(HasItemDetails));
                OnPropertyChanged(nameof(HasAnyDetails));
            }
        }
    }

    public bool HasAccountDetails => AccountDetails != null;
    public bool HasItemDetails => ItemDetails != null;
    public bool HasAnyDetails => HasAccountDetails || HasItemDetails;

    public string DisplayText => string.IsNullOrWhiteSpace(Detail) ? Title : $"{Title}: {Detail}";
}
