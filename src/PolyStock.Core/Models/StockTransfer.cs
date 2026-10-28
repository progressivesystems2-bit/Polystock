using System;
using System.Collections.Generic;

namespace PolyStock.Core.Models;

public class StockTransfer
{
    public int Id { get; set; }
    public string TransferNo { get; set; } = string.Empty;
    public DateTime TransferDate { get; set; } = DateTime.Today;
    public int FromGodownId { get; set; }
    public string FromGodownName { get; set; } = string.Empty;
    public int ToGodownId { get; set; }
    public string ToGodownName { get; set; } = string.Empty;
    public int TotalBags { get; set; }
    public double TotalLooseKg { get; set; }
    public double TotalWeightKg { get; set; }
    public double TotalAmount { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<StockTransferItem> Items { get; set; } = new();

    public string TransferRouteDisplay => $"{FromGodownName} ➔ {ToGodownName}";
    public string SummaryDisplay => $"{TotalBags} Bags ({TotalWeightKg:F1} kg)";
}
