using System;
using System.Collections.Generic;
using System.Data.OleDb;
using PolyStock.Core.Models;

namespace PolyStock.Data.Repositories;

public class StockTransferRepository
{
    private readonly string? _dbPath;
    private readonly StockRepository _stockRepo;

    public StockTransferRepository(string? dbPath = null)
    {
        _dbPath = dbPath;
        _stockRepo = new StockRepository(dbPath);
    }

    public string GetNextTransferNo()
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        string datePrefix = $"TRF-{DateTime.Today:yyyyMMdd}-";
        var sql = @"
            SELECT TOP 1 TransferNo 
            FROM StockTransfers 
            WHERE TransferNo LIKE ? 
            ORDER BY Id DESC";

        using var cmd = new OleDbCommand(sql, conn);
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = datePrefix + "%";

        var result = cmd.ExecuteScalar()?.ToString();
        if (string.IsNullOrWhiteSpace(result))
        {
            return $"{datePrefix}0001";
        }

        var parts = result.Split('-');
        if (parts.Length >= 3 && int.TryParse(parts[2], out int seq))
        {
            return $"{datePrefix}{(seq + 1):D4}";
        }

        return $"{datePrefix}0001";
    }

    public int SaveTransfer(StockTransfer transfer)
    {
        if (transfer.FromGodownId == transfer.ToGodownId)
        {
            throw new InvalidOperationException("Source and Destination Godowns cannot be the same.");
        }

        if (transfer.Items == null || transfer.Items.Count == 0)
        {
            throw new InvalidOperationException("A stock transfer must contain at least one line item.");
        }

        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        using var tx = conn.BeginTransaction();

        try
        {
            if (string.IsNullOrWhiteSpace(transfer.TransferNo))
            {
                transfer.TransferNo = GetNextTransferNoWithConn(conn, tx);
            }

            // 1. Insert StockTransfers Master Header
            var headerSql = @"
                INSERT INTO StockTransfers (
                    TransferNo, TransferDate, FromGodownId, FromGodownName,
                    ToGodownId, ToGodownName, TotalBags, TotalLooseKg,
                    TotalWeightKg, TotalAmount, Notes, CreatedAt
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

            using (var cmd = new OleDbCommand(headerSql, conn, tx))
            {
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = transfer.TransferNo;
                cmd.Parameters.Add("?", OleDbType.Date).Value = transfer.TransferDate;
                cmd.Parameters.Add("?", OleDbType.Integer).Value = transfer.FromGodownId;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)transfer.FromGodownName ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.Integer).Value = transfer.ToGodownId;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)transfer.ToGodownName ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.Integer).Value = transfer.TotalBags;
                cmd.Parameters.Add("?", OleDbType.Double).Value = transfer.TotalLooseKg;
                cmd.Parameters.Add("?", OleDbType.Double).Value = transfer.TotalWeightKg;
                cmd.Parameters.Add("?", OleDbType.Double).Value = transfer.TotalAmount;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)transfer.Notes ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.Date).Value = transfer.CreatedAt == DateTime.MinValue ? DateTime.Now : transfer.CreatedAt;
                cmd.ExecuteNonQuery();
            }

            using var idCmd = new OleDbCommand("SELECT @@IDENTITY", conn, tx);
            var transferId = Convert.ToInt32(idCmd.ExecuteScalar());
            transfer.Id = transferId;

            // 2. Insert Lines & Update Stock + StockLedger for both Godowns
            foreach (var item in transfer.Items)
            {
                item.TransferId = transferId;

                var lineSql = @"
                    INSERT INTO StockTransferItems (
                        TransferId, ItemId, PackSizeKg, Bags, LooseKg,
                        TotalKg, RatePerKg, Amount, Remarks
                    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)";

                using (var lineCmd = new OleDbCommand(lineSql, conn, tx))
                {
                    lineCmd.Parameters.Add("?", OleDbType.Integer).Value = transferId;
                    lineCmd.Parameters.Add("?", OleDbType.Integer).Value = item.ItemId;
                    lineCmd.Parameters.Add("?", OleDbType.Double).Value = item.PackSizeKg;
                    lineCmd.Parameters.Add("?", OleDbType.Integer).Value = item.Bags;
                    lineCmd.Parameters.Add("?", OleDbType.Double).Value = item.LooseKg;
                    lineCmd.Parameters.Add("?", OleDbType.Double).Value = item.TotalKg;
                    lineCmd.Parameters.Add("?", OleDbType.Double).Value = item.RatePerKg;
                    lineCmd.Parameters.Add("?", OleDbType.Double).Value = item.Amount;
                    lineCmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)item.Remarks ?? DBNull.Value;
                    lineCmd.ExecuteNonQuery();
                }

                // === A. SOURCE GODOWN: Deduct Stock & Write TRANSFER_OUT Ledger ===
                var sourceStock = _stockRepo.GetStock(item.ItemId, item.PackSizeKg, transfer.FromGodownId, conn, tx);
                int oldSourceBags;
                int newSourceBags;

                if (sourceStock != null)
                {
                    oldSourceBags = sourceStock.FullBags;
                    newSourceBags = Math.Max(0, oldSourceBags - item.Bags);
                    double newSourceTotalKg = newSourceBags * item.PackSizeKg;
                    UpdateStockRow(conn, tx, item.ItemId, item.PackSizeKg, transfer.FromGodownId, transfer.FromGodownName ?? string.Empty, newSourceBags, newSourceTotalKg);
                }
                else
                {
                    var unassignedStock = _stockRepo.GetUnassignedStock(item.ItemId, item.PackSizeKg, conn, tx);
                    if (unassignedStock != null)
                    {
                        oldSourceBags = unassignedStock.FullBags;
                        newSourceBags = Math.Max(0, oldSourceBags - item.Bags);
                        using var updUnassigned = new OleDbCommand(
                            "UPDATE Stock SET FullBags = ?, LooseKg = 0, TotalKg = ?, LastUpdated = ? WHERE ItemId = ? AND ABS(PackSizeKg - ?) < 0.001 AND (GodownId IS NULL OR GodownId = 0)", conn, tx);
                        updUnassigned.Parameters.Add("?", OleDbType.Integer).Value = newSourceBags;
                        updUnassigned.Parameters.Add("?", OleDbType.Double).Value = newSourceBags * item.PackSizeKg;
                        updUnassigned.Parameters.Add("?", OleDbType.Date).Value = DateTime.Now;
                        updUnassigned.Parameters.Add("?", OleDbType.Integer).Value = item.ItemId;
                        updUnassigned.Parameters.Add("?", OleDbType.Double).Value = item.PackSizeKg;
                        updUnassigned.ExecuteNonQuery();
                    }
                    else
                    {
                        oldSourceBags = 0;
                        newSourceBags = 0;
                    }
                }

                double ledgerSourceTotalKg = newSourceBags * item.PackSizeKg;

                // Ledger entry for Source Godown
                InsertStockLedgerEntry(
                    conn, tx,
                    item.ItemId,
                    transfer.FromGodownId,
                    transfer.FromGodownName ?? string.Empty,
                    item.PackSizeKg,
                    "TRANSFER_OUT",
                    transfer.TransferNo,
                    -item.Bags,
                    -item.LooseKg,
                    -item.TotalKg,
                    newSourceBags,
                    0.0,
                    ledgerSourceTotalKg,
                    $"Transferred to [{transfer.ToGodownName}]",
                    transfer.TransferDate);

                // === B. DESTINATION GODOWN: Add Stock & Write TRANSFER_IN Ledger ===
                var destStock = _stockRepo.GetStock(item.ItemId, item.PackSizeKg, transfer.ToGodownId, conn, tx);
                int oldDestBags = destStock?.FullBags ?? 0;
                int newDestBags = oldDestBags + item.Bags;
                double newDestTotalKg = newDestBags * item.PackSizeKg;

                // Update or Insert dest stock row
                UpdateStockRow(conn, tx, item.ItemId, item.PackSizeKg, transfer.ToGodownId, transfer.ToGodownName ?? string.Empty, newDestBags, newDestTotalKg);

                // Ledger entry for Destination Godown
                InsertStockLedgerEntry(
                    conn, tx,
                    item.ItemId,
                    transfer.ToGodownId,
                    transfer.ToGodownName ?? string.Empty,
                    item.PackSizeKg,
                    "TRANSFER_IN",
                    transfer.TransferNo,
                    item.Bags,
                    item.LooseKg,
                    item.TotalKg,
                    newDestBags,
                    0.0,
                    newDestTotalKg,
                    $"Transferred from [{transfer.FromGodownName}]",
                    transfer.TransferDate);
            }

            tx.Commit();
            return transferId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private void UpdateStockRow(
        OleDbConnection conn, OleDbTransaction tx,
        int itemId, double packSizeKg, int godownId, string godownName,
        int fullBags, double totalKg)
    {
        bool exists = false;
        using (var checkCmd = new OleDbCommand(
            "SELECT COUNT(*) FROM Stock WHERE ItemId = ? AND ABS(PackSizeKg - ?) < 0.001 AND GodownId = ?", conn, tx))
        {
            checkCmd.Parameters.Add("?", OleDbType.Integer).Value = itemId;
            checkCmd.Parameters.Add("?", OleDbType.Double).Value = packSizeKg;
            checkCmd.Parameters.Add("?", OleDbType.Integer).Value = godownId;
            exists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;
        }

        if (exists)
        {
            using var updCmd = new OleDbCommand(
                "UPDATE Stock SET FullBags = ?, LooseKg = 0, TotalKg = ?, GodownName = ?, LastUpdated = ? WHERE ItemId = ? AND ABS(PackSizeKg - ?) < 0.001 AND GodownId = ?", conn, tx);
            updCmd.Parameters.Add("?", OleDbType.Integer).Value = fullBags;
            updCmd.Parameters.Add("?", OleDbType.Double).Value = totalKg;
            updCmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)godownName ?? DBNull.Value;
            updCmd.Parameters.Add("?", OleDbType.Date).Value = DateTime.Now;
            updCmd.Parameters.Add("?", OleDbType.Integer).Value = itemId;
            updCmd.Parameters.Add("?", OleDbType.Double).Value = packSizeKg;
            updCmd.Parameters.Add("?", OleDbType.Integer).Value = godownId;
            updCmd.ExecuteNonQuery();
        }
        else
        {
            using var insCmd = new OleDbCommand(
                "INSERT INTO Stock (ItemId, PackSizeKg, FullBags, LooseKg, TotalKg, GodownId, GodownName, LastUpdated) VALUES (?, ?, ?, 0, ?, ?, ?, ?)", conn, tx);
            insCmd.Parameters.Add("?", OleDbType.Integer).Value = itemId;
            insCmd.Parameters.Add("?", OleDbType.Double).Value = packSizeKg;
            insCmd.Parameters.Add("?", OleDbType.Integer).Value = fullBags;
            insCmd.Parameters.Add("?", OleDbType.Double).Value = totalKg;
            insCmd.Parameters.Add("?", OleDbType.Integer).Value = godownId;
            insCmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)godownName ?? DBNull.Value;
            insCmd.Parameters.Add("?", OleDbType.Date).Value = DateTime.Now;
            insCmd.ExecuteNonQuery();
        }
    }

    private void InsertStockLedgerEntry(
        OleDbConnection conn, OleDbTransaction tx,
        int itemId, int godownId, string godownName, double packSizeKg,
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
        ledgerCmd.Parameters.Add("?", OleDbType.Integer).Value = godownId;
        ledgerCmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)godownName ?? DBNull.Value;
        ledgerCmd.Parameters.Add("?", OleDbType.Double).Value = packSizeKg;
        ledgerCmd.Parameters.Add("?", OleDbType.VarWChar).Value = transactionType;
        ledgerCmd.Parameters.Add("?", OleDbType.VarWChar).Value = refInvoiceNo ?? string.Empty;
        ledgerCmd.Parameters.Add("?", OleDbType.Integer).Value = fullBagsChange;
        ledgerCmd.Parameters.Add("?", OleDbType.Double).Value = looseKgChange;
        ledgerCmd.Parameters.Add("?", OleDbType.Double).Value = totalKgChange;
        ledgerCmd.Parameters.Add("?", OleDbType.Integer).Value = balFullBags;
        ledgerCmd.Parameters.Add("?", OleDbType.Double).Value = balLooseKg;
        ledgerCmd.Parameters.Add("?", OleDbType.Double).Value = balTotalKg;
        ledgerCmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)remarks ?? DBNull.Value;
        ledgerCmd.Parameters.Add("?", OleDbType.Date).Value = timestamp;
        ledgerCmd.ExecuteNonQuery();
    }

    private string GetNextTransferNoWithConn(OleDbConnection conn, OleDbTransaction tx)
    {
        string datePrefix = $"TRF-{DateTime.Today:yyyyMMdd}-";
        var sql = @"
            SELECT TOP 1 TransferNo 
            FROM StockTransfers 
            WHERE TransferNo LIKE ? 
            ORDER BY Id DESC";

        using var cmd = new OleDbCommand(sql, conn, tx);
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = datePrefix + "%";

        var result = cmd.ExecuteScalar()?.ToString();
        if (string.IsNullOrWhiteSpace(result))
        {
            return $"{datePrefix}0001";
        }

        var parts = result.Split('-');
        if (parts.Length >= 3 && int.TryParse(parts[2], out int seq))
        {
            return $"{datePrefix}{(seq + 1):D4}";
        }

        return $"{datePrefix}0001";
    }

    public List<StockTransfer> GetAllTransfers()
    {
        var list = new List<StockTransfer>();
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        var sql = @"
            SELECT Id, TransferNo, TransferDate, FromGodownId, FromGodownName,
                   ToGodownId, ToGodownName, TotalBags, TotalLooseKg, TotalWeightKg,
                   TotalAmount, Notes, CreatedAt
            FROM StockTransfers
            ORDER BY TransferDate DESC, Id DESC";

        using var cmd = new OleDbCommand(sql, conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new StockTransfer
            {
                Id = Convert.ToInt32(reader["Id"]),
                TransferNo = reader["TransferNo"]?.ToString() ?? string.Empty,
                TransferDate = Convert.ToDateTime(reader["TransferDate"]),
                FromGodownId = Convert.ToInt32(reader["FromGodownId"]),
                FromGodownName = reader["FromGodownName"]?.ToString() ?? string.Empty,
                ToGodownId = Convert.ToInt32(reader["ToGodownId"]),
                ToGodownName = reader["ToGodownName"]?.ToString() ?? string.Empty,
                TotalBags = Convert.ToInt32(reader["TotalBags"]),
                TotalLooseKg = reader["TotalLooseKg"] != DBNull.Value ? Convert.ToDouble(reader["TotalLooseKg"]) : 0.0,
                TotalWeightKg = Convert.ToDouble(reader["TotalWeightKg"]),
                TotalAmount = reader["TotalAmount"] != DBNull.Value ? Convert.ToDouble(reader["TotalAmount"]) : 0.0,
                Notes = reader["Notes"]?.ToString(),
                CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue
            });
        }
        return list;
    }

    public StockTransfer? GetTransferById(int id)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        var sql = @"
            SELECT Id, TransferNo, TransferDate, FromGodownId, FromGodownName,
                   ToGodownId, ToGodownName, TotalBags, TotalLooseKg, TotalWeightKg,
                   TotalAmount, Notes, CreatedAt
            FROM StockTransfers
            WHERE Id = ?";

        using var cmd = new OleDbCommand(sql, conn);
        cmd.Parameters.Add("?", OleDbType.Integer).Value = id;

        StockTransfer? transfer = null;
        using (var reader = cmd.ExecuteReader())
        {
            if (reader.Read())
            {
                transfer = new StockTransfer
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    TransferNo = reader["TransferNo"]?.ToString() ?? string.Empty,
                    TransferDate = Convert.ToDateTime(reader["TransferDate"]),
                    FromGodownId = Convert.ToInt32(reader["FromGodownId"]),
                    FromGodownName = reader["FromGodownName"]?.ToString() ?? string.Empty,
                    ToGodownId = Convert.ToInt32(reader["ToGodownId"]),
                    ToGodownName = reader["ToGodownName"]?.ToString() ?? string.Empty,
                    TotalBags = Convert.ToInt32(reader["TotalBags"]),
                    TotalLooseKg = reader["TotalLooseKg"] != DBNull.Value ? Convert.ToDouble(reader["TotalLooseKg"]) : 0.0,
                    TotalWeightKg = Convert.ToDouble(reader["TotalWeightKg"]),
                    TotalAmount = reader["TotalAmount"] != DBNull.Value ? Convert.ToDouble(reader["TotalAmount"]) : 0.0,
                    Notes = reader["Notes"]?.ToString(),
                    CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue
                };
            }
        }

        if (transfer != null)
        {
            transfer.Items = GetItemsForTransfer(transfer.Id, conn);
        }

        return transfer;
    }

    public StockTransfer? GetTransferByNo(string transferNo)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        var sql = @"
            SELECT Id, TransferNo, TransferDate, FromGodownId, FromGodownName,
                   ToGodownId, ToGodownName, TotalBags, TotalLooseKg, TotalWeightKg,
                   TotalAmount, Notes, CreatedAt
            FROM StockTransfers
            WHERE TransferNo = ?";

        using var cmd = new OleDbCommand(sql, conn);
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = transferNo;

        StockTransfer? transfer = null;
        using (var reader = cmd.ExecuteReader())
        {
            if (reader.Read())
            {
                transfer = new StockTransfer
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    TransferNo = reader["TransferNo"]?.ToString() ?? string.Empty,
                    TransferDate = Convert.ToDateTime(reader["TransferDate"]),
                    FromGodownId = Convert.ToInt32(reader["FromGodownId"]),
                    FromGodownName = reader["FromGodownName"]?.ToString() ?? string.Empty,
                    ToGodownId = Convert.ToInt32(reader["ToGodownId"]),
                    ToGodownName = reader["ToGodownName"]?.ToString() ?? string.Empty,
                    TotalBags = Convert.ToInt32(reader["TotalBags"]),
                    TotalLooseKg = reader["TotalLooseKg"] != DBNull.Value ? Convert.ToDouble(reader["TotalLooseKg"]) : 0.0,
                    TotalWeightKg = Convert.ToDouble(reader["TotalWeightKg"]),
                    TotalAmount = reader["TotalAmount"] != DBNull.Value ? Convert.ToDouble(reader["TotalAmount"]) : 0.0,
                    Notes = reader["Notes"]?.ToString(),
                    CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue
                };
            }
        }

        if (transfer != null)
        {
            transfer.Items = GetItemsForTransfer(transfer.Id, conn);
        }

        return transfer;
    }

    private List<StockTransferItem> GetItemsForTransfer(int transferId, OleDbConnection conn)
    {
        var items = new List<StockTransferItem>();
        var sql = @"
            SELECT sti.Id, sti.TransferId, sti.ItemId, i.ItemName, sti.PackSizeKg,
                   sti.Bags, sti.LooseKg, sti.TotalKg, sti.RatePerKg, sti.Amount, sti.Remarks
            FROM StockTransferItems sti
            LEFT JOIN Items i ON sti.ItemId = i.Id
            WHERE sti.TransferId = ?
            ORDER BY sti.Id ASC";

        using var cmd = new OleDbCommand(sql, conn);
        cmd.Parameters.Add("?", OleDbType.Integer).Value = transferId;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new StockTransferItem
            {
                Id = Convert.ToInt32(reader["Id"]),
                TransferId = Convert.ToInt32(reader["TransferId"]),
                ItemId = Convert.ToInt32(reader["ItemId"]),
                ItemName = reader["ItemName"]?.ToString() ?? string.Empty,
                PackSizeKg = Convert.ToDouble(reader["PackSizeKg"]),
                Bags = Convert.ToInt32(reader["Bags"]),
                LooseKg = reader["LooseKg"] != DBNull.Value ? Convert.ToDouble(reader["LooseKg"]) : 0.0,
                TotalKg = Convert.ToDouble(reader["TotalKg"]),
                RatePerKg = reader["RatePerKg"] != DBNull.Value ? Convert.ToDouble(reader["RatePerKg"]) : 0.0,
                Amount = reader["Amount"] != DBNull.Value ? Convert.ToDouble(reader["Amount"]) : 0.0,
                Remarks = reader["Remarks"]?.ToString()
            });
        }

        return items;
    }
}
