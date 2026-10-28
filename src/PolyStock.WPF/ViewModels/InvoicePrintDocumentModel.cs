using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using PolyStock.Core.Common;

namespace PolyStock.WPF.ViewModels;

public class PrintItemRow
{
    public string ItemName { get; set; } = string.Empty;
    public string PackSize { get; set; } = string.Empty;
    public string Bags { get; set; } = string.Empty;
    public string LooseKg { get; set; } = string.Empty;
    public double TotalWeight { get; set; }
    public double Rate { get; set; }
    public double Amount { get; set; }
}

public class InvoicePrintDocumentModel : ObservableObject
{
    public string DocumentTitle { get; set; } = "TAX INVOICE";
    public string DocumentSubTitle { get; set; } = "ORIGINAL FOR RECIPIENT";
    public string PartyHeader { get; set; } = "BILLED TO / CUSTOMER:";
    public string PartyName { get; set; } = string.Empty;
    public string DateLabel { get; set; } = "Invoice Date: ";
    public string DateText { get; set; } = string.Empty;
    public string InvoiceNo { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public ObservableCollection<PrintItemRow> PrintItems { get; } = new();

    public int TotalBags { get; set; }
    public double TotalWeightKg { get; set; }
    public double SubTotal { get; set; }
    public double GstPercent { get; set; }
    public double GstAmount { get; set; }
    public double Freight { get; set; }
    public double LoadingUnloading { get; set; }
    public double PartyAmount { get; set; }
    public double GrandTotal { get; set; }
    public string PartyPostingText { get; set; } = string.Empty;
    public string DualBreakdownText { get; set; } = string.Empty;
    public string TermsPartyName { get; set; } = "POLYSTOCK DISTRIBUTORS";

    public static InvoicePrintDocumentModel FromSales(SalesViewModel vm)
    {
        var model = new InvoicePrintDocumentModel
        {
            DocumentTitle = "TAX INVOICE",
            DocumentSubTitle = "ORIGINAL FOR RECIPIENT",
            PartyHeader = "BILLED TO / CUSTOMER:",
            PartyName = vm.CustomerName,
            DateLabel = "Invoice Date: ",
            DateText = vm.SaleDate.ToString("dd-MMM-yyyy"),
            InvoiceNo = vm.InvoiceNo,
            Notes = vm.Notes,
            TotalBags = vm.TotalBags,
            TotalWeightKg = vm.TotalWeightKg,
            SubTotal = vm.SubTotal,
            GstPercent = vm.GstPercent,
            GstAmount = vm.GstAmount,
            Freight = vm.Freight,
            LoadingUnloading = vm.LoadingUnloading,
            PartyAmount = vm.PartyAmount,
            GrandTotal = vm.GrandTotal,
            PartyPostingText = $"Party Bill Amount (SubTotal + GST): ₹ {vm.PartyAmount:N2}",
            DualBreakdownText = $"Full Bags ({vm.TotalBags}) + Loose ({vm.TotalLooseKg:F2} kg) Auto Calculations",
            TermsPartyName = "POLYSTOCK DISTRIBUTORS"
        };

        foreach (var line in vm.Lines)
        {
            if (line.Item == null || line.TotalKgSold <= 0.0001) continue;
            model.PrintItems.Add(new PrintItemRow
            {
                ItemName = line.Item.ItemName,
                PackSize = $"{line.PackSizeValue} kg",
                Bags = $"{line.BagsSold}",
                LooseKg = line.LooseKgSold > 0.0001 ? $"{line.LooseKgSold:F2}" : "-",
                TotalWeight = line.TotalKgSold,
                Rate = line.RatePerKg,
                Amount = line.Amount
            });
        }

        return model;
    }

    public static InvoicePrintDocumentModel FromPurchase(PurchaseViewModel vm)
    {
        var model = new InvoicePrintDocumentModel
        {
            DocumentTitle = "PURCHASE INWARD VOUCHER",
            DocumentSubTitle = "GOODS INWARD NOTE & VOUCHER",
            PartyHeader = "SUPPLIER / VENDOR:",
            PartyName = vm.SupplierName,
            DateLabel = "Purchase Date: ",
            DateText = vm.PurchaseDate.ToString("dd-MMM-yyyy"),
            InvoiceNo = vm.InvoiceNo,
            Notes = vm.Notes,
            TotalBags = vm.TotalBags,
            TotalWeightKg = vm.TotalWeightKg,
            SubTotal = vm.SubTotal,
            GstPercent = vm.GstPercent,
            GstAmount = vm.GstAmount,
            Freight = vm.Freight,
            LoadingUnloading = vm.LoadingUnloading,
            PartyAmount = vm.PartyAmount,
            GrandTotal = vm.GrandTotal,
            PartyPostingText = $"Supplier Bill Amount (SubTotal + GST): ₹ {vm.PartyAmount:N2}",
            DualBreakdownText = $"Total Inward: {vm.TotalBags} Bags ({vm.TotalWeightKg:F2} kg) | Avg Landing: ₹ {vm.AverageEffectiveRatePerKg:N2}/kg",
            TermsPartyName = "POLYSTOCK WAREHOUSE INWARD"
        };

        foreach (var line in vm.Lines)
        {
            if (line.Item == null || line.TotalKg <= 0.0001) continue;
            model.PrintItems.Add(new PrintItemRow
            {
                ItemName = line.Item.ItemName,
                PackSize = $"{line.PackSizeValue} kg",
                Bags = $"{line.Bags}",
                LooseKg = "-",
                TotalWeight = line.TotalKg,
                Rate = line.RatePerKg,
                Amount = line.Amount
            });
        }

        return model;
    }
}
