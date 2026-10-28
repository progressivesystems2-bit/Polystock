using System;
using System.Collections.Generic;

namespace PolyStock.Core.Models;

public class Sale
{
    public int Id { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public int? GodownId { get; set; }
    public string? GodownName { get; set; }
    public DateTime SaleDate { get; set; } = DateTime.Today;
    public int TotalBags { get; set; }
    public double TotalLooseKg { get; set; }
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

    // Payment Settlement Mode: Credit, Cash, UPI, Split
    public string PaymentMode { get; set; } = "Credit";
    public int? BankAccountId { get; set; }
    public string? BankAccountName { get; set; }
    public string? UtrRefNo { get; set; }
    public double CashReceived { get; set; }
    public double UpiReceived { get; set; }
    public double CreditAmount { get; set; }

    public List<SaleItem> Items { get; set; } = new();
}

public class SaleItem
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int? GodownId { get; set; }
    public string? GodownName { get; set; }
    public double PackSizeKg { get; set; }
    public int BagsSold { get; set; }
    public double LooseKgSold { get; set; }
    public double TotalKgSold { get; set; }
    public double RatePerKg { get; set; }
    public double Amount { get; set; }
    public int BagsOpened { get; set; } // how many full bags were cut/opened during this sale
    public bool SkipStockDeduction { get; set; } // whether this line item skipped inventory subtraction
}
