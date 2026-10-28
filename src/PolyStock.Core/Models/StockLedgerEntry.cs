using System;

namespace PolyStock.Core.Models;

public enum TransactionType
{
    Purchase,
    Sale,
    ManualAdjustment,
    BagOpened,
    TransferOut,
    TransferIn
}

public class StockLedgerEntry
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int? GodownId { get; set; }
    public string? GodownName { get; set; }
    public double PackSizeKg { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string RefInvoiceNo { get; set; } = string.Empty;
    public int FullBagsChange { get; set; }
    public double LooseKgChange { get; set; }
    public double TotalKgChange { get; set; }
    public int BalanceFullBags { get; set; }
    public double BalanceLooseKg { get; set; }
    public double BalanceTotalKg { get; set; }
    public string? Remarks { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
