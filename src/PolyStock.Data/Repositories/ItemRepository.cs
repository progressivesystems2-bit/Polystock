using System;
using System.Collections.Generic;
using System.Data.OleDb;
using PolyStock.Core.Models;

namespace PolyStock.Data.Repositories;

public class ItemRepository
{
    private readonly string? _dbPath;
    public ItemRepository(string? dbPath = null) => _dbPath = dbPath;

    public List<Item> GetAll()
    {
        var list = new List<Item>();
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        var sql = @"
            SELECT i.Id, i.HeadGroupId, hg.[Name] AS HeadGroupName, 
                   i.BrandId, b.[Name] AS BrandName, 
                   i.ItemCode, i.ItemName, i.DefaultRatePerKg, i.[Description], i.CreatedAt
            FROM (Items i
            LEFT JOIN HeadGroups hg ON i.HeadGroupId = hg.Id)
            LEFT JOIN Brands b ON i.BrandId = b.Id
            ORDER BY i.ItemName";

        using var cmd = new OleDbCommand(sql, conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Item
            {
                Id = Convert.ToInt32(reader["Id"]),
                HeadGroupId = reader["HeadGroupId"] != DBNull.Value ? Convert.ToInt32(reader["HeadGroupId"]) : 0,
                HeadGroupName = reader["HeadGroupName"]?.ToString() ?? string.Empty,
                BrandId = reader["BrandId"] != DBNull.Value ? Convert.ToInt32(reader["BrandId"]) : 0,
                BrandName = reader["BrandName"]?.ToString() ?? string.Empty,
                ItemCode = reader["ItemCode"]?.ToString() ?? string.Empty,
                ItemName = reader["ItemName"]?.ToString() ?? string.Empty,
                DefaultRatePerKg = reader["DefaultRatePerKg"] != DBNull.Value ? Convert.ToDouble(reader["DefaultRatePerKg"]) : 0.0,
                Description = reader["Description"]?.ToString(),
                CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue
            });
        }

        // Load any known pack sizes for each item from Stock or ItemPackSizes
        foreach (var item in list)
        {
            item.PackSizes = GetKnownPackSizesForItem(conn, item.Id);
        }

