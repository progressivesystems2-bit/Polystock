using System;

namespace PolyStock.Core.Models;

public class ProfitLossRow
{
    public int ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public int HeadGroupId { get; set; }
    public string HeadGroupName { get; set; } = string.Empty;
    public int BrandId { get; set; }
    public string BrandName { get; set; } = string.Empty;

    // Sales in Period
    public int SoldBags { get; set; }
    public double SoldLooseKg { get; set; }
    public double SoldKg { get; set; }
    public double TotalSalesAmount { get; set; }
    public double AvgSaleRatePerKg { get; set; }

    // Purchases in Period
    public int PurchasedBags { get; set; }
    public double PurchasedKg { get; set; }
    public double TotalPurchaseAmount { get; set; }
    public double PeriodAvgPurchaseRatePerKg { get; set; }

    // Valuation / Cost Rate Applied
    public double EffectivePurchaseRatePerKg { get; set; }
    public string PurchaseRateSource { get; set; } = "Period"; // "Period", "Historical", "Master Rate"

    // Profit & Loss
    public double CogsAmount { get; set; }
    public double ProfitLossAmount { get; set; }
    public double ProfitPerKg { get; set; }
    public double MarginPercent { get; set; }
    public bool IsProfitable => ProfitLossAmount >= 0;

    // Display Formats
    public string FormattedSalesAmount => $"₹ {TotalSalesAmount:N2}";
    public string FormattedCogsAmount => $"₹ {CogsAmount:N2}";
    public string FormattedProfitLoss => $"₹ {Math.Abs(ProfitLossAmount):N2}";
    public string FormattedProfitLossSigned => ProfitLossAmount >= 0 ? $"+₹ {ProfitLossAmount:N2}" : $"-₹ {Math.Abs(ProfitLossAmount):N2}";
    public string FormattedAvgSaleRate => $"₹ {AvgSaleRatePerKg:N2}";
    public string FormattedPurchaseRate => $"₹ {EffectivePurchaseRatePerKg:N2}";
    public string FormattedProfitPerKg => ProfitPerKg >= 0 ? $"+₹ {ProfitPerKg:N2}" : $"-₹ {Math.Abs(ProfitPerKg):N2}";
    public string FormattedMarginPercent => $"{MarginPercent:N2}%";
    public string StatusText => ProfitLossAmount > 0.001 ? "PROFIT" : (ProfitLossAmount < -0.001 ? "LOSS" : "BREAK-EVEN");
}

public class ProfitLossSummary
{
    public double TotalSalesRevenue { get; set; }
    public double TotalCogs { get; set; }
    public double NetProfitLoss => TotalSalesRevenue - TotalCogs;
    public double OverallMarginPercent => TotalSalesRevenue > 0.0001 ? (NetProfitLoss / TotalSalesRevenue * 100.0) : 0.0;

    public double TotalSoldKg { get; set; }
    public int TotalSoldBags { get; set; }
    public double TotalPurchasedKg { get; set; }
    public int TotalPurchasedBags { get; set; }

    public double AverageProfitPerKg => TotalSoldKg > 0.0001 ? (NetProfitLoss / TotalSoldKg) : 0.0;
    public int ProfitableItemsCount { get; set; }
    public int LossItemsCount { get; set; }
    public int BreakEvenItemsCount { get; set; }
    public int TotalTradedItemsCount { get; set; }
    public bool IsNetProfitable => NetProfitLoss >= 0;

    public string FormattedTotalRevenue => $"₹ {TotalSalesRevenue:N2}";
    public string FormattedTotalCogs => $"₹ {TotalCogs:N2}";
    public string FormattedNetProfitLoss => $"₹ {Math.Abs(NetProfitLoss):N2}";
    public string FormattedNetProfitLossSigned => NetProfitLoss >= 0 ? $"+₹ {NetProfitLoss:N2}" : $"-₹ {Math.Abs(NetProfitLoss):N2}";
    public string FormattedOverallMargin => $"{OverallMarginPercent:N2}%";
    public string FormattedAverageProfitPerKg => AverageProfitPerKg >= 0 ? $"+₹ {AverageProfitPerKg:N2}/kg" : $"-₹ {Math.Abs(AverageProfitPerKg):N2}/kg";
}
