using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using PolyStock.Core.Common;

namespace PolyStock.WPF.ViewModels;

public class ShortcutItem
{
    public string Key { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string CategoryTitle { get; set; } = string.Empty;
    public string PageId { get; set; } = string.Empty;
    public bool CanNavigate => !string.IsNullOrEmpty(PageId) && !PageId.Equals("Global", StringComparison.OrdinalIgnoreCase);
}

public class ShortcutsViewModel : ObservableObject
{
    private readonly List<ShortcutItem> _allShortcuts;
    private string _searchText = string.Empty;
    private string _selectedCategory = "All";
    private readonly Action<string>? _openTabAction;

    public ShortcutsViewModel(Action<string>? openTabAction = null)
    {
        _openTabAction = openTabAction;
        _allShortcuts = InitializeShortcuts();

        Categories = new ObservableCollection<string>
        {
            "All",
            "Sales",
            "Purchase",
            "Vouchers",
            "Ledgers",
            "Stock",
            "Invoices",
            "Masters",
            "Global"
        };

        FilteredShortcuts = new ObservableCollection<ShortcutItem>(_allShortcuts);

        SelectCategoryCommand = new RelayCommand(param =>
        {
            if (param is string cat)
            {
                SelectedCategory = cat;
            }
        });

        NavigateToPageCommand = new RelayCommand(param =>
        {
            if (param is string pageId && !string.IsNullOrEmpty(pageId))
            {
                _openTabAction?.Invoke(pageId);
            }
        });

        ClearSearchCommand = new RelayCommand(() =>
        {
            SearchText = string.Empty;
        });
    }

    public ObservableCollection<string> Categories { get; }
    public ObservableCollection<ShortcutItem> FilteredShortcuts { get; }

