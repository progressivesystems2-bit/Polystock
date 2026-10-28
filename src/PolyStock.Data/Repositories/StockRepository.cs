using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Linq;
using PolyStock.Core.Common;
using PolyStock.Core.Models;

namespace PolyStock.Data.Repositories;

public class RebuildStockResult
{
    public int PurchasesProcessed { get; set; }
    public int SalesProcessed { get; set; }
    public int StockRowsCreated { get; set; }
    public int LedgerEntriesCreated { get; set; }
}

public class StockRepository
{
    private readonly string? _dbPath;
    public StockRepository(string? dbPath = null) => _dbPath = dbPath;

    public List<StockItem> GetAllStock()
    {
        var list = new List<StockItem>();
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        var sql = @"
            SELECT s.Id, s.ItemId, i.ItemName, hg.[Name] AS HeadGroupName, b.[Name] AS BrandName,
                   s.GodownId, s.GodownName, s.PackSizeKg, s.FullBags, s.LooseKg, s.TotalKg, i.DefaultRatePerKg, s.LastUpdated
            FROM ((Stock s
            INNER JOIN Items i ON s.ItemId = i.Id)
            LEFT JOIN HeadGroups hg ON i.HeadGroupId = hg.Id)
            LEFT JOIN Brands b ON i.BrandId = b.Id
            ORDER BY i.ItemName, s.PackSizeKg DESC";

        using var cmd = new OleDbCommand(sql, conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            int bags = Convert.ToInt32(reader["FullBags"]);
            double packSize = Convert.ToDouble(reader["PackSizeKg"]);
            double totalKg = bags * packSize;

            list.Add(new StockItem
            {
                Id = Convert.ToInt32(reader["Id"]),
                ItemId = Convert.ToInt32(reader["ItemId"]),
                ItemName = reader["ItemName"]?.ToString() ?? string.Empty,
                HeadGroupName = reader["HeadGroupName"]?.ToString() ?? string.Empty,
                BrandName = reader["BrandName"]?.ToString() ?? string.Empty,
                GodownId = reader["GodownId"] != DBNull.Value ? Convert.ToInt32(reader["GodownId"]) : null,
                GodownName = reader["GodownName"]?.ToString(),
                PackSizeKg = packSize,
                FullBags = bags,
                LooseKg = 0.0,
                TotalKg = totalKg,
                RatePerKg = reader["DefaultRatePerKg"] != DBNull.Value ? Convert.ToDouble(reader["DefaultRatePerKg"]) : 0.0,
                LastUpdated = reader["LastUpdated"] != DBNull.Value ? Convert.ToDateTime(reader["LastUpdated"]) : DateTime.MinValue
            });
        }
        return list;
    }