        return list;
    }

    public List<double> GetPackSizesForItem(int itemId)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        return GetKnownPackSizesForItem(conn, itemId);
    }

    private List<double> GetKnownPackSizesForItem(OleDbConnection conn, int itemId)
    {
        var sizes = new List<double>();
        // Query both Stock and ItemPackSizes to get all distinct pack sizes recorded for this item
        var sql = @"
            SELECT DISTINCT PackSizeKg FROM (
                SELECT PackSizeKg FROM ItemPackSizes WHERE ItemId = ?
                UNION
                SELECT PackSizeKg FROM Stock WHERE ItemId = ?
            ) ORDER BY PackSizeKg DESC";

        using var cmd = new OleDbCommand(sql, conn);
        cmd.Parameters.Add("?", OleDbType.Integer).Value = itemId;
        cmd.Parameters.Add("?", OleDbType.Integer).Value = itemId;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (reader["PackSizeKg"] != DBNull.Value)
            {
                sizes.Add(Convert.ToDouble(reader["PackSizeKg"]));
            }
        }
        return sizes;
    }

    public int Add(Item item, IEnumerable<double>? packSizes = null)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            using (var cmd = new OleDbCommand(@"
                INSERT INTO Items (HeadGroupId, BrandId, ItemCode, ItemName, DefaultRatePerKg, [Description], CreatedAt) 
                VALUES (?, ?, ?, ?, ?, ?, ?)", conn, tx))
            {
                cmd.Parameters.Add("?", OleDbType.Integer).Value = item.HeadGroupId;
                cmd.Parameters.Add("?", OleDbType.Integer).Value = item.BrandId;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = item.ItemCode ?? string.Empty;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = item.ItemName;
                cmd.Parameters.Add("?", OleDbType.Double).Value = item.DefaultRatePerKg;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)item.Description ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.Date).Value = item.CreatedAt == DateTime.MinValue ? DateTime.Now : item.CreatedAt;
                cmd.ExecuteNonQuery();
            }

            using var idCmd = new OleDbCommand("SELECT @@IDENTITY", conn, tx);
            var itemId = Convert.ToInt32(idCmd.ExecuteScalar());

            if (packSizes != null)
            {
                foreach (var size in packSizes)
                {
                    using var packCmd = new OleDbCommand("INSERT INTO ItemPackSizes (ItemId, PackSizeKg, IsActive) VALUES (?, ?, ?)", conn, tx);
                    packCmd.Parameters.Add("?", OleDbType.Integer).Value = itemId;
                    packCmd.Parameters.Add("?", OleDbType.Double).Value = size;
                    packCmd.Parameters.Add("?", OleDbType.Boolean).Value = true;
                    packCmd.ExecuteNonQuery();

                    using var stockCmd = new OleDbCommand("INSERT INTO Stock (ItemId, PackSizeKg, FullBags, LooseKg, TotalKg, LastUpdated) VALUES (?, ?, 0, 0, 0, ?)", conn, tx);
                    stockCmd.Parameters.Add("?", OleDbType.Integer).Value = itemId;
                    stockCmd.Parameters.Add("?", OleDbType.Double).Value = size;
                    stockCmd.Parameters.Add("?", OleDbType.Date).Value = DateTime.Now;
                    stockCmd.ExecuteNonQuery();
                }
            }

            tx.Commit();
            return itemId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public void Update(Item item, IEnumerable<double>? packSizes = null)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            using (var cmd = new OleDbCommand(@"
                UPDATE Items 
                SET HeadGroupId = ?, BrandId = ?, ItemCode = ?, ItemName = ?, DefaultRatePerKg = ?, [Description] = ? 
                WHERE Id = ?", conn, tx))
            {
                cmd.Parameters.Add("?", OleDbType.Integer).Value = item.HeadGroupId;
                cmd.Parameters.Add("?", OleDbType.Integer).Value = item.BrandId;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = item.ItemCode ?? string.Empty;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = item.ItemName;
                cmd.Parameters.Add("?", OleDbType.Double).Value = item.DefaultRatePerKg;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)item.Description ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.Integer).Value = item.Id;
                cmd.ExecuteNonQuery();
            }

            if (packSizes != null)
            {
                using (var delCmd = new OleDbCommand("DELETE FROM ItemPackSizes WHERE ItemId = ?", conn, tx))
                {
                    delCmd.Parameters.Add("?", OleDbType.Integer).Value = item.Id;
                    delCmd.ExecuteNonQuery();
                }

                foreach (var size in packSizes)
                {
                    using var packCmd = new OleDbCommand("INSERT INTO ItemPackSizes (ItemId, PackSizeKg, IsActive) VALUES (?, ?, ?)", conn, tx);
                    packCmd.Parameters.Add("?", OleDbType.Integer).Value = item.Id;
                    packCmd.Parameters.Add("?", OleDbType.Double).Value = size;
                    packCmd.Parameters.Add("?", OleDbType.Boolean).Value = true;
                    packCmd.ExecuteNonQuery();

                    using var checkCmd = new OleDbCommand("SELECT COUNT(*) FROM Stock WHERE ItemId = ? AND ABS(PackSizeKg - ?) < 0.001", conn, tx);
                    checkCmd.Parameters.Add("?", OleDbType.Integer).Value = item.Id;
                    checkCmd.Parameters.Add("?", OleDbType.Double).Value = size;
                    var count = Convert.ToInt32(checkCmd.ExecuteScalar());
                    if (count == 0)
                    {
                        using var stockCmd = new OleDbCommand("INSERT INTO Stock (ItemId, PackSizeKg, FullBags, LooseKg, TotalKg, LastUpdated) VALUES (?, ?, 0, 0, 0, ?)", conn, tx);
                        stockCmd.Parameters.Add("?", OleDbType.Integer).Value = item.Id;
                        stockCmd.Parameters.Add("?", OleDbType.Double).Value = size;
                        stockCmd.Parameters.Add("?", OleDbType.Date).Value = DateTime.Now;
                        stockCmd.ExecuteNonQuery();
                    }
                }
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public void Delete(int itemId)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            using (var cmd = new OleDbCommand("DELETE FROM ItemPackSizes WHERE ItemId = ?", conn, tx))
            {
                cmd.Parameters.Add("?", OleDbType.Integer).Value = itemId;
                cmd.ExecuteNonQuery();
            }
            using (var cmd = new OleDbCommand("DELETE FROM Stock WHERE ItemId = ?", conn, tx))
            {
                cmd.Parameters.Add("?", OleDbType.Integer).Value = itemId;
                cmd.ExecuteNonQuery();
            }
            using (var cmd = new OleDbCommand("DELETE FROM Items WHERE Id = ?", conn, tx))
            {
                cmd.Parameters.Add("?", OleDbType.Integer).Value = itemId;
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
