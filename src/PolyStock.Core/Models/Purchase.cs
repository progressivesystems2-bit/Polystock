using System;
using System.Collections.Generic;

namespace PolyStock.Core.Models;

public class Purchase
{
    public int Id { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public int? GodownId { get; set; }
    public string? GodownName { get; set; }
    public DateTime PurchaseDate { get; set; } = DateTime.Today;
    public int TotalBags { get; set; }
    public double TotalWeightKg { get; set; }
    public double SubTotal { get; set; }
    public double GstPercent { get; set; }
    public double GstAmount { get; set; }
    public double Freight { get; set; }
    public double LoadingUnloading { get; set; }
    public double PartyAmount => SubTotal + GstAmount; // Bill Amount + GST Amount posted to party
    public double GrandTotal { get; set; }
    public double TotalAmount
    {
        get => GrandTotal > 0 ? GrandTotal : (SubTotal > 0 ? SubTotal : 0.0);
        set
        {
            GrandTotal = value;
            if (SubTotal == 0) SubTotal = value;
        }
    }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public List<PurchaseItem> Items { get; set; } = new();
}

public class PurchaseItem
{
    public int Id { get; set; }
    public int PurchaseId { get; set; }
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int? GodownId { get; set; }
    public string? GodownName { get; set; }
    public double PackSizeKg { get; set; }
    public int Bags { get; set; }
    public double TotalKg { get; set; }
    public double RatePerKg { get; set; }
    public double Amount { get; set; }
    public double LandedCost { get; set; }
}