    public StockItem? GetStock(int itemId, double packSizeKg, OleDbConnection? conn = null, OleDbTransaction? tx = null)
    {
        bool shouldClose = false;
        if (conn == null)
        {
            conn = AccessConnectionProvider.CreateConnection(_dbPath);
            conn.Open();
            shouldClose = true;
        }

        try
        {
            var sql = @"
                SELECT s.ItemId, MIN(i.ItemName) AS ItemName, s.PackSizeKg,
                       SUM(s.FullBags) AS TotalFullBags, SUM(s.TotalKg) AS SumTotalKg,
                       MIN(i.DefaultRatePerKg) AS DefaultRatePerKg, MAX(s.LastUpdated) AS MaxLastUpdated
                FROM Stock s
                INNER JOIN Items i ON s.ItemId = i.Id
                WHERE s.ItemId = ? AND ABS(s.PackSizeKg - ?) < 0.001
                GROUP BY s.ItemId, s.PackSizeKg";

            using var cmd = new OleDbCommand(sql, conn, tx);
            cmd.Parameters.Add("?", OleDbType.Integer).Value = itemId;
            cmd.Parameters.Add("?", OleDbType.Double).Value = packSizeKg;

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                int bags = reader["TotalFullBags"] != DBNull.Value ? Convert.ToInt32(reader["TotalFullBags"]) : 0;
                double packSize = Convert.ToDouble(reader["PackSizeKg"]);
                return new StockItem
                {
                    ItemId = Convert.ToInt32(reader["ItemId"]),
                    ItemName = reader["ItemName"]?.ToString() ?? string.Empty,
                    PackSizeKg = packSize,
                    FullBags = bags,
                    LooseKg = 0.0,
                    TotalKg = bags * packSize,
                    RatePerKg = reader["DefaultRatePerKg"] != DBNull.Value ? Convert.ToDouble(reader["DefaultRatePerKg"]) : 0.0,
                    LastUpdated = reader["MaxLastUpdated"] != DBNull.Value ? Convert.ToDateTime(reader["MaxLastUpdated"]) : DateTime.MinValue
                };
            }
            return null;
        }
        finally
        {
            if (shouldClose) conn.Dispose();
        }
    }

    public StockItem? GetStock(int itemId, double packSizeKg, int? godownId, OleDbConnection? conn = null, OleDbTransaction? tx = null)
    {
        if (!godownId.HasValue || godownId.Value <= 0)
        {
            return GetStock(itemId, packSizeKg, conn, tx);
        }

        bool shouldClose = false;
        if (conn == null)
        {
            conn = AccessConnectionProvider.CreateConnection(_dbPath);
            conn.Open();
            shouldClose = true;
        }

        try
        {
            // First check godown-specific stock
            var sql = @"
                SELECT s.Id, s.ItemId, i.ItemName, s.GodownId, s.GodownName, s.PackSizeKg, s.FullBags, s.LooseKg, s.TotalKg, i.DefaultRatePerKg, s.LastUpdated
                FROM Stock s
                INNER JOIN Items i ON s.ItemId = i.Id
                WHERE s.ItemId = ? AND ABS(s.PackSizeKg - ?) < 0.001 AND s.GodownId = ?";

            using (var cmd = new OleDbCommand(sql, conn, tx))
            {
                cmd.Parameters.Add("?", OleDbType.Integer).Value = itemId;
                cmd.Parameters.Add("?", OleDbType.Double).Value = packSizeKg;
                cmd.Parameters.Add("?", OleDbType.Integer).Value = godownId.Value;

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    int bags = Convert.ToInt32(reader["FullBags"]);
                    double packSize = Convert.ToDouble(reader["PackSizeKg"]);
                    return new StockItem
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        ItemId = Convert.ToInt32(reader["ItemId"]),
                        ItemName = reader["ItemName"]?.ToString() ?? string.Empty,
                        GodownId = reader["GodownId"] != DBNull.Value ? Convert.ToInt32(reader["GodownId"]) : null,
                        GodownName = reader["GodownName"]?.ToString(),
                        PackSizeKg = packSize,
                        FullBags = bags,
                        LooseKg = 0.0,
                        TotalKg = bags * packSize,
                        RatePerKg = reader["DefaultRatePerKg"] != DBNull.Value ? Convert.ToDouble(reader["DefaultRatePerKg"]) : 0.0,
                        LastUpdated = reader["LastUpdated"] != DBNull.Value ? Convert.ToDateTime(reader["LastUpdated"]) : DateTime.MinValue
                    };
                }
            }

            return null;
        }
        finally
        {
            if (shouldClose) conn.Dispose();
        }
    }

    public StockItem? GetUnassignedStock(int itemId, double packSizeKg, OleDbConnection? conn = null, OleDbTransaction? tx = null)
    {
        bool shouldClose = false;
        if (conn == null)
        {
            conn = AccessConnectionProvider.CreateConnection(_dbPath);
            conn.Open();
            shouldClose = true;
        }

        try
        {
            var fallbackSql = @"
                SELECT s.Id, s.ItemId, i.ItemName, s.GodownId, s.GodownName, s.PackSizeKg, s.FullBags, s.LooseKg, s.TotalKg, i.DefaultRatePerKg, s.LastUpdated
                FROM Stock s
                INNER JOIN Items i ON s.ItemId = i.Id
                WHERE s.ItemId = ? AND ABS(s.PackSizeKg - ?) < 0.001 AND (s.GodownId IS NULL OR s.GodownId = 0)";

            using var cmdFallback = new OleDbCommand(fallbackSql, conn, tx);
            cmdFallback.Parameters.Add("?", OleDbType.Integer).Value = itemId;
            cmdFallback.Parameters.Add("?", OleDbType.Double).Value = packSizeKg;

            using var readerFallback = cmdFallback.ExecuteReader();
            if (readerFallback.Read())
            {
                int bags = Convert.ToInt32(readerFallback["FullBags"]);
                double packSize = Convert.ToDouble(readerFallback["PackSizeKg"]);
                return new StockItem
                {
                    Id = Convert.ToInt32(readerFallback["Id"]),
                    ItemId = Convert.ToInt32(readerFallback["ItemId"]),
                    ItemName = readerFallback["ItemName"]?.ToString() ?? string.Empty,
                    GodownId = readerFallback["GodownId"] != DBNull.Value ? Convert.ToInt32(readerFallback["GodownId"]) : null,
                    GodownName = readerFallback["GodownName"]?.ToString(),
                    PackSizeKg = packSize,
                    FullBags = bags,
                    LooseKg = 0.0,
                    TotalKg = bags * packSize,
                    RatePerKg = readerFallback["DefaultRatePerKg"] != DBNull.Value ? Convert.ToDouble(readerFallback["DefaultRatePerKg"]) : 0.0,
                    LastUpdated = readerFallback["LastUpdated"] != DBNull.Value ? Convert.ToDateTime(readerFallback["LastUpdated"]) : DateTime.MinValue
                };
            }
            return null;
        }
        finally
        {
            if (shouldClose) conn.Dispose();
        }
    }

    public List<StockItem> GetStockByGodown(int godownId)
    {
        var list = new List<StockItem>();
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        var sql = @"
            SELECT s.Id, s.ItemId, i.ItemName, hg.[Name] AS HeadGroupName, b.[Name] AS BrandName,
                   s.GodownId, s.GodownName, s.PackSizeKg, s.FullBags, s.LooseKg, s.TotalKg, i.DefaultRatePerKg, s.LastUpdated
            FROM ((Stock s
            INNER JOIN Items i ON s.ItemId = i.Id)
            LEFT JOIN HeadGroups hg ON i.HeadGroupId = hg.Id)
            LEFT JOIN Brands b ON i.BrandId = b.Id
            WHERE (s.GodownId = ? OR (? = 0 AND (s.GodownId IS NULL OR s.GodownId = 0)))
            ORDER BY i.ItemName, s.PackSizeKg DESC";

        using var cmd = new OleDbCommand(sql, conn);
        cmd.Parameters.Add("?", OleDbType.Integer).Value = godownId;
        cmd.Parameters.Add("?", OleDbType.Integer).Value = godownId;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            int bags = Convert.ToInt32(reader["FullBags"]);
            double packSize = Convert.ToDouble(reader["PackSizeKg"]);
            list.Add(new StockItem
            {
                Id = Convert.ToInt32(reader["Id"]),
                ItemId = Convert.ToInt32(reader["ItemId"]),
                ItemName = reader["ItemName"]?.ToString() ?? string.Empty,
                HeadGroupName = reader["HeadGroupName"]?.ToString() ?? string.Empty,
                BrandName = reader["BrandName"]?.ToString() ?? string.Empty,
                GodownId = reader["GodownId"] != DBNull.Value ? Convert.ToInt32(reader["GodownId"]) : null,
                GodownName = reader["GodownName"]?.ToString(),
                PackSizeKg = packSize,
                FullBags = bags,
                LooseKg = 0.0,
                TotalKg = bags * packSize,
                RatePerKg = reader["DefaultRatePerKg"] != DBNull.Value ? Convert.ToDouble(reader["DefaultRatePerKg"]) : 0.0,
                LastUpdated = reader["LastUpdated"] != DBNull.Value ? Convert.ToDateTime(reader["LastUpdated"]) : DateTime.MinValue
            });
        }
        return list;
    }

    public void UpsertStockAndLedger(
        int itemId,
        string itemName,
        double packSizeKg,
        int newFullBags,
        double newLooseKg,
        double newTotalKg,
        string transactionType,
        string refInvoiceNo,
        int fullBagsChange,
        double looseKgChange,
        double totalKgChange,
        string remarks,
        OleDbConnection conn,
        OleDbTransaction tx,
        int? godownId = null,
        string? godownName = null)
    {
        double actualTotalKg = newFullBags * packSizeKg;

        // 1. Check if Stock row exists (either for specific godown or unassigned)
        bool hasGodown = godownId.HasValue && godownId.Value > 0;
        bool exists = false;

        string checkSql = hasGodown
            ? "SELECT COUNT(*) FROM Stock WHERE ItemId = ? AND ABS(PackSizeKg - ?) < 0.001 AND GodownId = ?"
            : "SELECT COUNT(*) FROM Stock WHERE ItemId = ? AND ABS(PackSizeKg - ?) < 0.001 AND (GodownId IS NULL OR GodownId = 0)";

        using (var checkCmd = new OleDbCommand(checkSql, conn, tx))
        {
            checkCmd.Parameters.Add("?", OleDbType.Integer).Value = itemId;
            checkCmd.Parameters.Add("?", OleDbType.Double).Value = packSizeKg;
            if (hasGodown)
            {
                checkCmd.Parameters.Add("?", OleDbType.Integer).Value = godownId!.Value;
            }
            exists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;
        }

        if (!exists)
        {
            if (hasGodown)
            {
                using var insCmd = new OleDbCommand(@"
                    INSERT INTO Stock (ItemId, PackSizeKg, FullBags, LooseKg, TotalKg, GodownId, GodownName, LastUpdated)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?)", conn, tx);
                insCmd.Parameters.Add("?", OleDbType.Integer).Value = itemId;
                insCmd.Parameters.Add("?", OleDbType.Double).Value = packSizeKg;
                insCmd.Parameters.Add("?", OleDbType.Integer).Value = newFullBags;
                insCmd.Parameters.Add("?", OleDbType.Double).Value = 0.0;
                insCmd.Parameters.Add("?", OleDbType.Double).Value = actualTotalKg;
                insCmd.Parameters.Add("?", OleDbType.Integer).Value = godownId!.Value;
                insCmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)godownName ?? DBNull.Value;
                insCmd.Parameters.Add("?", OleDbType.Date).Value = DateTime.Now;
                insCmd.ExecuteNonQuery();
            }
            else
            {
                using var insCmd = new OleDbCommand(@"
                    INSERT INTO Stock (ItemId, PackSizeKg, FullBags, LooseKg, TotalKg, LastUpdated)
                    VALUES (?, ?, ?, ?, ?, ?)", conn, tx);
                insCmd.Parameters.Add("?", OleDbType.Integer).Value = itemId;
                insCmd.Parameters.Add("?", OleDbType.Double).Value = packSizeKg;
                insCmd.Parameters.Add("?", OleDbType.Integer).Value = newFullBags;
                insCmd.Parameters.Add("?", OleDbType.Double).Value = 0.0;
                insCmd.Parameters.Add("?", OleDbType.Double).Value = actualTotalKg;
                insCmd.Parameters.Add("?", OleDbType.Date).Value = DateTime.Now;
                insCmd.ExecuteNonQuery();
            }
        }
        else
        {
            if (hasGodown)
            {
                using var updateCmd = new OleDbCommand(@"
                    UPDATE Stock 
                    SET FullBags = ?, LooseKg = 0, TotalKg = ?, GodownName = ?, LastUpdated = ?
                    WHERE ItemId = ? AND ABS(PackSizeKg - ?) < 0.001 AND GodownId = ?", conn, tx);
                updateCmd.Parameters.Add("?", OleDbType.Integer).Value = newFullBags;
                updateCmd.Parameters.Add("?", OleDbType.Double).Value = actualTotalKg;
                updateCmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)godownName ?? DBNull.Value;
                updateCmd.Parameters.Add("?", OleDbType.Date).Value = DateTime.Now;
                updateCmd.Parameters.Add("?", OleDbType.Integer).Value = itemId;
                updateCmd.Parameters.Add("?", OleDbType.Double).Value = packSizeKg;
                updateCmd.Parameters.Add("?", OleDbType.Integer).Value = godownId!.Value;
                updateCmd.ExecuteNonQuery();
            }
            else
            {
                using var updateCmd = new OleDbCommand(@"
                    UPDATE Stock 
                    SET FullBags = ?, LooseKg = 0, TotalKg = ?, LastUpdated = ?
                    WHERE ItemId = ? AND ABS(PackSizeKg - ?) < 0.001 AND (GodownId IS NULL OR GodownId = 0)", conn, tx);
                updateCmd.Parameters.Add("?", OleDbType.Integer).Value = newFullBags;
                updateCmd.Parameters.Add("?", OleDbType.Double).Value = actualTotalKg;
                updateCmd.Parameters.Add("?", OleDbType.Date).Value = DateTime.Now;
                updateCmd.Parameters.Add("?", OleDbType.Integer).Value = itemId;
                updateCmd.Parameters.Add("?", OleDbType.Double).Value = packSizeKg;
                updateCmd.ExecuteNonQuery();
            }
        }

        // 2. Insert StockLedger entry
        using var ledgerCmd = new OleDbCommand(@"
            INSERT INTO StockLedger (
                ItemId, GodownId, GodownName, PackSizeKg, TransactionType, RefInvoiceNo,
                FullBagsChange, LooseKgChange, TotalKgChange,
                BalanceFullBags, BalanceLooseKg, BalanceTotalKg,
                Remarks, [Timestamp]
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)", conn, tx);

        ledgerCmd.Parameters.Add("?", OleDbType.Integer).Value = itemId;
        ledgerCmd.Parameters.Add("?", OleDbType.Integer).Value = (object?)godownId ?? DBNull.Value;
        ledgerCmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)godownName ?? DBNull.Value;
        ledgerCmd.Parameters.Add("?", OleDbType.Double).Value = packSizeKg;
        ledgerCmd.Parameters.Add("?", OleDbType.VarWChar).Value = transactionType;
        ledgerCmd.Parameters.Add("?", OleDbType.VarWChar).Value = refInvoiceNo ?? string.Empty;
        ledgerCmd.Parameters.Add("?", OleDbType.Integer).Value = fullBagsChange;
        ledgerCmd.Parameters.Add("?", OleDbType.Double).Value = 0.0;
        ledgerCmd.Parameters.Add("?", OleDbType.Double).Value = totalKgChange;
        ledgerCmd.Parameters.Add("?", OleDbType.Integer).Value = newFullBags;
        ledgerCmd.Parameters.Add("?", OleDbType.Double).Value = 0.0;
        ledgerCmd.Parameters.Add("?", OleDbType.Double).Value = actualTotalKg;
        ledgerCmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)remarks ?? DBNull.Value;
        ledgerCmd.Parameters.Add("?", OleDbType.Date).Value = DateTime.Now;
        ledgerCmd.ExecuteNonQuery();
    }

    public void ApplySaleBagMorph(
        int itemId,
        string itemName,
        double sourcePackSizeKg,
        int sourceBagsDeducted,
        double morphedPackSizeKg,
        int morphedBagsAdded,
        double totalKgSold,
        string invoiceNo,
        string partyName,
        int? godownId,
        string? godownName,
        OleDbConnection conn,
        OleDbTransaction tx)
    {
        // 1. Deduct source bags
        var sourceStock = (godownId.HasValue && godownId.Value > 0)
            ? (GetStock(itemId, sourcePackSizeKg, godownId, conn, tx) ?? GetStock(itemId, sourcePackSizeKg, conn, tx))
            : GetStock(itemId, sourcePackSizeKg, conn, tx);

        int oldSourceBags = sourceStock?.FullBags ?? 0;
        int newSourceBags = oldSourceBags - sourceBagsDeducted;
        if (newSourceBags < 0) newSourceBags = 0;
        double newSourceTotalKg = newSourceBags * sourcePackSizeKg;

        string sourceRemarks = morphedBagsAdded > 0
            ? $"Sold {totalKgSold:0.##} kg from {sourcePackSizeKg:0.##} kg bag to {partyName} (Cut bag -> Created 1 Bag of {morphedPackSizeKg:0.##} kg)"
            : $"Sold {sourceBagsDeducted} bag(s) of {sourcePackSizeKg:0.##} kg to {partyName}";

        UpsertStockAndLedger(
            itemId,
            itemName,
            sourcePackSizeKg,
            newSourceBags,
            0.0,
            newSourceTotalKg,
            "SALE",
            invoiceNo,
            -sourceBagsDeducted,
            0.0,
            -(sourceBagsDeducted * sourcePackSizeKg),
            sourceRemarks,
            conn,
            tx,
            godownId,
            godownName);

        // 2. Add morphed bag if remainder exists
        if (morphedBagsAdded > 0 && morphedPackSizeKg > 0.0001)
        {
            var morphedStock = (godownId.HasValue && godownId.Value > 0)
                ? (GetStock(itemId, morphedPackSizeKg, godownId, conn, tx) ?? GetStock(itemId, morphedPackSizeKg, conn, tx))
                : GetStock(itemId, morphedPackSizeKg, conn, tx);

            int oldMorphedBags = morphedStock?.FullBags ?? 0;
            int newMorphedBags = oldMorphedBags + morphedBagsAdded;
            double newMorphedTotalKg = newMorphedBags * morphedPackSizeKg;

            UpsertStockAndLedger(
                itemId,
                itemName,
                morphedPackSizeKg,
                newMorphedBags,
                0.0,
                newMorphedTotalKg,
                "BAG_MORPH",
                invoiceNo,
                morphedBagsAdded,
                0.0,
                morphedBagsAdded * morphedPackSizeKg,
                $"Created 1 Bag of {morphedPackSizeKg:0.##} kg (Reduced remainder from {sourcePackSizeKg:0.##} kg bag on Bill #{invoiceNo})",
                conn,
                tx,
                godownId,
                godownName);
        }
    }

    public void RevertSaleBagMorph(
        int itemId,
        string itemName,
        double sourcePackSizeKg,
        int sourceBagsDeducted,
        double morphedPackSizeKg,
        int morphedBagsAdded,
        string invoiceNo,
        OleDbConnection conn,
        OleDbTransaction tx)
    {
        // 1. Add back source bags
        var sourceStock = GetStock(itemId, sourcePackSizeKg, conn, tx);
        int oldSourceBags = sourceStock?.FullBags ?? 0;
        int newSourceBags = oldSourceBags + sourceBagsDeducted;
        double newSourceTotalKg = newSourceBags * sourcePackSizeKg;

        UpsertStockAndLedger(
            itemId,
            itemName,
            sourcePackSizeKg,
            newSourceBags,
            0.0,
            newSourceTotalKg,
            "SALE_REVERT",
            invoiceNo,
            sourceBagsDeducted,
            0.0,
            sourceBagsDeducted * sourcePackSizeKg,
            $"Reverting sale line for edit #{invoiceNo}",
            conn,
            tx);

        // 2. Remove morphed bag
        if (morphedBagsAdded > 0 && morphedPackSizeKg > 0.0001)
        {
            var morphedStock = GetStock(itemId, morphedPackSizeKg, conn, tx);
            int oldMorphedBags = morphedStock?.FullBags ?? 0;
            int newMorphedBags = oldMorphedBags - morphedBagsAdded;
            if (newMorphedBags < 0) newMorphedBags = 0;
            double newMorphedTotalKg = newMorphedBags * morphedPackSizeKg;

            UpsertStockAndLedger(
                itemId,
                itemName,
                morphedPackSizeKg,
                newMorphedBags,
                0.0,
                newMorphedTotalKg,
                "MORPH_REVERT",
                invoiceNo,
                -morphedBagsAdded,
                0.0,
                -(morphedBagsAdded * morphedPackSizeKg),
                $"Reverting morphed {morphedPackSizeKg:0.##} kg bag for edit #{invoiceNo}",
                conn,
                tx);
        }
    }

    public List<StockLedgerEntry> GetLedgerEntries(int? itemId = null, double? packSizeKg = null)
    {
        var list = new List<StockLedgerEntry>();
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        var sql = @"
            SELECT sl.Id, sl.ItemId, i.ItemName, sl.GodownId, sl.GodownName, sl.PackSizeKg, sl.TransactionType, sl.RefInvoiceNo,
                   sl.FullBagsChange, sl.LooseKgChange, sl.TotalKgChange,
                   sl.BalanceFullBags, sl.BalanceLooseKg, sl.BalanceTotalKg,
                   sl.Remarks, sl.[Timestamp]
            FROM StockLedger sl
            LEFT JOIN Items i ON sl.ItemId = i.Id
            WHERE 1=1 ";

        if (itemId.HasValue) sql += " AND sl.ItemId = " + itemId.Value;
        if (packSizeKg.HasValue) sql += " AND ABS(sl.PackSizeKg - " + packSizeKg.Value + ") < 0.001 ";
        sql += " ORDER BY sl.[Timestamp] DESC, sl.Id DESC";

        using var cmd = new OleDbCommand(sql, conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new StockLedgerEntry
            {
                Id = Convert.ToInt32(reader["Id"]),
                ItemId = Convert.ToInt32(reader["ItemId"]),
                ItemName = reader["ItemName"]?.ToString() ?? string.Empty,
                GodownId = reader["GodownId"] != DBNull.Value ? Convert.ToInt32(reader["GodownId"]) : null,
                GodownName = reader["GodownName"]?.ToString(),
                PackSizeKg = Convert.ToDouble(reader["PackSizeKg"]),
                TransactionType = reader["TransactionType"]?.ToString() ?? string.Empty,
                RefInvoiceNo = reader["RefInvoiceNo"]?.ToString() ?? string.Empty,
                FullBagsChange = Convert.ToInt32(reader["FullBagsChange"]),
                LooseKgChange = 0.0,
                TotalKgChange = Convert.ToDouble(reader["TotalKgChange"]),
                BalanceFullBags = Convert.ToInt32(reader["BalanceFullBags"]),
                BalanceLooseKg = 0.0,
                BalanceTotalKg = Convert.ToDouble(reader["BalanceTotalKg"]),
                Remarks = reader["Remarks"]?.ToString(),
                Timestamp = Convert.ToDateTime(reader["Timestamp"])
            });
        }
        return list;
    }

    /// <summary>
    /// Completely rebuilds the Stock and StockLedger tables from all raw Purchase and Sale vouchers chronologically
    /// using the Dynamic Morphing Bag Identity model.
    /// </summary>
    public RebuildStockResult RebuildStockFromVouchers()
    {
        var result = new RebuildStockResult();
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        using var tx = conn.BeginTransaction();

        try
        {
            var settingsRepo = new SettingsRepository(_dbPath);
            double thresholdKg = settingsRepo.GetSalesDeductionThresholdKg(conn, tx);

            // 1. Clear existing Stock and StockLedger
            using (var clearStockCmd = new OleDbCommand("DELETE FROM Stock", conn, tx))
            {
                clearStockCmd.ExecuteNonQuery();
            }

            using (var clearLedgerCmd = new OleDbCommand("DELETE FROM StockLedger", conn, tx))
            {
                clearLedgerCmd.ExecuteNonQuery();
            }

            // 2. Load all chronologically ordered voucher items
            var events = new List<StockEvent>();

            // Purchases
            var purSql = @"
                SELECT p.Id AS VoucherId, p.InvoiceNo, p.SupplierName AS PartyName, p.PurchaseDate AS EventDate,
                       pi.ItemId, i.ItemName, pi.GodownId, pi.GodownName, pi.PackSizeKg, pi.Bags, pi.TotalKg, p.CreatedAt
                FROM (Purchases p
                INNER JOIN PurchaseItems pi ON p.Id = pi.PurchaseId)
                LEFT JOIN Items i ON pi.ItemId = i.Id
                ORDER BY p.PurchaseDate ASC, p.Id ASC";

            using (var cmd = new OleDbCommand(purSql, conn, tx))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    events.Add(new StockEvent
                    {
                        IsPurchase = true,
                        VoucherId = Convert.ToInt32(reader["VoucherId"]),
                        InvoiceNo = reader["InvoiceNo"]?.ToString() ?? string.Empty,
                        PartyName = reader["PartyName"]?.ToString() ?? string.Empty,
                        EventDate = Convert.ToDateTime(reader["EventDate"]),
                        CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue,
                        ItemId = Convert.ToInt32(reader["ItemId"]),
                        ItemName = reader["ItemName"]?.ToString() ?? string.Empty,
                        GodownId = reader["GodownId"] != DBNull.Value ? Convert.ToInt32(reader["GodownId"]) : null,
                        GodownName = reader["GodownName"]?.ToString(),
                        PackSizeKg = Convert.ToDouble(reader["PackSizeKg"]),
                        Bags = Convert.ToInt32(reader["Bags"]),
                        LooseKg = 0.0,
                        TotalKg = Convert.ToDouble(reader["TotalKg"])
                    });
                }
            }

            // Sales
            var saleSql = @"
                SELECT s.Id AS VoucherId, s.InvoiceNo, s.CustomerName AS PartyName, s.SaleDate AS EventDate,
                       si.ItemId, i.ItemName, si.GodownId, si.GodownName, si.PackSizeKg, si.BagsSold, si.LooseKgSold, si.TotalKgSold, si.SkipStockDeduction, s.CreatedAt
                FROM (Sales s
                INNER JOIN SaleItems si ON s.Id = si.SaleId)
                LEFT JOIN Items i ON si.ItemId = i.Id
                ORDER BY s.SaleDate ASC, s.Id ASC";

            using (var cmd = new OleDbCommand(saleSql, conn, tx))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    events.Add(new StockEvent
                    {
                        IsPurchase = false,
                        VoucherId = Convert.ToInt32(reader["VoucherId"]),
                        InvoiceNo = reader["InvoiceNo"]?.ToString() ?? string.Empty,
                        PartyName = reader["PartyName"]?.ToString() ?? string.Empty,
                        EventDate = Convert.ToDateTime(reader["EventDate"]),
                        CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue,
                        ItemId = Convert.ToInt32(reader["ItemId"]),
                        ItemName = reader["ItemName"]?.ToString() ?? string.Empty,
                        GodownId = reader["GodownId"] != DBNull.Value ? Convert.ToInt32(reader["GodownId"]) : null,
                        GodownName = reader["GodownName"]?.ToString(),
                        PackSizeKg = Convert.ToDouble(reader["PackSizeKg"]),
                        Bags = Convert.ToInt32(reader["BagsSold"]),
                        LooseKg = Convert.ToDouble(reader["LooseKgSold"]),
                        TotalKg = Convert.ToDouble(reader["TotalKgSold"]),
                        SkipStockDeduction = reader["SkipStockDeduction"] != DBNull.Value && Convert.ToBoolean(reader["SkipStockDeduction"])
                    });
                }
            }

            // Sort all events chronologically
            var sortedEvents = events.OrderBy(e => e.EventDate).ThenBy(e => e.CreatedAt).ThenBy(e => e.VoucherId).ToList();

            // Track live state: key = (ItemId, PackSizeKg, GodownId, GodownName) -> FullBags
            var balanceTracker = new Dictionary<(int ItemId, double PackSizeKg, int? GodownId, string? GodownName), int>();

            foreach (var ev in sortedEvents)
            {
                var sourceKey = (ev.ItemId, ev.PackSizeKg, ev.GodownId, ev.GodownName);
                if (!balanceTracker.ContainsKey(sourceKey))
                {
                    balanceTracker[sourceKey] = 0;
                }

                if (ev.IsPurchase)
                {
                    result.PurchasesProcessed++;
                    int currentBags = balanceTracker[sourceKey];
                    int newBags = currentBags + ev.Bags;
                    balanceTracker[sourceKey] = newBags;
                    double newTotalKg = newBags * ev.PackSizeKg;

                    InsertLedgerDirect(conn, tx, ev.ItemId, ev.PackSizeKg, ev.GodownId, ev.GodownName,
                        "PURCHASE", ev.InvoiceNo, ev.Bags, 0.0, ev.TotalKg,
                        newBags, 0.0, newTotalKg,
                        $"Purchased {ev.Bags} bags from {ev.PartyName}", ev.EventDate);

                    result.LedgerEntriesCreated++;
                }
                else
                {
                    result.SalesProcessed++;
                    bool skipDeduction = ev.SkipStockDeduction || !StockCalculationEngine.ShouldDeductInventory(ev.TotalKg, thresholdKg);
                    if (skipDeduction)
                    {
                        continue;
                    }

                    int currentBags = balanceTracker[sourceKey];
                    var calc = StockCalculationEngine.CalculateBagMorph(currentBags, ev.PackSizeKg, ev.Bags, ev.LooseKg);

                    int newSourceBags = calc.SourceBagsRemaining;
                    balanceTracker[sourceKey] = newSourceBags;
                    double newSourceTotalKg = newSourceBags * ev.PackSizeKg;

                    string sourceRemarks = calc.MorphedBagsAdded > 0
                        ? $"Sold {calc.TotalKgSold:0.##} kg from {ev.PackSizeKg:0.##} kg bag to {ev.PartyName} (Cut bag -> Created 1 Bag of {calc.MorphedPackSizeKg:0.##} kg)"
                        : $"Sold {calc.SourceBagsDeducted} bag(s) of {ev.PackSizeKg:0.##} kg to {ev.PartyName}";

                    InsertLedgerDirect(conn, tx, ev.ItemId, ev.PackSizeKg, ev.GodownId, ev.GodownName,
                        "SALE", ev.InvoiceNo, -calc.SourceBagsDeducted, 0.0, -(calc.SourceBagsDeducted * ev.PackSizeKg),
                        newSourceBags, 0.0, newSourceTotalKg,
                        sourceRemarks, ev.EventDate);

                    result.LedgerEntriesCreated++;

                    if (calc.MorphedBagsAdded > 0 && calc.MorphedPackSizeKg > 0.0001)
                    {
                        var morphedKey = (ev.ItemId, calc.MorphedPackSizeKg, ev.GodownId, ev.GodownName);
                        if (!balanceTracker.ContainsKey(morphedKey))
                        {
                            balanceTracker[morphedKey] = 0;
                        }

                        int currentMorphedBags = balanceTracker[morphedKey];
                        int newMorphedBags = currentMorphedBags + calc.MorphedBagsAdded;
                        balanceTracker[morphedKey] = newMorphedBags;
                        double newMorphedTotalKg = newMorphedBags * calc.MorphedPackSizeKg;

                        InsertLedgerDirect(conn, tx, ev.ItemId, calc.MorphedPackSizeKg, ev.GodownId, ev.GodownName,
                            "BAG_MORPH", ev.InvoiceNo, calc.MorphedBagsAdded, 0.0, calc.MorphedBagsAdded * calc.MorphedPackSizeKg,
                            newMorphedBags, 0.0, newMorphedTotalKg,
                            $"Created 1 Bag of {calc.MorphedPackSizeKg:0.##} kg (Reduced remainder from {ev.PackSizeKg:0.##} kg bag on Bill #{ev.InvoiceNo})", ev.EventDate);

                        result.LedgerEntriesCreated++;
                    }
                }
            }

            // Also load all registered ItemPackSizes to ensure default configured packs exist
            var packSizesSql = "SELECT ItemId, PackSizeKg FROM ItemPackSizes WHERE IsActive = True";
            using (var psCmd = new OleDbCommand(packSizesSql, conn, tx))
            using (var reader = psCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    int iId = Convert.ToInt32(reader["ItemId"]);
                    double ps = Convert.ToDouble(reader["PackSizeKg"]);
                    bool anyForPack = balanceTracker.Keys.Any(k => k.ItemId == iId && Math.Abs(k.PackSizeKg - ps) < 0.001);
                    if (!anyForPack)
                    {
                        balanceTracker[(iId, ps, null, null)] = 0;
                    }
                }
            }

            // 3. Write final Stock records with Godown information
            foreach (var kvp in balanceTracker)
            {
                double packSize = kvp.Key.PackSizeKg;
                int bags = kvp.Value;
                double totalKg = bags * packSize;
                int? gid = kvp.Key.GodownId;
                string? gname = kvp.Key.GodownName;

                if (gid.HasValue && gid.Value > 0)
                {
                    using var insStockCmd = new OleDbCommand(@"
                        INSERT INTO Stock (ItemId, PackSizeKg, FullBags, LooseKg, TotalKg, GodownId, GodownName, LastUpdated)
                        VALUES (?, ?, ?, ?, ?, ?, ?, ?)", conn, tx);

                    insStockCmd.Parameters.Add("?", OleDbType.Integer).Value = kvp.Key.ItemId;
                    insStockCmd.Parameters.Add("?", OleDbType.Double).Value = packSize;
                    insStockCmd.Parameters.Add("?", OleDbType.Integer).Value = bags;
                    insStockCmd.Parameters.Add("?", OleDbType.Double).Value = 0.0;
                    insStockCmd.Parameters.Add("?", OleDbType.Double).Value = totalKg;
                    insStockCmd.Parameters.Add("?", OleDbType.Integer).Value = gid.Value;
                    insStockCmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)gname ?? DBNull.Value;
                    insStockCmd.Parameters.Add("?", OleDbType.Date).Value = DateTime.Now;
                    insStockCmd.ExecuteNonQuery();
                }
                else
                {
                    using var insStockCmd = new OleDbCommand(@"
                        INSERT INTO Stock (ItemId, PackSizeKg, FullBags, LooseKg, TotalKg, LastUpdated)
                        VALUES (?, ?, ?, ?, ?, ?)", conn, tx);

                    insStockCmd.Parameters.Add("?", OleDbType.Integer).Value = kvp.Key.ItemId;
                    insStockCmd.Parameters.Add("?", OleDbType.Double).Value = packSize;
                    insStockCmd.Parameters.Add("?", OleDbType.Integer).Value = bags;
                    insStockCmd.Parameters.Add("?", OleDbType.Double).Value = 0.0;
                    insStockCmd.Parameters.Add("?", OleDbType.Double).Value = totalKg;
                    insStockCmd.Parameters.Add("?", OleDbType.Date).Value = DateTime.Now;
                    insStockCmd.ExecuteNonQuery();
                }
                result.StockRowsCreated++;
            }

            tx.Commit();
            return result;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static void InsertLedgerDirect(
        OleDbConnection conn, OleDbTransaction tx,
        int itemId, double packSizeKg, int? godownId, string? godownName,
        string transactionType, string refInvoiceNo,
        int fullBagsChange, double looseKgChange, double totalKgChange,
        int balFullBags, double balLooseKg, double balTotalKg,
        string remarks, DateTime timestamp)
    {
        using var ledgerCmd = new OleDbCommand(@"
            INSERT INTO StockLedger (
                ItemId, GodownId, GodownName, PackSizeKg, TransactionType, RefInvoiceNo,
                FullBagsChange, LooseKgChange, TotalKgChange,
                BalanceFullBags, BalanceLooseKg, BalanceTotalKg,
                Remarks, [Timestamp]
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)", conn, tx);

        ledgerCmd.Parameters.Add("?", OleDbType.Integer).Value = itemId;
        ledgerCmd.Parameters.Add("?", OleDbType.Integer).Value = (object?)godownId ?? DBNull.Value;
        ledgerCmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)godownName ?? DBNull.Value;
        ledgerCmd.Parameters.Add("?", OleDbType.Double).Value = packSizeKg;
        ledgerCmd.Parameters.Add("?", OleDbType.VarWChar).Value = transactionType;
        ledgerCmd.Parameters.Add("?", OleDbType.VarWChar).Value = refInvoiceNo ?? string.Empty;
        ledgerCmd.Parameters.Add("?", OleDbType.Integer).Value = fullBagsChange;
        ledgerCmd.Parameters.Add("?", OleDbType.Double).Value = 0.0;
        ledgerCmd.Parameters.Add("?", OleDbType.Double).Value = totalKgChange;
        ledgerCmd.Parameters.Add("?", OleDbType.Integer).Value = balFullBags;
        ledgerCmd.Parameters.Add("?", OleDbType.Double).Value = 0.0;
        ledgerCmd.Parameters.Add("?", OleDbType.Double).Value = balTotalKg;
        ledgerCmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)remarks ?? DBNull.Value;
        ledgerCmd.Parameters.Add("?", OleDbType.Date).Value = timestamp;
        ledgerCmd.ExecuteNonQuery();
    }

    private class StockEvent
    {
        public bool IsPurchase { get; set; }
        public int VoucherId { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
        public string PartyName { get; set; } = string.Empty;
        public DateTime EventDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int? GodownId { get; set; }
        public string? GodownName { get; set; }
        public double PackSizeKg { get; set; }
        public int Bags { get; set; }
        public double LooseKg { get; set; }
        public double TotalKg { get; set; }
        public bool SkipStockDeduction { get; set; }
    }
}
