using System;
using System.Collections.Generic;
using System.Linq;
using PolyStock.Core.Models;
using PolyStock.Services;
using PolyStock.WPF.Controls;
using PolyStock.WPF.ViewModels;
using Xunit;

namespace PolyStock.Tests;

public class SearchableItemTests
{
    private List<Item> CreateSampleCatalog()
    {
        return new List<Item>
        {
            new Item { Id = 1, ItemName = "Reliance Repol H030SG", BrandId = 1, BrandName = "Reliance", HeadGroupId = 10, HeadGroupName = "PP", ItemCode = "H030", DefaultRatePerKg = 105.0 },
            new Item { Id = 2, ItemName = "Reliance Repol H110FU", BrandId = 1, BrandName = "Reliance", HeadGroupId = 10, HeadGroupName = "PP", ItemCode = "H110", DefaultRatePerKg = 106.5 },
            new Item { Id = 3, ItemName = "Reliance Repol H200MA", BrandId = 1, BrandName = "Reliance", HeadGroupId = 10, HeadGroupName = "PP", ItemCode = "H200", DefaultRatePerKg = 108.0 },
            new Item { Id = 4, ItemName = "Reliance Relene 24FS040", BrandId = 1, BrandName = "Reliance", HeadGroupId = 20, HeadGroupName = "LLDPE", ItemCode = "24FS", DefaultRatePerKg = 98.0 },
            new Item { Id = 5, ItemName = "IOCL Propel 1030RG", BrandId = 2, BrandName = "IOCL", HeadGroupId = 10, HeadGroupName = "PP", ItemCode = "1030", DefaultRatePerKg = 104.0 },
            new Item { Id = 6, ItemName = "IOCL Propel 1110MG", BrandId = 2, BrandName = "IOCL", HeadGroupId = 10, HeadGroupName = "PP", ItemCode = "1110", DefaultRatePerKg = 105.5 },
            new Item { Id = 7, ItemName = "GAIL G-Lex 52A003", BrandId = 3, BrandName = "GAIL", HeadGroupId = 30, HeadGroupName = "HDPE", ItemCode = "52A", DefaultRatePerKg = 112.0 }
        };
    }

    [Fact]
    public void FilterAndSortItems_WithRow1Priority_OrdersSameGroupAndBrandFirst()
    {
        var catalog = CreateSampleCatalog();

        // Simulate Row 1 has selected Reliance Repol H030SG (Brand=1, HeadGroup=10)
        int row1HeadGroupId = 10;
        int row1BrandId = 1;

        // When Row 2 opens items without typing any query
        var results = SearchableItemWrapper.FilterAndSortItems(catalog, null, row1HeadGroupId, row1BrandId);

        Assert.Equal(catalog.Count, results.Count);

        // The top 3 items must be the sister grades matching both Reliance (Brand 1) and PP (HeadGroup 10)
        var top3 = results.Take(3).ToList();
        Assert.All(top3, r =>
        {
            Assert.True(r.IsPriorityMatch);
            Assert.Equal(1, r.Item.BrandId);
            Assert.Equal(10, r.Item.HeadGroupId);
        });

        // Verify the names are the 3 Reliance PP grades
        var top3Names = top3.Select(r => r.Item.ItemName).ToList();
        Assert.Contains("Reliance Repol H030SG", top3Names);
        Assert.Contains("Reliance Repol H110FU", top3Names);
        Assert.Contains("Reliance Repol H200MA", top3Names);

        // Subsequent items should be other items from Brand 1 or HeadGroup 10 before completely different items
        var fourth = results[3];
        Assert.True(fourth.Item.BrandId == 1 || fourth.Item.HeadGroupId == 10);
    }

    [Fact]
    public void FilterAndSortItems_WithQuery_FiltersAndPrioritizesSameGroupAndBrand()
    {
        var catalog = CreateSampleCatalog();

        // Simulate Row 1 has Reliance (Brand 1) & PP (HeadGroup 10)
        int row1HeadGroupId = 10;
        int row1BrandId = 1;

        // User in Row 2 types "11" (which matches both "H110FU" [Reliance PP] and "1110MG" [IOCL PP])
        var results = SearchableItemWrapper.FilterAndSortItems(catalog, "11", row1HeadGroupId, row1BrandId);

        Assert.Equal(2, results.Count);

        // The Reliance Repol H110FU must be ranked first because it matches Row 1's Brand & HeadGroup
        Assert.Equal("Reliance Repol H110FU", results[0].Item.ItemName);
        Assert.True(results[0].IsPriorityMatch);

        // The IOCL Propel 1110MG is ranked second
        Assert.Equal("IOCL Propel 1110MG", results[1].Item.ItemName);
        Assert.False(results[1].IsPriorityMatch);
    }

    [Fact]
    public void FilterAndSortItems_SearchAcrossItemCodeBrandAndName()
    {
        var catalog = CreateSampleCatalog();

        // Search by ItemCode "52A"
        var byCode = SearchableItemWrapper.FilterAndSortItems(catalog, "52A", 0, 0);
        Assert.Single(byCode);
        Assert.Equal("GAIL G-Lex 52A003", byCode[0].Item.ItemName);

        // Search by Brand "IOCL"
        var byBrand = SearchableItemWrapper.FilterAndSortItems(catalog, "IOCL", 0, 0);
        Assert.Equal(2, byBrand.Count);
        Assert.All(byBrand, r => Assert.Equal("IOCL", r.Item.BrandName));

        // Search by HeadGroup "LLDPE"
        var byGroup = SearchableItemWrapper.FilterAndSortItems(catalog, "LLDPE", 0, 0);
        Assert.Single(byGroup);
        Assert.Equal("Reliance Relene 24FS040", byGroup[0].Item.ItemName);
    }