    public ICommand SelectCategoryCommand { get; }
    public ICommand NavigateToPageCommand { get; }
    public ICommand ClearSearchCommand { get; }

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

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                ApplyFilter();
            }
        }
    }

    public void SetContextCategory(string viewId)
    {
        string targetCat = viewId switch
        {
            "Sales" => "Sales",
            "Purchase" => "Purchase",
            "Vouchers" or "Receipt" or "Payment" or "Journal" => "Vouchers",
            "LedgerReports" or "Ledger" => "Ledgers",
            "StockRegister" => "Stock",
            "Invoices" => "Invoices",
            "ItemCreation" or "Brand" or "Godown" or "AccountCreation" or "HeadGroup" => "Masters",
            _ => "All"
        };

        SelectedCategory = targetCat;
    }

    private void ApplyFilter()
    {
        var filtered = _allShortcuts.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(_selectedCategory) && !_selectedCategory.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(s => s.Category.Equals(_selectedCategory, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            string query = _searchText.Trim();
            filtered = filtered.Where(s =>
                s.Key.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.Action.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.CategoryTitle.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        FilteredShortcuts.Clear();
        foreach (var item in filtered)
        {
            FilteredShortcuts.Add(item);
        }
    }

    private static List<ShortcutItem> InitializeShortcuts()
    {
        return new List<ShortcutItem>
        {
            // 1. Global Navigation & Window Management
            new() { Key = "F1", Action = "Keyboard Shortcuts Cheatsheet", Description = "Opens this comprehensive shortcut reference guide from any screen.", Category = "Global", CategoryTitle = "🌐 Global & Navigation", PageId = "Shortcuts" },
            new() { Key = "Ctrl + W", Action = "Close Current Document Tab", Description = "Closes active tab and safely restores previous workspace view.", Category = "Global", CategoryTitle = "🌐 Global & Navigation", PageId = "Global" },
            new() { Key = "F5", Action = "Open Payment Voucher", Description = "Instantly opens or switches to the Financial Vouchers tab in Payment mode.", Category = "Global", CategoryTitle = "🌐 Global & Navigation", PageId = "Vouchers" },
            new() { Key = "F6", Action = "Open Receipt Voucher", Description = "Instantly opens or switches to the Financial Vouchers tab in Receipt mode.", Category = "Global", CategoryTitle = "🌐 Global & Navigation", PageId = "Vouchers" },
            new() { Key = "F7", Action = "Open Journal Voucher", Description = "Instantly opens or switches to the Financial Vouchers tab in Journal mode.", Category = "Global", CategoryTitle = "🌐 Global & Navigation", PageId = "Vouchers" },

            // 2. Sales Entry
            new() { Key = "F8", Action = "Save Sales Invoice", Description = "Validates line items, commits focused field, and saves bill with auto dual-quantity stock reduction.", Category = "Sales", CategoryTitle = "🛒 Sales Entry (Dual-Qty)", PageId = "Sales" },
            new() { Key = "Ctrl + A / Ctrl + S", Action = "Quick Save Invoice", Description = "Rapid keyboard accelerator to commit changes and post the sale invoice.", Category = "Sales", CategoryTitle = "🛒 Sales Entry (Dual-Qty)", PageId = "Sales" },
            new() { Key = "F9", Action = "Print Thermal / A4 Invoice", Description = "Saves and triggers clean formatted printout of currently active sale bill.", Category = "Sales", CategoryTitle = "🛒 Sales Entry (Dual-Qty)", PageId = "Sales" },
            new() { Key = "Ctrl + P", Action = "Print Invoice Document", Description = "Standard Windows print shortcut to open print dialog.", Category = "Sales", CategoryTitle = "🛒 Sales Entry (Dual-Qty)", PageId = "Sales" },
            new() { Key = "Ctrl + N", Action = "New Blank Sales Bill", Description = "Clears the active entry form and prepares a new consecutive invoice number.", Category = "Sales", CategoryTitle = "🛒 Sales Entry (Dual-Qty)", PageId = "Sales" },
            new() { Key = "Ctrl + L", Action = "Open Sales History", Description = "Jumps directly to the Invoices & History register filtered for Sales.", Category = "Sales", CategoryTitle = "🛒 Sales Entry (Dual-Qty)", PageId = "Invoices" },
            new() { Key = "F2", Action = "Focus Invoice Date", Description = "Selects date picker field for quick date modification.", Category = "Sales", CategoryTitle = "🛒 Sales Entry (Dual-Qty)", PageId = "Sales" },
            new() { Key = "F3", Action = "Focus Customer / Party Combo", Description = "Jumps focus to the customer selector and opens auto-complete search.", Category = "Sales", CategoryTitle = "🛒 Sales Entry (Dual-Qty)", PageId = "Sales" },
            new() { Key = "F4", Action = "Focus First Item Line", Description = "Places cursor directly inside the first item selection row in the grid.", Category = "Sales", CategoryTitle = "🛒 Sales Entry (Dual-Qty)", PageId = "Sales" },
            new() { Key = "F7", Action = "Focus GST Tax Rate", Description = "Jumps cursor down to the GST tax rate box for rapid tax adjustment.", Category = "Sales", CategoryTitle = "🛒 Sales Entry (Dual-Qty)", PageId = "Sales" },
            new() { Key = "Enter", Action = "Next Field & Rapid Row Addition", Description = "Navigates sequentially: Customer ➔ Godown ➔ Item ➔ Bags ➔ Rate ➔ adds line automatically.", Category = "Sales", CategoryTitle = "🛒 Sales Entry (Dual-Qty)", PageId = "Sales" },
            new() { Key = "Shift + Enter", Action = "Focus Loose kg Field", Description = "Quick jump to enter broken bag loose kilogram quantities.", Category = "Sales", CategoryTitle = "🛒 Sales Entry (Dual-Qty)", PageId = "Sales" },
            new() { Key = "Ctrl + Del", Action = "Delete Selected Line Item", Description = "Removes the currently highlighted product row from invoice and recalculates totals.", Category = "Sales", CategoryTitle = "🛒 Sales Entry (Dual-Qty)", PageId = "Sales" },
            new() { Key = "Esc", Action = "Cancel / Clear Focused Row", Description = "Aborts line edit or clears current input selection.", Category = "Sales", CategoryTitle = "🛒 Sales Entry (Dual-Qty)", PageId = "Sales" },

            // 3. Purchase Inward
            new() { Key = "Ctrl + A / Ctrl + S", Action = "Save Purchase Inward", Description = "Validates items, adds dual-unit bags and weight to inventory, and records supplier liability.", Category = "Purchase", CategoryTitle = "📥 Purchase Inward", PageId = "Purchase" },
            new() { Key = "Ctrl + N", Action = "New Purchase Voucher", Description = "Resets inward form for next incoming shipment.", Category = "Purchase", CategoryTitle = "📥 Purchase Inward", PageId = "Purchase" },
            new() { Key = "Ctrl + P", Action = "Print Inward Sheet", Description = "Prints inward goods voucher and delivery verification slip.", Category = "Purchase", CategoryTitle = "📥 Purchase Inward", PageId = "Purchase" },
            new() { Key = "Ctrl + L", Action = "Open Purchase History", Description = "Jumps to Invoices & History tab filtered for Purchase vouchers.", Category = "Purchase", CategoryTitle = "📥 Purchase Inward", PageId = "Invoices" },
            new() { Key = "F2", Action = "Focus Purchase Date", Description = "Places cursor in the inward date field.", Category = "Purchase", CategoryTitle = "📥 Purchase Inward", PageId = "Purchase" },
            new() { Key = "Enter", Action = "Rapid Line Navigation", Description = "Fast flow: Supplier ➔ Godown ➔ Item ➔ Pack Size ➔ Bags ➔ Rate ➔ Auto-row.", Category = "Purchase", CategoryTitle = "📥 Purchase Inward", PageId = "Purchase" },
            new() { Key = "Ctrl + Del", Action = "Delete Inward Line Item", Description = "Removes the selected product inward row from the grid.", Category = "Purchase", CategoryTitle = "📥 Purchase Inward", PageId = "Purchase" },

            // 4. Financial Vouchers
            new() { Key = "F5", Action = "Switch to Payment Mode", Description = "Toggles voucher mode to Cash/Bank Payment and filters account debits.", Category = "Vouchers", CategoryTitle = "💰 Financial Vouchers (F5/F6/F7)", PageId = "Vouchers" },
            new() { Key = "F6", Action = "Switch to Receipt Mode", Description = "Toggles voucher mode to Customer/Bank Receipt and filters account credits.", Category = "Vouchers", CategoryTitle = "💰 Financial Vouchers (F5/F6/F7)", PageId = "Vouchers" },
            new() { Key = "F7", Action = "Switch to Journal Mode", Description = "Toggles voucher mode to General Journal adjustment voucher.", Category = "Vouchers", CategoryTitle = "💰 Financial Vouchers (F5/F6/F7)", PageId = "Vouchers" },
            new() { Key = "Ctrl + A / Ctrl + S", Action = "Save Voucher (Tally Style)", Description = "Validates balanced debit and credit totals and posts journal entries.", Category = "Vouchers", CategoryTitle = "💰 Financial Vouchers (F5/F6/F7)", PageId = "Vouchers" },
            new() { Key = "Ctrl + N", Action = "New Blank Voucher", Description = "Clears entries and fetches next consecutive voucher serial number.", Category = "Vouchers", CategoryTitle = "💰 Financial Vouchers (F5/F6/F7)", PageId = "Vouchers" },
            new() { Key = "Ctrl + P", Action = "Print Voucher Slip", Description = "Generates receipt/payment confirmation document.", Category = "Vouchers", CategoryTitle = "💰 Financial Vouchers (F5/F6/F7)", PageId = "Vouchers" },
            new() { Key = "Ctrl + D", Action = "Delete Active Voucher", Description = "Deletes currently loaded financial voucher after user confirmation.", Category = "Vouchers", CategoryTitle = "💰 Financial Vouchers (F5/F6/F7)", PageId = "Vouchers" },
            new() { Key = "Enter", Action = "Quick Field Advance & Save", Description = "Enter advances through line items. Pressing Enter in Narration saves the voucher.", Category = "Vouchers", CategoryTitle = "💰 Financial Vouchers (F5/F6/F7)", PageId = "Vouchers" },
            new() { Key = "Ctrl + Del", Action = "Delete Voucher Row", Description = "Removes selected debit/credit account line from voucher.", Category = "Vouchers", CategoryTitle = "💰 Financial Vouchers (F5/F6/F7)", PageId = "Vouchers" },

            // 5. Account & Group Ledgers
            new() { Key = "Enter", Action = "Drill-Down to Ledger / Bill", Description = "In Group Summary: drills into Account Ledger. In Ledger Statement: opens original Sales/Purchase/Voucher bill.", Category = "Ledgers", CategoryTitle = "📒 Account & Group Ledgers", PageId = "LedgerReports" },
            new() { Key = "Esc", Action = "Back to Group Summary (Tally ERP Style)", Description = "Returns from individual statement view back to top group totals.", Category = "Ledgers", CategoryTitle = "📒 Account & Group Ledgers", PageId = "LedgerReports" },

            // 6. Live Stock Register
            new() { Key = "Filter / Search", Action = "Instant Stock Search", Description = "Live search by polymer grade, item code, brand, or godown location.", Category = "Stock", CategoryTitle = "📦 Live Stock Register", PageId = "StockRegister" },
            new() { Key = "Auto-Calculate", Action = "Dual Unit Conversion", Description = "Instantly displays bags, loose kilograms, total kilograms, and valuation.", Category = "Stock", CategoryTitle = "📦 Live Stock Register", PageId = "StockRegister" },

            // 7. Invoices & History
            new() { Key = "Double-Click", Action = "Load & View Document", Description = "Opens selected invoice directly in Sales or Purchase view for modification or reprint.", Category = "Invoices", CategoryTitle = "📋 Invoices & History", PageId = "Invoices" },

            // 8. Master Data Entry
            new() { Key = "Enter / Tab", Action = "Move to Next Input", Description = "Sequentially focuses next input field for high-speed single-handed master creation.", Category = "Masters", CategoryTitle = "🏷 Master Data Creation", PageId = "ItemCreation" },
            new() { Key = "Save Button", Action = "Commit Master Record", Description = "Saves Item, Account, Brand, Godown, or Head Group to MS Access database.", Category = "Masters", CategoryTitle = "🏷 Master Data Creation", PageId = "ItemCreation" }
        };
    }
}
