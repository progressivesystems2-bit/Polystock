using System;
using PolyStock.Core.Common;
using Xunit;

namespace PolyStock.Tests;

public class StockDeductionTests
{
    [Fact]
    public void Scenario1_FullBagSale_DeductsFullBagDirectly()
    {
        // 5 bags of 30kg, sell 1 full bag
        var result = StockCalculationEngine.CalculateBagMorph(
            currentFullBags: 5,
            packSizeKg: 30,
            bagsSold: 1,
            looseKgSold: 0);

        Assert.True(result.Success);
        Assert.Equal(4, result.SourceBagsRemaining);
        Assert.Equal(0, result.MorphedBagsAdded);
        Assert.Equal(30.0, result.TotalKgSold);
    }

    [Fact]
    public void Scenario2_LooseSaleFromSealedBag_MorphsRemainderIntoNewBag()
    {
        // 3 bags of 20kg, sell 8kg loose
        // Deducts 1 bag of 20kg (leaves 2 bags of 20kg), remainder 12kg becomes a new 12kg bag
        var result = StockCalculationEngine.CalculateBagMorph(
            currentFullBags: 3,
            packSizeKg: 20,
            bagsSold: 0,
            looseKgSold: 8);

        Assert.True(result.Success);
        Assert.Equal(2, result.SourceBagsRemaining);
        Assert.Equal(1, result.SourceBagsDeducted);
        Assert.Equal(12.0, result.MorphedPackSizeKg);
        Assert.Equal(1, result.MorphedBagsAdded);
        Assert.Equal(8.0, result.TotalKgSold);
    }

    [Fact]
    public void Scenario3_SubsequentLooseSaleFromReducedBag_MorphsAgain()
    {
        // 1 bag of 12kg (already a reduced bag), sell 5kg loose
        // Deducts 1 bag of 12kg (leaves 0 bags of 12kg), remainder 7kg becomes a new 7kg bag
        var result = StockCalculationEngine.CalculateBagMorph(
            currentFullBags: 1,
            packSizeKg: 12,
            bagsSold: 0,
            looseKgSold: 5);

        Assert.True(result.Success);
        Assert.Equal(0, result.SourceBagsRemaining);
        Assert.Equal(1, result.SourceBagsDeducted);
        Assert.Equal(7.0, result.MorphedPackSizeKg);
        Assert.Equal(1, result.MorphedBagsAdded);
        Assert.Equal(5.0, result.TotalKgSold);
    }

    [Fact]
    public void Scenario4_SellingFullRemainderOfReducedBag_LeavesZeroRemainder()
    {
        // 1 bag of 7kg, sell 1 full bag
        var result = StockCalculationEngine.CalculateBagMorph(
            currentFullBags: 1,
            packSizeKg: 7,
            bagsSold: 1,
            looseKgSold: 0);

        Assert.True(result.Success);
        Assert.Equal(0, result.SourceBagsRemaining);
        Assert.Equal(0, result.MorphedBagsAdded);
        Assert.Equal(7.0, result.TotalKgSold);
    }

    [Fact]
    public void Scenario5_MixedSale_FullBagsAndLooseInSameLine()
    {
        // 5 bags of 25kg, sell 2 full bags AND 5kg loose
        // Consumes 3 bags of 25kg (2 sold full + 1 cut), leaves 2 bags of 25kg + creates 1 bag of 20kg
        var result = StockCalculationEngine.CalculateBagMorph(
            currentFullBags: 5,
            packSizeKg: 25,
            bagsSold: 2,
            looseKgSold: 5);

        Assert.True(result.Success);
        Assert.Equal(2, result.SourceBagsRemaining);
        Assert.Equal(3, result.SourceBagsDeducted);
        Assert.Equal(20.0, result.MorphedPackSizeKg);
        Assert.Equal(1, result.MorphedBagsAdded);
        Assert.Equal(55.0, result.TotalKgSold);
    }

    [Fact]
    public void Scenario6_Overselling_FailsWithErrorMessage()
    {
        // 1 bag of 20kg, attempt to sell 2 bags or sell 25kg loose
        var result = StockCalculationEngine.CalculateBagMorph(
            currentFullBags: 1,
            packSizeKg: 20,
            bagsSold: 0,
            looseKgSold: 25);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }
}
