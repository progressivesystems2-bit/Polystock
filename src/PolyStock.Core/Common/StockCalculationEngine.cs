using System;

namespace PolyStock.Core.Common;

public static class StockCalculationEngine
{
    public record BagMorphDeductionResult(
        double SourcePackSizeKg,
        int SourceBagsDeducted,
        int SourceBagsRemaining,
        double MorphedPackSizeKg,
        int MorphedBagsAdded,
        double TotalKgSold,
        bool Success,
        string? ErrorMessage);

    /// <summary>
    /// Determines whether a sales line item should be deducted from warehouse inventory
    /// based on the configured sales weight threshold in KG.
    /// If sales of line item is below threshold KG, it doesn't get subtracted from inventory.
    /// </summary>
    public static bool ShouldDeductInventory(double totalKgSold, double thresholdKg)
    {
        if (thresholdKg <= 0.0001) return true;
        return totalKgSold >= thresholdKg;
    }

    /// <summary>
    /// Calculates bag deduction with dynamic bag morphing:
    /// When loose kg is sold from a bag of packSizeKg, 1 bag is cut/opened, and the remaining weight
    /// becomes a new bag of (packSizeKg - looseKgSold).
    /// </summary>
    public static BagMorphDeductionResult CalculateBagMorph(
        int currentFullBags,
        double packSizeKg,
        int bagsSold,
        double looseKgSold)
    {
        if (packSizeKg <= 0.0001)
        {
            return new BagMorphDeductionResult(packSizeKg, 0, currentFullBags, 0, 0, 0, false, "Pack size must be greater than zero.");
        }

        if (bagsSold < 0 || looseKgSold < 0)
        {
            return new BagMorphDeductionResult(packSizeKg, 0, currentFullBags, 0, 0, 0, false, "Quantities cannot be negative.");
        }

        int bagsToCut = looseKgSold > 0.0001 ? 1 : 0;
        int totalBagsNeeded = bagsSold + bagsToCut;

        if (totalBagsNeeded == 0)
        {
            return new BagMorphDeductionResult(packSizeKg, 0, currentFullBags, 0, 0, 0, true, null);
        }

        if (currentFullBags < totalBagsNeeded)
        {
            return new BagMorphDeductionResult(
                packSizeKg,
                0,
                currentFullBags,
                0,
                0,
                0,
                false,
                $"Insufficient bags of {packSizeKg:0.##} kg. Available: {currentFullBags} Bag(s), Required: {totalBagsNeeded} Bag(s).");
        }

        if (looseKgSold >= packSizeKg)
        {
            return new BagMorphDeductionResult(
                packSizeKg,
                0,
                currentFullBags,
                0,
                0,
                0,
                false,
                $"Loose quantity ({looseKgSold:F2} kg) cannot be greater than or equal to bag weight ({packSizeKg:0.##} kg). Please enter whole bags instead.");
        }

        int sourceBagsRemaining = currentFullBags - totalBagsNeeded;
        double morphedPackSizeKg = 0.0;
        int morphedBagsAdded = 0;

        if (looseKgSold > 0.0001)
        {
            morphedPackSizeKg = Math.Round(packSizeKg - looseKgSold, 3);
            if (morphedPackSizeKg > 0.0001)
            {
                morphedBagsAdded = 1;
            }
        }

        double totalKgSold = (bagsSold * packSizeKg) + looseKgSold;

        return new BagMorphDeductionResult(
            packSizeKg,
            totalBagsNeeded,
            sourceBagsRemaining,
            morphedPackSizeKg,
            morphedBagsAdded,
            Math.Round(totalKgSold, 3),
            true,
            null);
    }
}
