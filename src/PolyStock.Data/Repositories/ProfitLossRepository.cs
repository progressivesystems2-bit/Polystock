using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Linq;
using PolyStock.Core.Models;

namespace PolyStock.Data.Repositories;

public class ProfitLossRepository
{
    private readonly string? _dbPath;

    public ProfitLossRepository(string? dbPath = null)
    {
        _dbPath = dbPath;
    }

    private class SaleLineAggregate
    {
        public int ItemId { get; set; }
        public int BagsSold { get; set; }
        public double LooseKgSold { get; set; }
        public double TotalKgSold { get; set; }
        public double TotalAmount { get; set; }
    }

    private class PurchaseLineAggregate
    {
        public int ItemId { get; set; }
        public int BagsPurchased { get; set; }
        public double TotalKgPurchased { get; set; }
        public double TotalAmount { get; set; }
    }

    public (List<ProfitLossRow> Rows, ProfitLossSummary Summary) GetProfitLossReport(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? headGroupId = null,
        int? brandId = null,
        int? itemId = null,
        bool onlySoldItems = true)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        // 1. Fetch Item catalog with HeadGroup and Brand names
        var itemsDict = LoadItems(conn, headGroupId, brandId, itemId);

        // 2. Fetch Period Sales aggregates
        var periodSales = LoadSalesAggregates(conn, fromDate, toDate, itemId);

        // 3. Fetch Period Purchases aggregates
        var periodPurchases = LoadPurchasesAggregates(conn, fromDate, toDate, itemId);

        // 4. Fetch Historical Purchases aggregates for fallback rates
        var historicalPurchases = LoadHistoricalPurchasesAggregates(conn, toDate, itemId);

        // 5. Construct P/L rows
        var rows = new List<ProfitLossRow>();

        // Candidate item IDs:
        // If onlySoldItems == true: only items that had sales in the period
        // Else: all items that had sales OR purchases OR all catalog items matching master filter
        IEnumerable<int> candidateItemIds;
        if (onlySoldItems)
        {
            candidateItemIds = periodSales.Keys.Where(id => itemsDict.ContainsKey(id));
        }
        else
        {
            candidateItemIds = periodSales.Keys
                .Union(periodPurchases.Keys)
                .Where(id => itemsDict.ContainsKey(id));

            // If a specific itemId or headGroup/brand filter was passed, include all filtered items
            if (itemId.HasValue || headGroupId.HasValue || brandId.HasValue)
            {
                candidateItemIds = candidateItemIds.Union(itemsDict.Keys);
            }
        }

        foreach (var id in candidateItemIds.OrderBy(id => itemsDict.TryGetValue(id, out var itm) ? itm.ItemName : string.Empty))
        {
            if (!itemsDict.TryGetValue(id, out var item)) continue;

            periodSales.TryGetValue(id, out var saleAgg);
            periodPurchases.TryGetValue(id, out var purAgg);
            historicalPurchases.TryGetValue(id, out var histAgg);

            int soldBags = saleAgg?.BagsSold ?? 0;
            double soldLooseKg = saleAgg?.LooseKgSold ?? 0.0;
            double soldKg = saleAgg?.TotalKgSold ?? 0.0;
            double salesAmount = saleAgg?.TotalAmount ?? 0.0;
            double avgSaleRate = soldKg > 0.0001 ? Math.Round(salesAmount / soldKg, 4) : 0.0;

            int purBags = purAgg?.BagsPurchased ?? 0;
            double purKg = purAgg?.TotalKgPurchased ?? 0.0;
            double purAmount = purAgg?.TotalAmount ?? 0.0;
            double periodAvgPurRate = purKg > 0.0001 ? Math.Round(purAmount / purKg, 4) : 0.0;

            // Determine effective purchase rate / cost basis
            double effectivePurRate;
            string rateSource;

            if (purKg > 0.0001)
            {
                effectivePurRate = periodAvgPurRate;
                rateSource = "Period Purchases";
            }
            else if (histAgg != null && histAgg.TotalKgPurchased > 0.0001)
            {
                effectivePurRate = Math.Round(histAgg.TotalAmount / histAgg.TotalKgPurchased, 4);
                rateSource = "Historical Purchases";
            }
            else
            {
                effectivePurRate = item.DefaultRatePerKg;
                rateSource = "Master Rate";
            }

            double cogs = Math.Round(soldKg * effectivePurRate, 2);
            double profitLoss = Math.Round(salesAmount - cogs, 2);
            double profitPerKg = soldKg > 0.0001 ? Math.Round(profitLoss / soldKg, 4) : 0.0;
            double marginPercent = salesAmount > 0.0001 ? Math.Round((profitLoss / salesAmount) * 100.0, 2) : 0.0;

            rows.Add(new ProfitLossRow
            {
                ItemId = item.Id,
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                HeadGroupId = item.HeadGroupId,
                HeadGroupName = item.HeadGroupName,
                BrandId = item.BrandId,
                BrandName = item.BrandName,

                SoldBags = soldBags,
                SoldLooseKg = soldLooseKg,
                SoldKg = soldKg,
                TotalSalesAmount = salesAmount,
                AvgSaleRatePerKg = avgSaleRate,

                PurchasedBags = purBags,
                PurchasedKg = purKg,
                TotalPurchaseAmount = purAmount,
                PeriodAvgPurchaseRatePerKg = periodAvgPurRate,

                EffectivePurchaseRatePerKg = effectivePurRate,
                PurchaseRateSource = rateSource,

                CogsAmount = cogs,
                ProfitLossAmount = profitLoss,
                ProfitPerKg = profitPerKg,
                MarginPercent = marginPercent
            });
        }