    [Fact]
    public void SalesViewModel_Row1Selection_AutomaticallyUpdatesRow2Priority()
    {
        var service = new InventoryService();
        var vm = new SalesViewModel(service);
        vm.StartNewBill();

        Assert.NotEmpty(vm.Lines);
        var row1 = vm.Lines[0];

        // Ensure row 1 initially has no priority
        Assert.Equal(0, row1.PriorityHeadGroupId);
        Assert.Equal(0, row1.PriorityBrandId);

        // Set row 1 item
        var item1 = new Item { Id = 101, ItemName = "Test Grade A", BrandId = 5, BrandName = "BrandX", HeadGroupId = 8, HeadGroupName = "PP" };
        row1.Item = item1;

        // Add row 2
        vm.AddLine();
        Assert.Equal(2, vm.Lines.Count);
        var row2 = vm.Lines[1];

        // Row 2 should now automatically have inherited Row 1's HeadGroupId and BrandId!
        Assert.Equal(8, row2.PriorityHeadGroupId);
        Assert.Equal(5, row2.PriorityBrandId);

        // If Row 1 item changes to another brand/group
        var item2 = new Item { Id = 102, ItemName = "Test Grade B", BrandId = 9, BrandName = "BrandY", HeadGroupId = 12, HeadGroupName = "HDPE" };
        row1.Item = item2;

        // Row 2 priority must automatically update to reflect Row 1's new brand & group!
        Assert.Equal(12, row2.PriorityHeadGroupId);
        Assert.Equal(9, row2.PriorityBrandId);
    }

    [Fact]
    public void PurchaseViewModel_Row1Selection_AutomaticallyUpdatesRow2Priority()
    {
        var service = new InventoryService();
        var vm = new PurchaseViewModel(service);
        vm.StartNewBill();

        Assert.NotEmpty(vm.Lines);
        var row1 = vm.Lines[0];

        var item = new Item { Id = 201, ItemName = "Purchase Grade X", BrandId = 7, BrandName = "Reliance", HeadGroupId = 3, HeadGroupName = "PVC" };
        row1.Item = item;

        vm.AddLine();
        var row2 = vm.Lines[1];

        Assert.Equal(3, row2.PriorityHeadGroupId);
        Assert.Equal(7, row2.PriorityBrandId);
        Assert.Equal("Reliance ", row2.SearchPrefix);
    }

    [Fact]
    public void GetPrefixUpToHeadGroupAndBrand_ExtractsPrefixCorrectly()
    {
        // 1. Direct match: HeadGroup "CC" + Brand "Balaji" + Code "7"
        var item1 = new Item { ItemName = "CC Balaji 7", HeadGroupName = "CC", BrandName = "Balaji", ItemCode = "7" };
        Assert.Equal("CC Balaji ", SearchableItemWrapper.GetPrefixUpToHeadGroupAndBrand(item1));

        // 2. Name ending with code: "Reliance Repol H030SG"
        var item2 = new Item { ItemName = "Reliance Repol H030SG", HeadGroupName = "PP", BrandName = "Reliance", ItemCode = "H030SG" };
        Assert.Equal("Reliance Repol ", SearchableItemWrapper.GetPrefixUpToHeadGroupAndBrand(item2));

        // 3. Name ending with code: "IOCL Propel 1110MG"
        var item3 = new Item { ItemName = "IOCL Propel 1110MG", HeadGroupName = "PP", BrandName = "IOCL", ItemCode = "1110MG" };
        Assert.Equal("IOCL Propel ", SearchableItemWrapper.GetPrefixUpToHeadGroupAndBrand(item3));
    }

    [Fact]
    public void SalesViewModel_Row1Selection_PreFillsSearchPrefixOnRow2()
    {
        var service = new InventoryService();
        var vm = new SalesViewModel(service);
        vm.StartNewBill();

        var row1 = vm.Lines[0];
        row1.Item = new Item
        {
            Id = 501,
            ItemName = "CC Balaji 7",
            BrandId = 1,
            BrandName = "Balaji",
            HeadGroupId = 1,
            HeadGroupName = "CC",
            ItemCode = "7",
            DefaultRatePerKg = 40.0,
            PackSizes = new List<double> { 30.0, 25.0 }
        };
        row1.PackSizeKg = 30.0;
        row1.RatePerKg = 40.0;

        // When Row 2 is created
        vm.AddLine();
        Assert.Equal(2, vm.Lines.Count);
        var row2 = vm.Lines[1];

        // Row 2 must have the exact prefix from row 1 up to headgroup & brand
        Assert.Equal("CC Balaji ", row2.SearchPrefix);
        Assert.Equal(30.0, row2.PackSizeKg);
        Assert.Equal(40.0, row2.RatePerKg);
    }

    [Fact]
    public void SearchableItemBox_InstantiateAndSetNull_DoesNotThrow()
    {
        Exception? caught = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                var box = new SearchableItemBox();
                box.SelectedItem = null;
                Assert.Null(box.SelectedItem);

                var item = new Item { Id = 1, ItemName = "Test Item" };
                box.SelectedItem = item;
                Assert.Equal(item, box.SelectedItem);

                box.SelectedItem = null;
                Assert.Null(box.SelectedItem);
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(caught);
    }
}
