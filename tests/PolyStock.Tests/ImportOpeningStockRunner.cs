using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PolyStock.Core.Models;
using PolyStock.Data;
using PolyStock.Services;
using Xunit;
using Xunit.Abstractions;

namespace PolyStock.Tests;

public class ImportOpeningStockRunner
{
    private readonly ITestOutputHelper _output;

    public ImportOpeningStockRunner(ITestOutputHelper output)
    {
        _output = output;
    }

    private class ExcelStockItemDto
    {
        public int row { get; set; }
        public string head_group { get; set; } = string.Empty;
        public string brand { get; set; } = string.Empty;
        public string size_name { get; set; } = string.Empty;
        public string item_name { get; set; } = string.Empty;
        public int qty { get; set; }
        public double pack_size { get; set; }
        public double total_kg { get; set; }
        public double landed_cost { get; set; }
        public double amount { get; set; }
        public string godown { get; set; } = string.Empty;
    }

    [Fact]
    public void ImportOpeningStockFromExcel()
    {
        string dbPath = @"E:\repos\PolyStock\Data\PolyStock.accdb";
        Assert.True(File.Exists(dbPath), $"Database not found at {dbPath}");

        string jsonPath = @"C:\Users\ASUS\.gemini\antigravity\brain\802e4f1d-69cd-4aeb-98af-711b74ff3264\scratch\parsed_stockdata.json";
        Assert.True(File.Exists(jsonPath), $"Parsed stockdata json not found at {jsonPath}");

        // 1. Ensure migrations are applied (including LandedCost)
        DatabaseInitializer.Initialize(dbPath);

        var service = new InventoryService(dbPath);

        // Check if OPENPURCHASE already exists; if so, skip or remove old one
        var existing = service.GetPurchaseByInvoiceNo("OPENPURCHASE");
        if (existing != null)
        {
            _output.WriteLine($"OPENPURCHASE already exists (ID: {existing.Id}).");
            return;
        }

        // 2. Ensure Godowns exist
        string[] requiredGodowns = { "G1", "G2", "G3L", "G3R", "MAIN" };
        var allGodowns = service.GetAllGodowns();
        var godownMap = new Dictionary<string, Godown>(StringComparer.OrdinalIgnoreCase);

        foreach (var gCode in requiredGodowns)
        {
            var match = allGodowns.FirstOrDefault(g => 
                g.Code.Equals(gCode, StringComparison.OrdinalIgnoreCase) || 
                g.Name.Equals(gCode, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                var newGodown = new Godown
                {
                    Code = gCode,
                    Name = gCode,
                    Location = $"Warehouse {gCode}",
                    Description = $"Godown {gCode} (Opening stock)",
                    IsActive = true
                };
                int gid = service.SaveGodown(newGodown);
                newGodown.Id = gid;
                godownMap[gCode] = newGodown;
                _output.WriteLine($"Created Godown: {gCode} (ID: {gid})");
            }
            else
            {
                godownMap[gCode] = match;
                _output.WriteLine($"Using existing Godown: {match.Name} (ID: {match.Id})");
            }
        }

        // 3. Load json rows
        var jsonText = File.ReadAllText(jsonPath);
        var dtoArray = JsonSerializer.Deserialize<List<ExcelStockItemDto>>(jsonText);
        Assert.NotNull(dtoArray);
        Assert.NotEmpty(dtoArray);
        _output.WriteLine($"Loaded {dtoArray.Count} lines from parsed stockdata.");

        // 4. Cache HeadGroups, Brands, Items
        var headGroups = service.GetAllHeadGroups();
        var headGroupMap = new Dictionary<string, HeadGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in headGroups)
        {
            string k = h.Name.Trim();
            if (!headGroupMap.ContainsKey(k)) headGroupMap[k] = h;
        }

        var brands = service.GetAllBrands();
        var brandMap = new Dictionary<string, Brand>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in brands)
        {
            string k = b.Name.Trim();
            if (!brandMap.ContainsKey(k)) brandMap[k] = b;
        }

        var items = service.GetAllItems();
        var itemMap = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
        foreach (var it in items)
        {
            string k = it.ItemName.Trim();
            if (!itemMap.ContainsKey(k)) itemMap[k] = it;
        }

        // Helper to get or create HeadGroup
        int GetOrCreateHeadGroup(string hgName)
        {
            string key = hgName.Trim().ToLowerInvariant();
            if (headGroupMap.TryGetValue(key, out var hg)) return hg.Id;

            var newHg = new HeadGroup { Name = hgName.Trim() };
            int id = service.SaveHeadGroup(newHg);
            newHg.Id = id;
            headGroupMap[key] = newHg;
            return id;
        }