        // Summary calculations
        var summary = new ProfitLossSummary
        {
            TotalSalesRevenue = Math.Round(rows.Sum(r => r.TotalSalesAmount), 2),
            TotalCogs = Math.Round(rows.Sum(r => r.CogsAmount), 2),
            TotalSoldKg = Math.Round(rows.Sum(r => r.SoldKg), 3),
            TotalSoldBags = rows.Sum(r => r.SoldBags),
            TotalPurchasedKg = Math.Round(rows.Sum(r => r.PurchasedKg), 3),
            TotalPurchasedBags = rows.Sum(r => r.PurchasedBags),
            ProfitableItemsCount = rows.Count(r => r.ProfitLossAmount > 0.001),
            LossItemsCount = rows.Count(r => r.ProfitLossAmount < -0.001),
            BreakEvenItemsCount = rows.Count(r => Math.Abs(r.ProfitLossAmount) <= 0.001),
            TotalTradedItemsCount = rows.Count
        };

        return (rows, summary);
    }

    private Dictionary<int, Item> LoadItems(OleDbConnection conn, int? headGroupId, int? brandId, int? itemId)
    {
        var items = new Dictionary<int, Item>();
        var sql = @"
            SELECT i.Id, i.HeadGroupId, hg.[Name] AS HeadGroupName, i.BrandId, b.[Name] AS BrandName, 
                   i.ItemCode, i.ItemName, i.DefaultRatePerKg
            FROM (Items i
            LEFT JOIN HeadGroups hg ON i.HeadGroupId = hg.Id)
            LEFT JOIN Brands b ON i.BrandId = b.Id
            WHERE 1=1";

        if (headGroupId.HasValue) sql += " AND i.HeadGroupId = ?";
        if (brandId.HasValue) sql += " AND i.BrandId = ?";
        if (itemId.HasValue) sql += " AND i.Id = ?";

        using var cmd = new OleDbCommand(sql, conn);
        if (headGroupId.HasValue) cmd.Parameters.Add("?", OleDbType.Integer).Value = headGroupId.Value;
        if (brandId.HasValue) cmd.Parameters.Add("?", OleDbType.Integer).Value = brandId.Value;
        if (itemId.HasValue) cmd.Parameters.Add("?", OleDbType.Integer).Value = itemId.Value;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var itm = new Item
            {
                Id = Convert.ToInt32(reader["Id"]),
                HeadGroupId = reader["HeadGroupId"] != DBNull.Value ? Convert.ToInt32(reader["HeadGroupId"]) : 0,
                HeadGroupName = reader["HeadGroupName"]?.ToString() ?? string.Empty,
                BrandId = reader["BrandId"] != DBNull.Value ? Convert.ToInt32(reader["BrandId"]) : 0,
                BrandName = reader["BrandName"]?.ToString() ?? string.Empty,
                ItemCode = reader["ItemCode"]?.ToString() ?? string.Empty,
                ItemName = reader["ItemName"]?.ToString() ?? string.Empty,
                DefaultRatePerKg = reader["DefaultRatePerKg"] != DBNull.Value ? Convert.ToDouble(reader["DefaultRatePerKg"]) : 0.0
            };
            items[itm.Id] = itm;
        }

        return items;
    }

    private Dictionary<int, SaleLineAggregate> LoadSalesAggregates(OleDbConnection conn, DateTime? fromDate, DateTime? toDate, int? itemId)
    {
        var map = new Dictionary<int, SaleLineAggregate>();
        var sql = @"
            SELECT si.ItemId, si.BagsSold, si.LooseKgSold, si.TotalKgSold, si.Amount, s.SaleDate
            FROM SaleItems si
            INNER JOIN Sales s ON si.SaleId = s.Id
            WHERE 1=1";

        if (fromDate.HasValue) sql += " AND s.SaleDate >= ?";
        if (toDate.HasValue) sql += " AND s.SaleDate <= ?";
        if (itemId.HasValue) sql += " AND si.ItemId = ?";

        using var cmd = new OleDbCommand(sql, conn);
        if (fromDate.HasValue) cmd.Parameters.Add("?", OleDbType.Date).Value = fromDate.Value.Date;
        if (toDate.HasValue) cmd.Parameters.Add("?", OleDbType.Date).Value = toDate.Value.Date.AddDays(1).AddTicks(-1);
        if (itemId.HasValue) cmd.Parameters.Add("?", OleDbType.Integer).Value = itemId.Value;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            int itmId = Convert.ToInt32(reader["ItemId"]);
            int bags = Convert.ToInt32(reader["BagsSold"]);
            double looseKg = reader["LooseKgSold"] != DBNull.Value ? Convert.ToDouble(reader["LooseKgSold"]) : 0.0;
            double totalKg = Convert.ToDouble(reader["TotalKgSold"]);
            double amount = Convert.ToDouble(reader["Amount"]);

            if (!map.TryGetValue(itmId, out var agg))
            {
                agg = new SaleLineAggregate { ItemId = itmId };
                map[itmId] = agg;
            }

            agg.BagsSold += bags;
            agg.LooseKgSold += looseKg;
            agg.TotalKgSold += totalKg;
            agg.TotalAmount += amount;
        }

        return map;
    }

    private Dictionary<int, PurchaseLineAggregate> LoadPurchasesAggregates(OleDbConnection conn, DateTime? fromDate, DateTime? toDate, int? itemId)
    {
        var map = new Dictionary<int, PurchaseLineAggregate>();
        var sql = @"
            SELECT pi.ItemId, pi.Bags, pi.TotalKg, pi.Amount, p.PurchaseDate
            FROM PurchaseItems pi
            INNER JOIN Purchases p ON pi.PurchaseId = p.Id
            WHERE 1=1";

        if (fromDate.HasValue) sql += " AND p.PurchaseDate >= ?";
        if (toDate.HasValue) sql += " AND p.PurchaseDate <= ?";
        if (itemId.HasValue) sql += " AND pi.ItemId = ?";

        using var cmd = new OleDbCommand(sql, conn);
        if (fromDate.HasValue) cmd.Parameters.Add("?", OleDbType.Date).Value = fromDate.Value.Date;
        if (toDate.HasValue) cmd.Parameters.Add("?", OleDbType.Date).Value = toDate.Value.Date.AddDays(1).AddTicks(-1);
        if (itemId.HasValue) cmd.Parameters.Add("?", OleDbType.Integer).Value = itemId.Value;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            int itmId = Convert.ToInt32(reader["ItemId"]);
            int bags = Convert.ToInt32(reader["Bags"]);
            double totalKg = Convert.ToDouble(reader["TotalKg"]);
            double amount = Convert.ToDouble(reader["Amount"]);

            if (!map.TryGetValue(itmId, out var agg))
            {
                agg = new PurchaseLineAggregate { ItemId = itmId };
                map[itmId] = agg;
            }

            agg.BagsPurchased += bags;
            agg.TotalKgPurchased += totalKg;
            agg.TotalAmount += amount;
        }

        return map;
    }

    private Dictionary<int, PurchaseLineAggregate> LoadHistoricalPurchasesAggregates(OleDbConnection conn, DateTime? upToDate, int? itemId)
    {
        var map = new Dictionary<int, PurchaseLineAggregate>();
        var sql = @"
            SELECT pi.ItemId, pi.Bags, pi.TotalKg, pi.Amount, p.PurchaseDate
            FROM PurchaseItems pi
            INNER JOIN Purchases p ON pi.PurchaseId = p.Id
            WHERE 1=1";

        if (upToDate.HasValue) sql += " AND p.PurchaseDate <= ?";
        if (itemId.HasValue) sql += " AND pi.ItemId = ?";

        using var cmd = new OleDbCommand(sql, conn);
        if (upToDate.HasValue) cmd.Parameters.Add("?", OleDbType.Date).Value = upToDate.Value.Date.AddDays(1).AddTicks(-1);
        if (itemId.HasValue) cmd.Parameters.Add("?", OleDbType.Integer).Value = itemId.Value;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            int itmId = Convert.ToInt32(reader["ItemId"]);
            int bags = Convert.ToInt32(reader["Bags"]);
            double totalKg = Convert.ToDouble(reader["TotalKg"]);
            double amount = Convert.ToDouble(reader["Amount"]);

            if (!map.TryGetValue(itmId, out var agg))
            {
                agg = new PurchaseLineAggregate { ItemId = itmId };
                map[itmId] = agg;
            }

            agg.BagsPurchased += bags;
            agg.TotalKgPurchased += totalKg;
            agg.TotalAmount += amount;
        }

        return map;
    }
}
