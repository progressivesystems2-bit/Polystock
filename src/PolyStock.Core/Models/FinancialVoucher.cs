using System;
using System.Collections.Generic;
using System.Linq;

namespace PolyStock.Core.Models;

public class FinancialVoucher
{
    public int Id { get; set; }
    public string VoucherType { get; set; } = "RECEIPT"; // RECEIPT, PAYMENT, JOURNAL
    public string VoucherNo { get; set; } = string.Empty;
    public DateTime VoucherDate { get; set; } = DateTime.Today;

    // Single-entry primary account (Cash or Bank account for Receipts/Payments)
    public int? PrimaryAccountId { get; set; }
    public string PrimaryAccountName { get; set; } = string.Empty;

    // Legacy / 1-to-1 helper mapping
    public int? DebitAccountId { get; set; }
    public string DebitAccountName { get; set; } = string.Empty;

    public int? CreditAccountId { get; set; }
    public string CreditAccountName { get; set; } = string.Empty;

    private double _amount;
    public double Amount
    {
        get => TotalAmount > 0 ? TotalAmount : _amount;
        set
        {
            _amount = value;
            TotalAmount = value;
            if (Items != null && Items.Count == 1)
            {
                Items[0].Amount = value;
            }
        }
    }

    public double TotalAmount { get; set; }

    public string PaymentMode { get; set; } = "Cash"; // Cash, Bank / Online / UPI, Cheque, Adjustment
    public string? ReferenceNo { get; set; } // Cheque No, UTR, Bill No
    public DateTime? ReferenceDate { get; set; }
    public string? Narration { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Multi-line particulars table
    public List<VoucherLineItem> Items { get; set; } = new();

    // Helper UI Properties
    public string TypeDisplay => VoucherType switch
    {
        "RECEIPT" => "🟢 RECEIPT (F6)",
        "PAYMENT" => "🔴 PAYMENT (F5)",
        "JOURNAL" => "🔵 JOURNAL (F7)",
        _ => VoucherType
    };

    public string TypeBadgeText => VoucherType switch
    {
        "RECEIPT" => "RECEIPT",
        "PAYMENT" => "PAYMENT",
        "JOURNAL" => "JOURNAL",
        _ => VoucherType
    };

    public string FormattedAmount => $"₹ {Amount:N2}";

    public string SummaryText
    {
        get
        {
            string primary = !string.IsNullOrWhiteSpace(PrimaryAccountName)
                ? PrimaryAccountName
                : (VoucherType.Equals("RECEIPT", StringComparison.OrdinalIgnoreCase) ? DebitAccountName : CreditAccountName);

            string counter;
            if (Items.Count > 1)
            {
                counter = $"{Items[0].AccountName} + {Items.Count - 1} other accounts";
            }
            else if (Items.Count == 1)
            {
                counter = Items[0].AccountName;
            }
            else
            {
                counter = VoucherType.Equals("RECEIPT", StringComparison.OrdinalIgnoreCase) ? CreditAccountName : DebitAccountName;
            }

            return VoucherType switch
            {
                "RECEIPT" => $"Into {primary} from {counter}",
                "PAYMENT" => $"From {primary} to {counter}",
                _ => $"{primary} -> {counter}"
            };
        }
    }
}
