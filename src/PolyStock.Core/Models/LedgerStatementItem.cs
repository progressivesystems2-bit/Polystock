using System;

namespace PolyStock.Core.Models;

public class LedgerStatementItem
{
    public DateTime Date { get; set; }
    public string TransactionType { get; set; } = string.Empty; // OPENING, SALE, PURCHASE, RECEIPT, PAYMENT, JOURNAL
    public string RefNo { get; set; } = string.Empty;
    public string Particulars { get; set; } = string.Empty;
    public double Debit { get; set; }
    public double Credit { get; set; }
    public double RunningBalance { get; set; }
    public string RunningBalanceType { get; set; } = "+"; // + or -
    public string? Narration { get; set; }

    public string FormattedDebit => Debit > 0.0001 ? $"+ ₹ {Debit:N2}" : "-";
    public string FormattedCredit => Credit > 0.0001 ? $"- ₹ {Credit:N2}" : "-";
    public string FormattedRunningBalance => Math.Abs(RunningBalance) > 0.0001
        ? $"{(RunningBalance >= 0 ? "+" : "-")} ₹ {Math.Abs(RunningBalance):N2}"
        : "₹ 0.00";

    public string TypeBadgeColor => TransactionType switch
    {
        "OPENING" => "#6B7280",
        "SALE" => "#2563EB",
        "PURCHASE" => "#059669",
        "RECEIPT" => "#10B981",
        "PAYMENT" => "#EF4444",
        "JOURNAL" => "#8B5CF6",
        _ => "#4B5563"
    };
}
