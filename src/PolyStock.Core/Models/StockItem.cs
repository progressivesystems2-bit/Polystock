using System;

namespace PolyStock.Core.Models;

public class StockItem
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string HeadGroupName { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public int? GodownId { get; set; }
    public string? GodownName { get; set; }
    public double PackSizeKg { get; set; }
    public int FullBags { get; set; }
    public double LooseKg { get; set; }
    public double TotalKg { get; set; }
    public double RatePerKg { get; set; }
    public double Valuation => TotalKg * RatePerKg;
    public DateTime LastUpdated { get; set; } = DateTime.Now;

    public string StockDisplay =>
        $"{FullBags} Bags ({TotalKg:F1} kg)";

    public bool HasOpenBag => LooseKg > 0.001;
}

public class StockSummaryItem
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string HeadGroupName { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public int TotalBags { get; set; }
    public double TotalKg { get; set; }
    public double RatePerKg { get; set; }
    public double Valuation => TotalKg * RatePerKg;
    public string BagBreakdownSummary { get; set; } = string.Empty;
}
