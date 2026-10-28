using System;

namespace PolyStock.Core.Models;

public class Account
{
    public int Id { get; set; }
    public string AccountType { get; set; } = "Customer"; // Customer, Supplier, Transporter, Broker, General
    public string AccountName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public string? GSTIN { get; set; }
    public double OpeningBalance { get; set; } = 0.0;
    public string OpeningBalanceType { get; set; } = "+"; // + (To Receive / Receivable) or - (To Pay / Payable)
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public double CurrentBalance { get; set; } = 0.0;
    public double TotalDebit { get; set; } = 0.0;
    public double TotalCredit { get; set; } = 0.0;

    // Period specific reporting (e.g. within selected date range)
    public double PeriodOpeningBalance { get; set; } = 0.0;
    public string PeriodOpeningBalanceType { get; set; } = "+";
    public double PeriodDebit { get; set; } = 0.0;
    public double PeriodCredit { get; set; } = 0.0;
    public double PeriodClosingBalance { get; set; } = 0.0;
    public string PeriodClosingBalanceType => PeriodClosingBalance >= 0 ? "+" : "-";

    public string FormattedOpeningBalance =>
        OpeningBalance > 0.0001
            ? $"{(OpeningBalanceType == "-" || OpeningBalanceType == "Cr" ? "-" : "+")} ₹ {OpeningBalance:N2}"
            : "₹ 0.00";

    public string FormattedCurrentBalance =>
        Math.Abs(CurrentBalance) > 0.0001
            ? $"{(CurrentBalance >= 0 ? "+" : "-")} ₹ {Math.Abs(CurrentBalance):N2}"
            : "₹ 0.00";

    public string FormattedPeriodDebit => PeriodDebit > 0.0001 ? $"+ ₹ {PeriodDebit:N2}" : "-";
    public string FormattedPeriodCredit => PeriodCredit > 0.0001 ? $"- ₹ {PeriodCredit:N2}" : "-";
    public string FormattedPeriodClosing => Math.Abs(PeriodClosingBalance) > 0.0001
        ? $"{(PeriodClosingBalance >= 0 ? "+" : "-")} ₹ {Math.Abs(PeriodClosingBalance):N2}"
        : "₹ 0.00";

    public string BalanceStatus =>
        Math.Abs(CurrentBalance) < 0.0001 ? "Settled" :
        CurrentBalance > 0 ? "Receivable (+)" : "Payable (-)";

    public override string ToString() => $"[{AccountType}] {AccountName}";
}