        // Helper to get or create Brand
        int GetOrCreateBrand(string brandName)
        {
            string key = brandName.Trim().ToLowerInvariant();
            if (brandMap.TryGetValue(key, out var b)) return b.Id;

            var newBrand = new Brand { Name = brandName.Trim() };
            int id = service.SaveBrand(newBrand);
            newBrand.Id = id;
            brandMap[key] = newBrand;
            return id;
        }

        // Helper to get or create Item
        Item GetOrCreateItem(ExcelStockItemDto dto)
        {
            string key = dto.item_name.Trim().ToLowerInvariant();
            if (itemMap.TryGetValue(key, out var it))
            {
                // Ensure pack size is registered
                if (dto.pack_size > 0 && !it.PackSizes.Contains(dto.pack_size))
                {
                    it.PackSizes.Add(dto.pack_size);
                    service.SaveItem(it);
                }
                return it;
            }

            int hgId = GetOrCreateHeadGroup(dto.head_group);
            int brandId = GetOrCreateBrand(dto.brand);

            var newItem = new Item
            {
                ItemName = dto.item_name,
                ItemCode = dto.size_name,
                HeadGroupId = hgId,
                BrandId = brandId,
                DefaultRatePerKg = dto.landed_cost > 0.0001 ? dto.landed_cost : 0.0,
                Description = "Created from stockdata.xlsx opening stock import"
            };

            if (dto.pack_size > 0)
            {
                newItem.PackSizes.Add(dto.pack_size);
            }

            int newItemId = service.SaveItem(newItem);
            newItem.Id = newItemId;
            itemMap[key] = newItem;
            return newItem;
        }

        // 5. Build OPENPURCHASE
        var purchase = new Purchase
        {
            InvoiceNo = "OPENPURCHASE",
            SupplierName = "Opening Stock",
            PurchaseDate = new DateTime(2026, 4, 1),
            Notes = "Opening stock imported from stockdata.xlsx"
        };

        foreach (var dto in dtoArray)
        {
            var item = GetOrCreateItem(dto);

            if (!godownMap.TryGetValue(dto.godown, out var godown))
            {
                // Fallback to MAIN
                godown = godownMap["MAIN"];
            }

            var pi = new PurchaseItem
            {
                ItemId = item.Id,
                ItemName = item.ItemName,
                GodownId = godown.Id,
                GodownName = godown.Name,
                PackSizeKg = dto.pack_size,
                Bags = dto.qty,
                TotalKg = dto.total_kg,
                RatePerKg = dto.landed_cost,
                LandedCost = dto.landed_cost,
                Amount = dto.amount
            };

            purchase.Items.Add(pi);
        }

        purchase.TotalBags = purchase.Items.Sum(i => i.Bags);
        purchase.TotalWeightKg = Math.Round(purchase.Items.Sum(i => i.TotalKg), 3);
        purchase.SubTotal = Math.Round(purchase.Items.Sum(i => i.Amount), 2);
        purchase.GrandTotal = purchase.SubTotal;
        purchase.TotalAmount = purchase.SubTotal;

        _output.WriteLine($"Saving OPENPURCHASE: {purchase.TotalBags} Bags, {purchase.TotalWeightKg:N2} kg, Total: ₹{purchase.GrandTotal:N2}...");

        int purchaseId = service.SavePurchase(purchase);
        Assert.True(purchaseId > 0, "Failed to save OPENPURCHASE!");
        _output.WriteLine($"✓ Successfully saved OPENPURCHASE with ID: {purchaseId}");

        // 6. Verify PurchaseItems in DB
        var saved = service.GetPurchaseById(purchaseId);
        Assert.NotNull(saved);
        Assert.Equal(dtoArray.Count, saved.Items.Count);
        _output.WriteLine($"✓ Verified {saved.Items.Count} PurchaseItems saved in database.");

        // 7. Verify Stock in DB across Godowns
        var allStock = service.GetAllStock();
        _output.WriteLine($"✓ Current Stock rows in database: {allStock.Count}");
        double totalBagsInStock = allStock.Sum(s => s.FullBags);
        double totalKgInStock = allStock.Sum(s => s.TotalKg);
        _output.WriteLine($"✓ Total Bags across all Stock: {totalBagsInStock:N0}");
        _output.WriteLine($"✓ Total Weight across all Stock: {totalKgInStock:N2} kg");
    }
}
