using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PolyStock.Core.Common;
using PolyStock.Core.Models;
using PolyStock.Services;
using PolyStock.WPF.Themes;

namespace PolyStock.WPF.ViewModels;

public class MainViewModel : ObservableObject
{
    private readonly InventoryService _inventoryService;
    private object? _currentView;
    private string _currentViewTitle = "Dashboard";
    private string _selectedThemeId = "Fluent";
    private string _currentViewName = "Dashboard";
    private DocumentTabItem? _activeTab;

    private CancellationTokenSource? _globalSearchCts;
    private string _globalSearchText = string.Empty;
    private bool _isGlobalSearchOpen;
    private GlobalSearchResultViewModel? _selectedSearchResult;

    private List<Account>? _cachedAccounts;
    private List<Item>? _cachedItems;
    private List<Brand>? _cachedBrands;
    private List<HeadGroup>? _cachedHeadGroups;

    public MainViewModel(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;

        InvoiceHistoryVM = new InvoiceHistoryViewModel(_inventoryService);
        SalesVM = new SalesViewModel(_inventoryService);
        SalesVM.RequestOpenHistoryAction = () => { OpenTab("Invoices"); InvoiceHistoryVM.IsSalesSelected = true; };
        PurchaseVM = new PurchaseViewModel(_inventoryService);
        PurchaseVM.RequestOpenHistoryAction = () => { OpenTab("Invoices"); InvoiceHistoryVM.IsPurchasesSelected = true; };
        DashboardVM = new DashboardViewModel(_inventoryService);
        DashboardVM.RequestOpenTabAction = viewId => OpenTab(viewId);
        DashboardVM.RequestOpenBillAction = (type, billId) =>
        {
            if (type.Equals("SALE", StringComparison.OrdinalIgnoreCase))
            {
                OpenTab("Sales");
                var sale = _inventoryService.GetSaleById(billId);
                if (sale != null) SalesVM.LoadBill(sale);
            }
            else if (type.Equals("PURCHASE", StringComparison.OrdinalIgnoreCase))
            {
                OpenTab("Purchase");
                var purchase = _inventoryService.GetPurchaseById(billId);
                if (purchase != null) PurchaseVM.LoadBill(purchase);
            }
        };
        HeadGroupVM = new HeadGroupViewModel(_inventoryService);
        BrandVM = new BrandViewModel(_inventoryService);
        GodownVM = new GodownViewModel(_inventoryService);
        AccountCreationVM = new AccountCreationViewModel(_inventoryService);
        ItemCreationVM = new ItemCreationViewModel(_inventoryService);
        StockTransferVM = new StockTransferViewModel(_inventoryService);
        StockRegisterVM = new StockRegisterViewModel(_inventoryService);
        VoucherVM = new VoucherViewModel(_inventoryService);
        ShortcutsVM = new ShortcutsViewModel(viewId => OpenTab(viewId));
        LedgerReportVM = new LedgerReportViewModel(_inventoryService);
        LedgerReportVM.RequestOpenTransactionAction = (type, refNo) =>
        {
            if (string.IsNullOrWhiteSpace(refNo)) return;

            if (type.Equals("SALE", StringComparison.OrdinalIgnoreCase))
            {
                var sale = _inventoryService.GetSaleByInvoiceNo(refNo);
                if (sale != null)
                {
                    OpenTab("Sales");
                    SalesVM.LoadBill(sale);
                }
                else
                {
                    MessageBox.Show($"Sales Bill #{refNo} could not be found.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else if (type.Equals("PURCHASE", StringComparison.OrdinalIgnoreCase))
            {
                var purchase = _inventoryService.GetPurchaseByInvoiceNo(refNo);
                if (purchase != null)
                {
                    OpenTab("Purchase");
                    PurchaseVM.LoadBill(purchase);
                }
                else
                {
                    MessageBox.Show($"Purchase Voucher #{refNo} could not be found.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else if (type.Equals("RECEIPT", StringComparison.OrdinalIgnoreCase) ||
                     type.Equals("PAYMENT", StringComparison.OrdinalIgnoreCase) ||
                     type.Equals("JOURNAL", StringComparison.OrdinalIgnoreCase) ||
                     type.Equals("VOUCHER", StringComparison.OrdinalIgnoreCase))
            {
                var voucher = _inventoryService.GetVoucherByVoucherNo(refNo);
                if (voucher != null)
                {
                    OpenTab("Vouchers");
                    VoucherVM.LoadVoucher(voucher);
                }
                else
                {
                    MessageBox.Show($"Financial Voucher #{refNo} could not be found.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        };

        ProfitLossReportVM = new ProfitLossReportViewModel(_inventoryService);
        UserSecurityVM = new UserSecurityViewModel(_inventoryService);
        SettingsVM = new SettingsViewModel(_inventoryService);
        SettingsVM.SettingsSavedAction = () => SalesVM.RefreshThreshold();

        NavigateCommand = new RelayCommand(param => OpenTab(param?.ToString() ?? "Dashboard"));
        OpenTabCommand = new RelayCommand(param => OpenTab(param?.ToString() ?? "Dashboard"));
        CloseTabCommand = new RelayCommand(param => CloseTab(param));
        CloseActiveTabCommand = new RelayCommand(() => CloseActiveTab());

        OpenReceiptCommand = new RelayCommand(() => { if (CanAccessVouchers) { OpenTab("Vouchers"); VoucherVM.SetMode("RECEIPT"); } else NotifyAccessDenied("Vouchers"); });
        OpenPaymentCommand = new RelayCommand(() => { if (CanAccessVouchers) { OpenTab("Vouchers"); VoucherVM.SetMode("PAYMENT"); } else NotifyAccessDenied("Vouchers"); });
        OpenJournalCommand = new RelayCommand(() => { if (CanAccessVouchers) { OpenTab("Vouchers"); VoucherVM.SetMode("JOURNAL"); } else NotifyAccessDenied("Vouchers"); });
        OpenShortcutsCommand = new RelayCommand(param => OpenShortcuts(param?.ToString()));

        SwitchUserCommand = new RelayCommand(() => RequestUserSwitchAction?.Invoke());
        LogoutCommand = new RelayCommand(() => RequestUserSwitchAction?.Invoke());

        OpenGlobalSearchResultCommand = new RelayCommand(param => OpenGlobalSearchResult(param as GlobalSearchResultViewModel ?? SelectedSearchResult));
        ClearGlobalSearchCommand = new RelayCommand(() =>
        {
            GlobalSearchText = string.Empty;
            IsGlobalSearchOpen = false;
        });

        CheckForUpdatesCommand = new RelayCommand(async () =>
        {
            await Services.UpdateService.CheckForUpdatesManualAsync(Application.Current.MainWindow);
        });

        AboutCommand = new RelayCommand(() =>
        {
            MessageBox.Show(
                Application.Current.MainWindow,
                $"PolyStock - Polymer & Commodity Bag Inventory & Sales Management\n\n" +
                $"Version: v{AppVersion}\n" +
                $"Publisher: Tauras Technology\n" +
                $"Updates: Automated via Velopack & GitHub Releases\n" +
                $"Repository: {Services.UpdateService.GitHubRepoUrl}",
                "About PolyStock",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        });

        // Load saved theme
        _selectedThemeId = ThemeManager.LoadSavedThemeId();
        _selectedTheme = AvailableThemes.FirstOrDefault(t => t.Id == _selectedThemeId) ?? AvailableThemes.FirstOrDefault();
        if (_selectedTheme != null)
        {
            ThemeManager.Apply(_selectedTheme.Id);
        }

        // Listen for security session changes
        _inventoryService.Security.UserChanged += (s, e) => OnSecurityUserChanged();

        // Open initial tab based on permissions
        OpenInitialTab();
    }

    public DashboardViewModel DashboardVM { get; }
    public HeadGroupViewModel HeadGroupVM { get; }
    public BrandViewModel BrandVM { get; }
    public GodownViewModel GodownVM { get; }
    public AccountCreationViewModel AccountCreationVM { get; }
    public ItemCreationViewModel ItemCreationVM { get; }
    public StockTransferViewModel StockTransferVM { get; }
    public PurchaseViewModel PurchaseVM { get; }
    public SalesViewModel SalesVM { get; }
    public StockRegisterViewModel StockRegisterVM { get; }
    public InvoiceHistoryViewModel InvoiceHistoryVM { get; }
    public VoucherViewModel VoucherVM { get; }
    public LedgerReportViewModel LedgerReportVM { get; }
    public ProfitLossReportViewModel ProfitLossReportVM { get; }
    public ShortcutsViewModel ShortcutsVM { get; }
    public UserSecurityViewModel UserSecurityVM { get; }
    public SettingsViewModel SettingsVM { get; }

    public SecurityService Security => _inventoryService.Security;
    public User? CurrentUser => Security.CurrentUser;
    public string CurrentUserDisplay => CurrentUser != null ? $"{CurrentUser.DisplayName} ({CurrentUser.RoleName})" : "Guest";
    public string CurrentUserRole => CurrentUser?.RoleName ?? "Guest";
    public bool IsAdmin => Security.IsAdmin;

    public bool CanAccessUserSecurity => Security.CanAccessTab("UserSecurity");
    public bool CanAccessSettings => Security.CanAccessTab("Settings");
    public bool CanAccessSales => Security.CanAccessTab("Sales");
    public bool CanAccessPurchase => Security.CanAccessTab("Purchase");
    public bool CanAccessStockTransfer => Security.CanAccessTab("StockTransfer");
    public bool CanAccessVouchers => Security.CanAccessTab("Vouchers");
    public bool CanAccessDashboard => Security.CanAccessTab("Dashboard");
    public bool CanAccessStockRegister => Security.CanAccessTab("StockRegister");
    public bool CanAccessInvoices => Security.CanAccessTab("Invoices");
    public bool CanAccessLedgerReports => Security.CanAccessTab("LedgerReports");
    public bool CanAccessProfitLossReport => Security.CanAccessTab("ProfitLossReport");
    public bool CanAccessItemMaster => Security.CanAccessTab("ItemCreation");
    public bool CanAccessHeadGroupMaster => Security.CanAccessTab("HeadGroup");
    public bool CanAccessBrandMaster => Security.CanAccessTab("Brand");
    public bool CanAccessGodownMaster => Security.CanAccessTab("Godown");
    public bool CanAccessAccountMaster => Security.CanAccessTab("AccountCreation");

    public Action? RequestUserSwitchAction { get; set; }
    public ICommand SwitchUserCommand { get; }
    public ICommand LogoutCommand { get; }

    public IReadOnlyList<ThemeInfo> AvailableThemes => ThemeManager.Themes;

    public ObservableCollection<DocumentTabItem> OpenTabs { get; } = new();

    public DocumentTabItem? ActiveTab
    {
        get => _activeTab;
        set
        {
            _activeTab = value;
            OnPropertyChanged();
            if (value != null)
            {
                _currentViewName = value.Id;
                CurrentViewTitle = value.Title;
                CurrentView = value.ContentViewModel;
                OnPropertyChanged(nameof(CurrentViewName));
                OnPropertyChanged(nameof(CurrentViewTitle));
                OnPropertyChanged(nameof(CurrentView));
                OnPropertyChanged(nameof(CurrentPageShortcutsSummary));
                OnPropertyChanged(nameof(CurrentPageShortcutsTooltip));
                RefreshTabContent(value.Id);
            }
            else if (OpenTabs.Count > 0)
            {
                ActiveTab = OpenTabs.LastOrDefault();
            }
            else
            {
                OpenTab("Dashboard");
            }
        }
    }

    private ThemeInfo? _selectedTheme;
    public ThemeInfo? SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
            {
                if (value != null)
                {
                    _selectedThemeId = value.Id;
                    ThemeManager.Apply(value.Id);
                }
            }
        }
    }

    public string SelectedThemeId
    {
        get => _selectedThemeId;
        set
        {
            if (SetProperty(ref _selectedThemeId, value))
            {
                _selectedTheme = AvailableThemes.FirstOrDefault(t => t.Id == value);
                OnPropertyChanged(nameof(SelectedTheme));
                ThemeManager.Apply(value);
            }
        }
    }

    public string CurrentViewName
    {
        get => _currentViewName;
        set
        {
            if (SetProperty(ref _currentViewName, value))
            {
                OpenTab(value);
            }
        }
    }

    public object? CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    public string CurrentViewTitle
    {
        get => _currentViewTitle;
        set => SetProperty(ref _currentViewTitle, value);
    }

    public ICommand NavigateCommand { get; }
    public ICommand OpenTabCommand { get; }
    public ICommand CloseTabCommand { get; }
    public ICommand CloseActiveTabCommand { get; }
    public ICommand OpenReceiptCommand { get; }
    public ICommand OpenPaymentCommand { get; }
    public ICommand OpenJournalCommand { get; }
    public ICommand OpenShortcutsCommand { get; }
    public ICommand OpenGlobalSearchResultCommand { get; }
    public ICommand ClearGlobalSearchCommand { get; }
    public ICommand CheckForUpdatesCommand { get; }
    public ICommand AboutCommand { get; }

    public string AppVersion => Services.UpdateService.CurrentVersionString;

    public ObservableCollection<GlobalSearchResultViewModel> GlobalSearchResults { get; } = new();

    public string GlobalSearchText
    {
        get => _globalSearchText;
        set
        {
            if (SetProperty(ref _globalSearchText, value))
            {
                _ = RefreshGlobalSearchAsync(value);
            }
        }
    }

    public bool IsGlobalSearchOpen
    {
        get => _isGlobalSearchOpen;
        set => SetProperty(ref _isGlobalSearchOpen, value);
    }

    public GlobalSearchResultViewModel? SelectedSearchResult
    {
        get => _selectedSearchResult;
        set => SetProperty(ref _selectedSearchResult, value);
    }

    public string CurrentPageShortcutsSummary
    {
        get
        {
            return _currentViewName switch
            {
                "Sales" => "F2 Date • F3 Party • F4 Item • F8 Save • F9 Print • Ctrl+A",
                "Purchase" => "F2 Date • Ctrl+A Save • Ctrl+P Print • Ctrl+N New",
                "StockTransfer" => "F8/Ctrl+S Save Transfer • Ctrl+N New • Ctrl+P Print Challan",
                "Vouchers" => "F5 Pay • F6 Rcpt • F7 Jrnl • Ctrl+A Save • Ctrl+D Del",
                "LedgerReports" => "Enter Drill-down • Esc Back to Summary",
                "StockRegister" => "Live Dual-Qty • Fast Search & Valuation",
                "ProfitLossReport" => "F2 Date • F5 Refresh • Ctrl+E Export CSV • Ctrl+P Print",
                "Invoices" => "Double-Click View • Filter Sales/Purchase",
                "ItemCreation" or "Brand" or "Godown" or "AccountCreation" or "HeadGroup" => "Enter Next • Tab Navigation • Save Record",
                "Shortcuts" => "Keyboard Shortcuts Cheatsheet Active",
                _ => "F1 Help • F5 Payment • F6 Receipt • F7 Journal • Ctrl+W Close"
            };
        }
    }

    public string CurrentPageShortcutsTooltip
    {
        get
        {
            return _currentViewName switch
            {
                "Sales" => "Sales Entry Shortcuts:\n• F2: Focus Date\n• F3: Focus Customer\n• F4: Focus Line Items\n• F7: Focus GST Rate\n• F8 / Ctrl+A: Save Invoice\n• F9 / Ctrl+P: Print Invoice\n• Ctrl+N: New Bill\n• Ctrl+L: Sales History\n• Shift+Enter: Loose Kg\n• Ctrl+Del: Delete Row\n\nClick to open full Shortcuts Cheatsheet (F1)",
                "Purchase" => "Purchase Inward Shortcuts:\n• F2: Focus Date\n• Ctrl+A / Ctrl+S: Save Inward\n• Ctrl+N: New Inward\n• Ctrl+P: Print Inward\n• Ctrl+L: Purchase History\n• Enter: Rapid Next Field\n• Ctrl+Del: Delete Row\n\nClick to open full Shortcuts Cheatsheet (F1)",
                "StockTransfer" => "Stock Transfer Shortcuts:\n• F8 / Ctrl+S: Save Transfer\n• Ctrl+N: New Transfer\n• Ctrl+P: Print Challan\n• Tab: Navigate Grid\n\nClick to open full Shortcuts Cheatsheet (F1)",
                "Vouchers" => "Financial Vouchers Shortcuts:\n• F5: Payment Mode\n• F6: Receipt Mode\n• F7: Journal Mode\n• Ctrl+A / Ctrl+S: Save Voucher\n• Ctrl+N: New Voucher\n• Ctrl+P: Print Voucher\n• Ctrl+D: Delete Voucher\n• Enter in Narration: Quick Save\n\nClick to open full Shortcuts Cheatsheet (F1)",
                "LedgerReports" => "Ledger Reports Shortcuts:\n• Enter: Drill-down to Account Statement or Transaction Bill\n• Esc: Back to Group Summary (Tally ERP style)\n\nClick to open full Shortcuts Cheatsheet (F1)",
                "StockRegister" => "Stock Register Shortcuts:\n• Instant Search by Item / Brand / Godown\n• Live Dual-Unit Bag & Loose kg Auto-calculation\n\nClick to open full Shortcuts Cheatsheet (F1)",
                "ProfitLossReport" => "Profit & Loss (P/L) Report Shortcuts:\n• F2: Date Range Filter\n• F5 / Enter: Generate Report\n• Ctrl+E: Export to CSV\n• Ctrl+P: Print Trading Statement\n\nClick to open full Shortcuts Cheatsheet (F1)",
                "Invoices" => "Invoices & History Shortcuts:\n• Double-click row: Load Bill into Sales or Purchase view\n\nClick to open full Shortcuts Cheatsheet (F1)",
                _ => "Global Shortcuts:\n• F1: Keyboard Shortcuts Guide\n• Ctrl+W: Close Current Tab\n• F5: Payment Voucher\n• F6: Receipt Voucher\n• F7: Journal Voucher\n\nClick to open full Shortcuts Cheatsheet (F1)"
            };
        }
    }

    public void OpenShortcuts(string? category = null)
    {
        if (!string.IsNullOrWhiteSpace(category))
        {
            ShortcutsVM.SelectedCategory = category;
        }
        else
        {
            ShortcutsVM.SetContextCategory(_currentViewName);
        }
        OpenTab("Shortcuts");
    }

    public void CloseActiveTab()
    {
        if (ActiveTab != null)
        {
            CloseTab(ActiveTab);
        }
    }

    public void OpenInitialTab()
    {
        string[] preferred = { "Dashboard", "Sales", "Purchase", "StockRegister", "Invoices", "Vouchers", "ItemCreation" };
        foreach (var viewId in preferred)
        {
            if (Security.CanAccessTab(viewId))
            {
                OpenTab(viewId);
                return;
            }
        }

        OpenTab("Shortcuts");
    }

    public void NotifyAccessDenied(string viewId)
    {
        MessageBox.Show(
            $"Access Denied: Your account role does not have permission to access the '{viewId}' module.\nPlease contact your system administrator.",
            "Access Restricted",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void OnSecurityUserChanged()
    {
        OnPropertyChanged(nameof(CurrentUser));
        OnPropertyChanged(nameof(CurrentUserDisplay));
        OnPropertyChanged(nameof(CurrentUserRole));
        OnPropertyChanged(nameof(IsAdmin));

        OnPropertyChanged(nameof(CanAccessUserSecurity));
        OnPropertyChanged(nameof(CanAccessSettings));
        OnPropertyChanged(nameof(CanAccessSales));
        OnPropertyChanged(nameof(CanAccessPurchase));
        OnPropertyChanged(nameof(CanAccessStockTransfer));
        OnPropertyChanged(nameof(CanAccessVouchers));
        OnPropertyChanged(nameof(CanAccessDashboard));
        OnPropertyChanged(nameof(CanAccessStockRegister));
        OnPropertyChanged(nameof(CanAccessInvoices));
        OnPropertyChanged(nameof(CanAccessLedgerReports));
        OnPropertyChanged(nameof(CanAccessProfitLossReport));
        OnPropertyChanged(nameof(CanAccessItemMaster));
        OnPropertyChanged(nameof(CanAccessHeadGroupMaster));
        OnPropertyChanged(nameof(CanAccessBrandMaster));
        OnPropertyChanged(nameof(CanAccessGodownMaster));
        OnPropertyChanged(nameof(CanAccessAccountMaster));

        // Purge unauthorized open tabs
        var unauthorizedTabs = OpenTabs.Where(t => !Security.CanAccessTab(t.Id)).ToList();
        foreach (var tab in unauthorizedTabs)
        {
            OpenTabs.Remove(tab);
        }

        if (ActiveTab == null || !Security.CanAccessTab(ActiveTab.Id))
        {
            OpenInitialTab();
        }
    }

    public void OpenTab(string viewId)
    {
        if (!Security.CanAccessTab(viewId))
        {
            NotifyAccessDenied(viewId);
            return;
        }

        var existing = OpenTabs.FirstOrDefault(t => t.Id.Equals(viewId, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            ActiveTab = existing;
            return;
        }

        var newTab = CreateTab(viewId);
        OpenTabs.Add(newTab);
        ActiveTab = newTab;
    }

    public void CloseTab(object? param)
    {
        DocumentTabItem? tab = param as DocumentTabItem;
        if (tab == null && param is string tabId)
        {
            tab = OpenTabs.FirstOrDefault(t => t.Id.Equals(tabId, StringComparison.OrdinalIgnoreCase));
        }

        if (tab == null) return;
        if (!tab.CanClose && OpenTabs.Count <= 1) return;

        int index = OpenTabs.IndexOf(tab);
        bool wasActiveOrCurrent = (ActiveTab == tab || _currentViewName.Equals(tab.Id, StringComparison.OrdinalIgnoreCase));

        OpenTabs.Remove(tab);

        if (OpenTabs.Count == 0)
        {
            OpenTab("Dashboard");
            return;
        }

        if (wasActiveOrCurrent || ActiveTab == null || !OpenTabs.Contains(ActiveTab))
        {
            int newIndex = Math.Clamp(index, 0, OpenTabs.Count - 1);
            ActiveTab = OpenTabs[newIndex];
        }
        else
        {
            // Re-assert active tab view properties
            _currentViewName = ActiveTab.Id;
            CurrentViewTitle = ActiveTab.Title;
            CurrentView = ActiveTab.ContentViewModel;
            OnPropertyChanged(nameof(CurrentViewName));
            OnPropertyChanged(nameof(CurrentViewTitle));
            OnPropertyChanged(nameof(CurrentView));
            OnPropertyChanged(nameof(CurrentPageShortcutsSummary));
            OnPropertyChanged(nameof(CurrentPageShortcutsTooltip));
        }
    }

    private DocumentTabItem CreateTab(string viewId)
    {
        DocumentTabItem tab = viewId switch
        {
            "Dashboard" => new DocumentTabItem
            {
                Id = "Dashboard",
                Title = "Dashboard",
                Icon = "📊",
                ContentViewModel = DashboardVM,
                CanClose = false
            },
            "Sales" => new DocumentTabItem
            {
                Id = "Sales",
                Title = "Sales Entry (Dual-Qty)",
                Icon = "🛒",
                ContentViewModel = SalesVM,
                CanClose = true
            },
            "Purchase" => new DocumentTabItem
            {
                Id = "Purchase",
                Title = "Purchase Inward",
                Icon = "📥",
                ContentViewModel = PurchaseVM,
                CanClose = true
            },
            "StockTransfer" => new DocumentTabItem
            {
                Id = "StockTransfer",
                Title = "Stock Transfer",
                Icon = "🚚",
                ContentViewModel = StockTransferVM,
                CanClose = true
            },
            "StockRegister" => new DocumentTabItem
            {
                Id = "StockRegister",
                Title = "Live Stock Register",
                Icon = "📦",
                ContentViewModel = StockRegisterVM,
                CanClose = true
            },
            "ItemCreation" => new DocumentTabItem
            {
                Id = "ItemCreation",
                Title = "Item Master",
                Icon = "🏷",
                ContentViewModel = ItemCreationVM,
                CanClose = true
            },
            "HeadGroup" => new DocumentTabItem
            {
                Id = "HeadGroup",
                Title = "Head Group Master",
                Icon = "📁",
                ContentViewModel = HeadGroupVM,
                CanClose = true
            },
            "Brand" => new DocumentTabItem
            {
                Id = "Brand",
                Title = "Brand Master",
                Icon = "🏢",
                ContentViewModel = BrandVM,
                CanClose = true
            },
            "Godown" => new DocumentTabItem
            {
                Id = "Godown",
                Title = "Godown Master",
                Icon = "🏭",
                ContentViewModel = GodownVM,
                CanClose = true
            },
            "AccountCreation" => new DocumentTabItem
            {
                Id = "AccountCreation",
                Title = "Account Master",
                Icon = "👥",
                ContentViewModel = AccountCreationVM,
                CanClose = true
            },
            "Invoices" => new DocumentTabItem
            {
                Id = "Invoices",
                Title = "Invoices & History",
                Icon = "📋",
                ContentViewModel = InvoiceHistoryVM,
                CanClose = true
            },
            "Vouchers" or "Receipt" or "Payment" or "Journal" => new DocumentTabItem
            {
                Id = "Vouchers",
                Title = "Financial Vouchers (F5/F6/F7)",
                Icon = "💰",
                ContentViewModel = VoucherVM,
                CanClose = true
            },
            "LedgerReports" or "Ledger" => new DocumentTabItem
            {
                Id = "LedgerReports",
                Title = "Account & Group Ledgers",
                Icon = "📒",
                ContentViewModel = LedgerReportVM,
                CanClose = true
            },
            "ProfitLossReport" or "PLReport" => new DocumentTabItem
            {
                Id = "ProfitLossReport",
                Title = "Profit & Loss (P/L)",
                Icon = "📈",
                ContentViewModel = ProfitLossReportVM,
                CanClose = true
            },
            "UserSecurity" => new DocumentTabItem
            {
                Id = "UserSecurity",
                Title = "User & Security Master",
                Icon = "🔐",
                ContentViewModel = UserSecurityVM,
                CanClose = true
            },
            "Shortcuts" => new DocumentTabItem
            {
                Id = "Shortcuts",
                Title = "Keyboard Shortcuts",
                Icon = "⌨",
                ContentViewModel = ShortcutsVM,
                CanClose = true
            },
            "Settings" => new DocumentTabItem
            {
                Id = "Settings",
                Title = "System Settings",
                Icon = "⚙",
                ContentViewModel = SettingsVM,
                CanClose = true
            },
            _ => new DocumentTabItem
            {
                Id = "Dashboard",
                Title = "Dashboard",
                Icon = "📊",
                ContentViewModel = DashboardVM,
                CanClose = false
            }
        };

        tab.CloseCommand = new RelayCommand(() => CloseTab(tab));
        RefreshTabContent(tab.Id);
        return tab;
    }

    private void RefreshTabContent(string id)
    {
        switch (id)
        {
            case "Dashboard": DashboardVM.LoadData(); break;
            case "StockRegister": StockRegisterVM.LoadData(); break;
            case "ItemCreation": 
                ItemCreationVM.LoadData(); 
                _cachedItems = null;
                break;
            case "HeadGroup": 
                HeadGroupVM.LoadData(); 
                _cachedHeadGroups = null;
                break;
            case "Brand": 
                BrandVM.LoadData(); 
                _cachedBrands = null;
                break;
            case "Godown": GodownVM.LoadData(); break;
            case "AccountCreation": 
                AccountCreationVM.LoadData(); 
                _cachedAccounts = null;
                break;
            case "UserSecurity": UserSecurityVM.LoadData(); break;
            case "Invoices": InvoiceHistoryVM.LoadData(); break;
            case "Vouchers": VoucherVM.LoadData(); break;
            case "LedgerReports": LedgerReportVM.LoadData(); break;
            case "ProfitLossReport": ProfitLossReportVM.LoadData(); break;
            case "Sales": 
                SalesVM.RefreshThreshold();
                SalesVM.ResetForm(); 
                break;
            case "Purchase": PurchaseVM.ResetForm(); break;
            case "Shortcuts": break;
            case "Settings": SettingsVM.LoadSettings(); break;
        }
    }

    public void SelectNextSearchResult()
    {
        if (GlobalSearchResults.Count == 0) return;
        if (SelectedSearchResult == null)
        {
            SelectedSearchResult = GlobalSearchResults[0];
            return;
        }
        var idx = GlobalSearchResults.IndexOf(SelectedSearchResult);
        if (idx < GlobalSearchResults.Count - 1)
        {
            SelectedSearchResult = GlobalSearchResults[idx + 1];
        }
    }

    public void SelectPreviousSearchResult()
    {
        if (GlobalSearchResults.Count == 0) return;
        if (SelectedSearchResult == null)
        {
            SelectedSearchResult = GlobalSearchResults[0];
            return;
        }
        var idx = GlobalSearchResults.IndexOf(SelectedSearchResult);
        if (idx > 0)
        {
            SelectedSearchResult = GlobalSearchResults[idx - 1];
        }
    }

    public void CloseGlobalSearch()
    {
        IsGlobalSearchOpen = false;
    }

    public void OpenGlobalSearchResult(GlobalSearchResultViewModel? result)
    {
        if (result == null) return;

        IsGlobalSearchOpen = false;
        var target = result.NavigationTarget;
        var param = result.NavigationParam;

        switch (target)
        {
            case "AccountCreation":
                OpenTab("AccountCreation");
                if (param is string accName)
                    AccountCreationVM.SelectAccountByName(accName);
                break;

            case "LedgerReports":
                OpenTab("LedgerReports");
                if (param is string ledgerAccName)
                {
                    var acc = LedgerReportVM.AllAccounts.FirstOrDefault(a => a.AccountName.Equals(ledgerAccName, StringComparison.OrdinalIgnoreCase));
                    if (acc != null)
                    {
                        LedgerReportVM.SelectedAccount = acc;
                        LedgerReportVM.IsGroupReportSelected = false;
                    }
                }
                break;

            case "ItemCreation":
                OpenTab("ItemCreation");
                if (param is string itemName)
                    ItemCreationVM.SelectItemByName(itemName);
                break;

            case "StockRegister":
                OpenTab("StockRegister");
                if (param is string stockItemName)
                {
                    StockRegisterVM.SearchItemName = stockItemName;
                }
                break;

            case "Sales":
                OpenTab("Sales");
                if (param is string invNo)
                {
                    var sale = _inventoryService.GetSaleByInvoiceNo(invNo);
                    if (sale != null) SalesVM.LoadBill(sale);
                }
                break;

            case "Purchase":
                OpenTab("Purchase");
                if (param is string purInvNo)
                {
                    var purchase = _inventoryService.GetPurchaseByInvoiceNo(purInvNo);
                    if (purchase != null) PurchaseVM.LoadBill(purchase);
                }
                break;

            case "StockTransfer":
                OpenTab("StockTransfer");
                if (param is string trfNo)
                {
                    var transfer = _inventoryService.GetStockTransferByNo(trfNo);
                    if (transfer != null) StockTransferVM.LoadTransfer(transfer);
                }
                break;

            case "Vouchers":
                OpenTab("Vouchers");
                if (param is string vchNo)
                {
                    var vch = _inventoryService.GetVoucherByVoucherNo(vchNo);
                    if (vch != null) VoucherVM.LoadVoucher(vch);
                }
                break;

            case "Invoices":
                OpenTab("Invoices");
                break;

            case "Brand":
                OpenTab("Brand");
                break;

            case "Godown":
                OpenTab("Godown");
                break;

            case "HeadGroup":
                OpenTab("HeadGroup");
                break;

            case "Shortcuts":
                OpenShortcuts();
                break;

            case "UserSecurity":
                OpenTab("UserSecurity");
                break;

            case "Settings":
                OpenTab("Settings");
                break;

            case "Dashboard":
            default:
                OpenTab("Dashboard");
                break;
        }
    }

    private async Task RefreshGlobalSearchAsync(string text)
    {
        _globalSearchCts?.Cancel();
        _globalSearchCts?.Dispose();
        _globalSearchCts = new CancellationTokenSource();
        var token = _globalSearchCts.Token;

        var query = text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
        {
            GlobalSearchResults.Clear();
            SelectedSearchResult = null;
            IsGlobalSearchOpen = false;
            return;
        }

        IsGlobalSearchOpen = true;

        try
        {
            await Task.Delay(160, token);
            if (token.IsCancellationRequested) return;

            // Ensure caches
            _cachedAccounts ??= _inventoryService.GetAllAccounts();
            _cachedItems ??= _inventoryService.GetAllItems();
            _cachedBrands ??= _inventoryService.GetAllBrands();
            _cachedHeadGroups ??= _inventoryService.GetAllHeadGroups();

            var brandMap = _cachedBrands.ToDictionary(b => b.Id, b => b.Name);
            var hgMap = _cachedHeadGroups.ToDictionary(h => h.Id, h => h.Name);

            var results = new List<GlobalSearchResultViewModel>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Accounts
            foreach (var acc in OrderByMatchQuality(_cachedAccounts, query, a => new[] { a.AccountName, a.Mobile, a.GSTIN, a.AccountType }))
            {
                var qDetails = new AccountQuickDetails
                {
                    AccountName = acc.AccountName,
                    AccountType = acc.AccountType,
                    Mobile = acc.Mobile,
                    Email = acc.Email,
                    GSTIN = acc.GSTIN,
                    Address = acc.Address,
                    OpeningBalance = acc.OpeningBalance,
                    OpeningBalanceType = string.IsNullOrWhiteSpace(acc.OpeningBalanceType) ? "Dr" : acc.OpeningBalanceType
                };

                AddResult(results, seen, new GlobalSearchResultViewModel
                {
                    Title = "Party Account",
                    Detail = $"{acc.AccountName} ({acc.AccountType})",
                    Icon = "👥",
                    Badge = "ACCOUNT",
                    BadgeBrushHex = "#2563EB",
                    NavigationTarget = "AccountCreation",
                    NavigationParam = acc.AccountName,
                    AccountDetails = qDetails
                });

                AddResult(results, seen, new GlobalSearchResultViewModel
                {
                    Title = "Ledger Statement",
                    Detail = $"{acc.AccountName} (Ledger & Statement)",
                    Icon = "📒",
                    Badge = "LEDGER",
                    BadgeBrushHex = "#7C3AED",
                    NavigationTarget = "LedgerReports",
                    NavigationParam = acc.AccountName,
                    AccountDetails = qDetails
                });
            }

            // 2. Items
            foreach (var item in OrderByMatchQuality(_cachedItems, query, i => new[] { i.ItemName, i.ItemCode }))
            {
                brandMap.TryGetValue(item.BrandId, out var bName);
                hgMap.TryGetValue(item.HeadGroupId, out var hName);

                var itemDetails = new ItemQuickDetails
                {
                    ItemName = item.ItemName,
                    ItemCode = item.ItemCode,
                    BrandName = bName ?? item.BrandName,
                    HeadGroupName = hName ?? item.HeadGroupName,
                    DefaultRatePerKg = item.DefaultRatePerKg,
                    Description = item.Description
                };

                AddResult(results, seen, new GlobalSearchResultViewModel
                {
                    Title = "Item Master",
                    Detail = $"{item.ItemName} • ₹{item.DefaultRatePerKg:N2}/kg",
                    Icon = "🏷",
                    Badge = "ITEM",
                    BadgeBrushHex = "#0D9488",
                    NavigationTarget = "ItemCreation",
                    NavigationParam = item.ItemName,
                    ItemDetails = itemDetails
                });

                AddResult(results, seen, new GlobalSearchResultViewModel
                {
                    Title = "Live Stock Register",
                    Detail = $"{item.ItemName} (Live Godown Balance)",
                    Icon = "📦",
                    Badge = "STOCK",
                    BadgeBrushHex = "#059669",
                    NavigationTarget = "StockRegister",
                    NavigationParam = item.ItemName,
                    ItemDetails = itemDetails
                });
            }

            // 3. Recent Sales & Purchases (by InvoiceNo or CustomerName / SupplierName)
            var allSales = _inventoryService.GetAllSales();
            foreach (var s in OrderByMatchQuality(allSales, query, s => new[] { s.InvoiceNo, s.CustomerName }))
            {
                AddResult(results, seen, new GlobalSearchResultViewModel
                {
                    Title = "Sales Bill",
                    Detail = $"{s.InvoiceNo} • {s.CustomerName} • ₹{s.GrandTotal:N2}",
                    Icon = "🛒",
                    Badge = "SALE",
                    BadgeBrushHex = "#2563EB",
                    NavigationTarget = "Sales",
                    NavigationParam = s.InvoiceNo
                });
            }

            var allPurchases = _inventoryService.GetAllPurchases();
            foreach (var p in OrderByMatchQuality(allPurchases, query, p => new[] { p.InvoiceNo, p.SupplierName }))
            {
                AddResult(results, seen, new GlobalSearchResultViewModel
                {
                    Title = "Purchase Inward",
                    Detail = $"{p.InvoiceNo} • {p.SupplierName} • ₹{p.GrandTotal:N2}",
                    Icon = "📥",
                    Badge = "PURCHASE",
                    BadgeBrushHex = "#059669",
                    NavigationTarget = "Purchase",
                    NavigationParam = p.InvoiceNo
                });
            }

            // 4. Financial Vouchers
            var allVouchers = _inventoryService.GetAllVouchers();
            foreach (var v in OrderByMatchQuality(allVouchers, query, v => new[] { v.VoucherNo, v.PrimaryAccountName, v.DebitAccountName, v.CreditAccountName, v.VoucherType, v.Narration }))
            {
                var party = !string.IsNullOrWhiteSpace(v.PrimaryAccountName) ? v.PrimaryAccountName : (!string.IsNullOrWhiteSpace(v.CreditAccountName) ? v.CreditAccountName : v.DebitAccountName);
                AddResult(results, seen, new GlobalSearchResultViewModel
                {
                    Title = $"{v.VoucherType} Voucher",
                    Detail = $"{v.VoucherNo} • {party} • ₹{v.Amount:N2}",
                    Icon = "💰",
                    Badge = "VOUCHER",
                    BadgeBrushHex = "#D97706",
                    NavigationTarget = "Vouchers",
                    NavigationParam = v.VoucherNo
                });
            }

            // 5. Stock Transfers
            var allTransfers = _inventoryService.GetAllStockTransfers();
            foreach (var t in OrderByMatchQuality(allTransfers, query, t => new[] { t.TransferNo, t.FromGodownName, t.ToGodownName, t.Notes }))
            {
                AddResult(results, seen, new GlobalSearchResultViewModel
                {
                    Title = "Stock Transfer",
                    Detail = $"{t.TransferNo} • {t.FromGodownName} ➔ {t.ToGodownName} • {t.TotalBags} Bags",
                    Icon = "🚚",
                    Badge = "TRANSFER",
                    BadgeBrushHex = "#D97706",
                    NavigationTarget = "StockTransfer",
                    NavigationParam = t.TransferNo
                });
            }

            // 6. Navigation / Menus
            var menuItems = GetAppNavigationItems();
            foreach (var m in OrderByMatchQuality(menuItems, query, m => new[] { m.Title, m.Detail }))
            {
                AddResult(results, seen, m);
            }

            if (token.IsCancellationRequested) return;

            // Filter out items the current user does not have permission to access
            results.RemoveAll(r => !string.IsNullOrEmpty(r.NavigationTarget) && !Security.CanAccessTab(r.NavigationTarget));

            GlobalSearchResults.Clear();
            foreach (var item in results.Take(12))
            {
                GlobalSearchResults.Add(item);
            }

            if (GlobalSearchResults.Count > 0)
            {
                SelectedSearchResult = GlobalSearchResults[0];
                IsGlobalSearchOpen = true;
            }
            else
            {
                SelectedSearchResult = null;
                IsGlobalSearchOpen = false;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Search error: {ex.Message}");
            GlobalSearchResults.Clear();
            SelectedSearchResult = null;
        }
    }

    private List<GlobalSearchResultViewModel> GetAppNavigationItems() => new()
    {
        new GlobalSearchResultViewModel { Title = "Sales Entry (Dual-Qty)", Detail = "Dual-unit sales invoice with automatic bag & loose kg cut", Icon = "🛒", Badge = "MENU", BadgeBrushHex = "#64748B", NavigationTarget = "Sales" },
        new GlobalSearchResultViewModel { Title = "Purchase Inward", Detail = "Raw polymer bag inwards, multi-godown stock receipt", Icon = "📥", Badge = "MENU", BadgeBrushHex = "#64748B", NavigationTarget = "Purchase" },
        new GlobalSearchResultViewModel { Title = "Stock Transfer Between Godowns", Detail = "Transfer bags and stock between godowns with challan", Icon = "🚚", Badge = "MENU", BadgeBrushHex = "#64748B", NavigationTarget = "StockTransfer" },
        new GlobalSearchResultViewModel { Title = "Payment Voucher (F5)", Detail = "Vendor & expense payment vouchers", Icon = "💰", Badge = "MENU", BadgeBrushHex = "#64748B", NavigationTarget = "Vouchers" },
        new GlobalSearchResultViewModel { Title = "Receipt Voucher (F6)", Detail = "Customer payment collections & bank receipts", Icon = "💰", Badge = "MENU", BadgeBrushHex = "#64748B", NavigationTarget = "Vouchers" },
        new GlobalSearchResultViewModel { Title = "Journal Voucher (F7)", Detail = "Double-entry adjustments and ledger contra entries", Icon = "💰", Badge = "MENU", BadgeBrushHex = "#64748B", NavigationTarget = "Vouchers" },
        new GlobalSearchResultViewModel { Title = "Live Stock Register", Detail = "Real-time dual-unit bag and kg balances across godowns", Icon = "📦", Badge = "MENU", BadgeBrushHex = "#64748B", NavigationTarget = "StockRegister" },
        new GlobalSearchResultViewModel { Title = "Invoices & History", Detail = "Sales bill & purchase inward archive and reprint", Icon = "📋", Badge = "MENU", BadgeBrushHex = "#64748B", NavigationTarget = "Invoices" },
        new GlobalSearchResultViewModel { Title = "Account & Group Ledgers", Detail = "Customer/supplier statements & Tally-style group summaries", Icon = "📒", Badge = "MENU", BadgeBrushHex = "#64748B", NavigationTarget = "LedgerReports" },
        new GlobalSearchResultViewModel { Title = "Item Master", Detail = "Polymer product catalog, standard bag weights & default rates", Icon = "🏷", Badge = "MENU", BadgeBrushHex = "#64748B", NavigationTarget = "ItemCreation" },
        new GlobalSearchResultViewModel { Title = "Brand Master", Detail = "Polymer manufacturer brand registry (e.g. Reliance, IOCL, Balaji)", Icon = "🏢", Badge = "MENU", BadgeBrushHex = "#64748B", NavigationTarget = "Brand" },
        new GlobalSearchResultViewModel { Title = "Godown Master", Detail = "Warehouse locations and godown stock storage setup", Icon = "🏭", Badge = "MENU", BadgeBrushHex = "#64748B", NavigationTarget = "Godown" },
        new GlobalSearchResultViewModel { Title = "Head Group Master", Detail = "Category classification for raw materials and finished goods", Icon = "📁", Badge = "MENU", BadgeBrushHex = "#64748B", NavigationTarget = "HeadGroup" },
        new GlobalSearchResultViewModel { Title = "User & Security Roles", Detail = "User credentials, access controls and permissions matrix", Icon = "🔐", Badge = "MENU", BadgeBrushHex = "#64748B", NavigationTarget = "UserSecurity" },
        new GlobalSearchResultViewModel { Title = "System Settings", Detail = "Configure sales inventory deduction threshold and global preferences", Icon = "⚙", Badge = "MENU", BadgeBrushHex = "#64748B", NavigationTarget = "Settings" },
        new GlobalSearchResultViewModel { Title = "Dashboard Overview", Detail = "Executive KPIs, stock valuation & live search", Icon = "📊", Badge = "MENU", BadgeBrushHex = "#64748B", NavigationTarget = "Dashboard" },
        new GlobalSearchResultViewModel { Title = "Keyboard Shortcuts (F1)", Detail = "Full high-speed accounting and billing hotkeys cheatsheet", Icon = "⌨", Badge = "MENU", BadgeBrushHex = "#64748B", NavigationTarget = "Shortcuts" }
    };

    private static IEnumerable<T> OrderByMatchQuality<T>(IEnumerable<T> source, string searchText, Func<T, IEnumerable<string?>> valueSelector)
    {
        return source
            .Select(item => new
            {
                Item = item,
                Score = GetBestScore(valueSelector(item), searchText)
            })
            .Where(x => x.Score < int.MaxValue)
            .OrderBy(x => x.Score)
            .Select(x => x.Item);
    }

    private static int GetBestScore(IEnumerable<string?> values, string searchText)
    {
        int best = int.MaxValue;
        foreach (var val in values)
        {
            if (string.IsNullOrWhiteSpace(val)) continue;
            var s = val.Trim();
            if (s.Equals(searchText, StringComparison.OrdinalIgnoreCase)) return 0;
            if (s.StartsWith(searchText, StringComparison.OrdinalIgnoreCase))
            {
                if (best > 1) best = 1;
            }
            else if (s.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            {
                if (best > 2) best = 2;
            }
        }
        return best;
    }

    private static void AddResult(ICollection<GlobalSearchResultViewModel> list, ISet<string> seen, GlobalSearchResultViewModel item)
    {
        var key = $"{item.NavigationTarget}|{item.Title}|{item.Detail}|{item.NavigationParam}";
        if (seen.Add(key))
        {
            list.Add(item);
        }
    }
}
