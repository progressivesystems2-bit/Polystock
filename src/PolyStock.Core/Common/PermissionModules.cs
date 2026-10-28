using System;
using System.Collections.Generic;
using System.Linq;

namespace PolyStock.Core.Common;

public enum PermissionAction
{
    View,
    Create,
    Edit,
    Delete,
    Print
}

public class ModuleDefinition
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // Masters, Entries, Reports, Security
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "📄";
    public string TabViewId { get; set; } = string.Empty;
}

public static class PermissionModules
{
    public const string CategoryMasters = "Master Data";
    public const string CategoryEntries = "Transactions & Entries";
    public const string CategoryReports = "Reports & Registers";
    public const string CategorySecurity = "Security Administration";

    // Masters
    public const string ItemMaster = "ItemMaster";
    public const string HeadGroupMaster = "HeadGroupMaster";
    public const string BrandMaster = "BrandMaster";
    public const string GodownMaster = "GodownMaster";
    public const string AccountMaster = "AccountMaster";

    // Entries
    public const string SalesEntry = "SalesEntry";
    public const string PurchaseEntry = "PurchaseEntry";
    public const string StockTransferEntry = "StockTransferEntry";
    public const string VoucherEntry = "VoucherEntry";

    // Reports
    public const string DashboardReport = "DashboardReport";
    public const string StockRegisterReport = "StockRegisterReport";
    public const string InvoiceHistoryReport = "InvoiceHistoryReport";
    public const string LedgerReport = "LedgerReport";
    public const string ProfitLossReport = "ProfitLossReport";

    // Security
    public const string UserSecurity = "UserSecurity";
    public const string SystemSettings = "SystemSettings";

    public static readonly IReadOnlyList<ModuleDefinition> AllModules = new List<ModuleDefinition>
    {
        // Masters
        new ModuleDefinition { Key = ItemMaster, DisplayName = "Item Master", Category = CategoryMasters, Description = "Create and manage items, bag sizes, and default rates", Icon = "🏷", TabViewId = "ItemCreation" },
        new ModuleDefinition { Key = HeadGroupMaster, DisplayName = "Head Group Master", Category = CategoryMasters, Description = "Manage polymer item categories & groups", Icon = "📁", TabViewId = "HeadGroup" },
        new ModuleDefinition { Key = BrandMaster, DisplayName = "Brand Master", Category = CategoryMasters, Description = "Manage manufacturers and brands", Icon = "🏢", TabViewId = "Brand" },
        new ModuleDefinition { Key = GodownMaster, DisplayName = "Godown Master", Category = CategoryMasters, Description = "Manage storage warehouses and godowns", Icon = "🏭", TabViewId = "Godown" },
        new ModuleDefinition { Key = AccountMaster, DisplayName = "Account Master", Category = CategoryMasters, Description = "Manage customer and supplier ledger accounts", Icon = "👥", TabViewId = "AccountCreation" },

        // Entries
        new ModuleDefinition { Key = SalesEntry, DisplayName = "Sales Entry", Category = CategoryEntries, Description = "Dual-quantity sales invoicing with loose kg calculations", Icon = "🛒", TabViewId = "Sales" },
        new ModuleDefinition { Key = PurchaseEntry, DisplayName = "Purchase Inward", Category = CategoryEntries, Description = "Record inbound vendor bags and freight adjustments", Icon = "📥", TabViewId = "Purchase" },
        new ModuleDefinition { Key = StockTransferEntry, DisplayName = "Stock Transfer", Category = CategoryEntries, Description = "Inter-godown stock transfers and delivery challans", Icon = "🚚", TabViewId = "StockTransfer" },
        new ModuleDefinition { Key = VoucherEntry, DisplayName = "Financial Vouchers", Category = CategoryEntries, Description = "Payments (F5), Receipts (F6), and Journals (F7)", Icon = "💰", TabViewId = "Vouchers" },

        // Reports
        new ModuleDefinition { Key = DashboardReport, DisplayName = "Dashboard Overview", Category = CategoryReports, Description = "Executive summary, valuation, and sales metrics", Icon = "📊", TabViewId = "Dashboard" },
        new ModuleDefinition { Key = StockRegisterReport, DisplayName = "Live Stock Register", Category = CategoryReports, Description = "Real-time godown balances and stock ledger logs", Icon = "📦", TabViewId = "StockRegister" },
        new ModuleDefinition { Key = InvoiceHistoryReport, DisplayName = "Invoices & History", Category = CategoryReports, Description = "Historical sales & purchase registry with reprinting", Icon = "📋", TabViewId = "Invoices" },
        new ModuleDefinition { Key = LedgerReport, DisplayName = "Account & Group Ledgers", Category = CategoryReports, Description = "Party statements and Tally-style summary ledgers", Icon = "📒", TabViewId = "LedgerReports" },
        new ModuleDefinition { Key = ProfitLossReport, DisplayName = "Profit & Loss (P/L) Report", Category = CategoryReports, Description = "Weighted average purchase & sales profit analysis", Icon = "📈", TabViewId = "ProfitLossReport" },

        // Security & Settings
        new ModuleDefinition { Key = UserSecurity, DisplayName = "User & Security Roles", Category = CategorySecurity, Description = "Configure user credentials, roles, and permissions", Icon = "🔐", TabViewId = "UserSecurity" },
        new ModuleDefinition { Key = SystemSettings, DisplayName = "System Settings", Category = CategorySecurity, Description = "Configure sales threshold and inventory deduction rules", Icon = "⚙", TabViewId = "Settings" }
    };

    private static readonly Dictionary<string, string> TabToModuleMap = AllModules
        .ToDictionary(m => m.TabViewId, m => m.Key, StringComparer.OrdinalIgnoreCase);

    public static string? GetModuleKeyForTab(string tabViewId)
    {
        if (TabToModuleMap.TryGetValue(tabViewId, out var moduleKey))
        {
            return moduleKey;
        }

        // Aliases
        return tabViewId.ToLowerInvariant() switch
        {
            "receipt" or "payment" or "journal" => VoucherEntry,
            "ledger" => LedgerReport,
            "plreport" or "profitloss" or "profitlossreport" => ProfitLossReport,
            _ => null
        };
    }
}
