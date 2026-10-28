using System;

namespace PolyStock.Core.Models;

public class VoucherLineItem
{
    public int Id { get; set; }
    public int VoucherId { get; set; }
    public int? AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public double CurrentBalance { get; set; }
    public double Amount { get; set; }
    public string? ReferenceNo { get; set; }
    public string? Remarks { get; set; }

    public string FormattedAmount => Amount > 0.0001 ? $"₹ {Amount:N2}" : "₹ 0.00";
    public string FormattedCurrentBalance => Math.Abs(CurrentBalance) > 0.0001
        ? $"₹ {Math.Abs(CurrentBalance):N2} {(CurrentBalance >= 0 ? "Dr" : "Cr")}"
        : "₹ 0.00";
}
