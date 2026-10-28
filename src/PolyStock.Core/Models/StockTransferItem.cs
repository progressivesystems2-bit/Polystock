using System;

namespace PolyStock.Core.Models;

public class StockTransferItem
{
    public int Id { get; set; }
    public int TransferId { get; set; }
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public double PackSizeKg { get; set; }
    public int Bags { get; set; }
    public double LooseKg { get; set; }
    public double TotalKg { get; set; }
    public double RatePerKg { get; set; }
    public double Amount { get; set; }
    public string? Remarks { get; set; }
}
