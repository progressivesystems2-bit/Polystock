using System;
using System.Collections.Generic;
using System.Data.OleDb;
using PolyStock.Core.Models;

namespace PolyStock.Data.Repositories;

public class PurchaseRepository
{
    private readonly string? _dbPath;
    private readonly StockRepository _stockRepo;
    private readonly AccountRepository _accountRepo;

    public PurchaseRepository(string? dbPath = null)
    {
        _dbPath = dbPath;
        _stockRepo = new StockRepository(dbPath);
        _accountRepo = new AccountRepository(dbPath);
    }

    public List<Purchase> GetAll()
    {
        var list = new List<Purchase>();
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        using var cmd = new OleDbCommand("SELECT Id, InvoiceNo, SupplierName, GodownId, GodownName, PurchaseDate, TotalBags, TotalWeightKg, SubTotal, GstPercent, GstAmount, Freight, LoadingUnloading, GrandTotal, TotalAmount, Notes, CreatedAt FROM Purchases ORDER BY PurchaseDate DESC, Id DESC", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Purchase
            {
                Id = Convert.ToInt32(reader["Id"]),
                InvoiceNo = reader["InvoiceNo"]?.ToString() ?? string.Empty,
                SupplierName = reader["SupplierName"]?.ToString() ?? string.Empty,
                GodownId = reader["GodownId"] != DBNull.Value ? Convert.ToInt32(reader["GodownId"]) : null,
                GodownName = reader["GodownName"]?.ToString(),
                PurchaseDate = Convert.ToDateTime(reader["PurchaseDate"]),
                TotalBags = Convert.ToInt32(reader["TotalBags"]),
                TotalWeightKg = Convert.ToDouble(reader["TotalWeightKg"]),
                SubTotal = reader["SubTotal"] != DBNull.Value ? Convert.ToDouble(reader["SubTotal"]) : Convert.ToDouble(reader["TotalAmount"]),
                GstPercent = reader["GstPercent"] != DBNull.Value ? Convert.ToDouble(reader["GstPercent"]) : 0.0,
                GstAmount = reader["GstAmount"] != DBNull.Value ? Convert.ToDouble(reader["GstAmount"]) : 0.0,
                Freight = reader["Freight"] != DBNull.Value ? Convert.ToDouble(reader["Freight"]) : 0.0,
                LoadingUnloading = reader["LoadingUnloading"] != DBNull.Value ? Convert.ToDouble(reader["LoadingUnloading"]) : 0.0,
                GrandTotal = reader["GrandTotal"] != DBNull.Value ? Convert.ToDouble(reader["GrandTotal"]) : Convert.ToDouble(reader["TotalAmount"]),
                Notes = reader["Notes"]?.ToString(),
                CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue
            });
        }

        foreach (var p in list)
        {
            p.Items = GetItemsForPurchase(conn, p.Id);
        }

        return list;
    }

    public Purchase? GetById(int id)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        using var cmd = new OleDbCommand("SELECT Id, InvoiceNo, SupplierName, GodownId, GodownName, PurchaseDate, TotalBags, TotalWeightKg, SubTotal, GstPercent, GstAmount, Freight, LoadingUnloading, GrandTotal, TotalAmount, Notes, CreatedAt FROM Purchases WHERE Id = ?", conn);
        cmd.Parameters.Add("?", OleDbType.Integer).Value = id;
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var purchase = new Purchase
            {
                Id = Convert.ToInt32(reader["Id"]),
                InvoiceNo = reader["InvoiceNo"]?.ToString() ?? string.Empty,
                SupplierName = reader["SupplierName"]?.ToString() ?? string.Empty,
                GodownId = reader["GodownId"] != DBNull.Value ? Convert.ToInt32(reader["GodownId"]) : null,
                GodownName = reader["GodownName"]?.ToString(),
                PurchaseDate = Convert.ToDateTime(reader["PurchaseDate"]),
                TotalBags = Convert.ToInt32(reader["TotalBags"]),
                TotalWeightKg = Convert.ToDouble(reader["TotalWeightKg"]),
                SubTotal = reader["SubTotal"] != DBNull.Value ? Convert.ToDouble(reader["SubTotal"]) : Convert.ToDouble(reader["TotalAmount"]),
                GstPercent = reader["GstPercent"] != DBNull.Value ? Convert.ToDouble(reader["GstPercent"]) : 0.0,
                GstAmount = reader["GstAmount"] != DBNull.Value ? Convert.ToDouble(reader["GstAmount"]) : 0.0,
                Freight = reader["Freight"] != DBNull.Value ? Convert.ToDouble(reader["Freight"]) : 0.0,
                LoadingUnloading = reader["LoadingUnloading"] != DBNull.Value ? Convert.ToDouble(reader["LoadingUnloading"]) : 0.0,
                GrandTotal = reader["GrandTotal"] != DBNull.Value ? Convert.ToDouble(reader["GrandTotal"]) : Convert.ToDouble(reader["TotalAmount"]),
                Notes = reader["Notes"]?.ToString(),
                CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue
            };
            purchase.Items = GetItemsForPurchase(conn, purchase.Id);
            return purchase;
        }
        return null;
    }

    public Purchase? GetByInvoiceNo(string invoiceNo)
    {
        if (string.IsNullOrWhiteSpace(invoiceNo)) return null;
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        using var cmd = new OleDbCommand("SELECT TOP 1 Id FROM Purchases WHERE InvoiceNo = ?", conn);
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = invoiceNo.Trim();
        var val = cmd.ExecuteScalar();
        if (val != null && val != DBNull.Value)
        {
            return GetById(Convert.ToInt32(val));
        }
        return null;
    }

    public List<PurchaseItem> GetItemsForPurchase(OleDbConnection conn, int purchaseId, OleDbTransaction? tx = null)
    {
        var items = new List<PurchaseItem>();
        var sql = @"
            SELECT pi.Id, pi.PurchaseId, pi.ItemId, i.ItemName, pi.GodownId, pi.GodownName, pi.PackSizeKg, pi.Bags, pi.TotalKg, pi.RatePerKg, pi.Amount, pi.LandedCost
            FROM PurchaseItems pi
            LEFT JOIN Items i ON pi.ItemId = i.Id
            WHERE pi.PurchaseId = ?";

        using var cmd = new OleDbCommand(sql, conn, tx);
        cmd.Parameters.Add("?", OleDbType.Integer).Value = purchaseId;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            double rate = Convert.ToDouble(reader["RatePerKg"]);
            double landed = rate;
            try
            {
                var ord = reader.GetOrdinal("LandedCost");
                if (!reader.IsDBNull(ord)) landed = Convert.ToDouble(reader.GetValue(ord));
            }
            catch { }

            items.Add(new PurchaseItem
            {
                Id = Convert.ToInt32(reader["Id"]),
                PurchaseId = Convert.ToInt32(reader["PurchaseId"]),
                ItemId = Convert.ToInt32(reader["ItemId"]),
                ItemName = reader["ItemName"]?.ToString() ?? string.Empty,
                GodownId = reader["GodownId"] != DBNull.Value ? Convert.ToInt32(reader["GodownId"]) : null,
                GodownName = reader["GodownName"]?.ToString(),
                PackSizeKg = Convert.ToDouble(reader["PackSizeKg"]),
                Bags = Convert.ToInt32(reader["Bags"]),
                TotalKg = Convert.ToDouble(reader["TotalKg"]),
                RatePerKg = rate,
                Amount = Convert.ToDouble(reader["Amount"]),
                LandedCost = landed > 0.0001 ? landed : rate
            });
        }
        return items;
    }

    public int SavePurchase(Purchase purchase)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            // Insert Purchase Master
            using (var cmd = new OleDbCommand(@"
                INSERT INTO Purchases (InvoiceNo, SupplierName, GodownId, GodownName, PurchaseDate, TotalBags, TotalWeightKg, SubTotal, GstPercent, GstAmount, Freight, LoadingUnloading, GrandTotal, TotalAmount, Notes, CreatedAt)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)", conn, tx))
            {
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = purchase.InvoiceNo;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = purchase.SupplierName ?? string.Empty;
                cmd.Parameters.Add("?", OleDbType.Integer).Value = (object?)purchase.GodownId ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)purchase.GodownName ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.Date).Value = purchase.PurchaseDate;
                cmd.Parameters.Add("?", OleDbType.Integer).Value = purchase.TotalBags;
                cmd.Parameters.Add("?", OleDbType.Double).Value = purchase.TotalWeightKg;
                cmd.Parameters.Add("?", OleDbType.Double).Value = purchase.SubTotal;
                cmd.Parameters.Add("?", OleDbType.Double).Value = purchase.GstPercent;
                cmd.Parameters.Add("?", OleDbType.Double).Value = purchase.GstAmount;
                cmd.Parameters.Add("?", OleDbType.Double).Value = purchase.Freight;
                cmd.Parameters.Add("?", OleDbType.Double).Value = purchase.LoadingUnloading;
                cmd.Parameters.Add("?", OleDbType.Double).Value = purchase.GrandTotal;
                cmd.Parameters.Add("?", OleDbType.Double).Value = purchase.GrandTotal > 0 ? purchase.GrandTotal : purchase.TotalAmount;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)purchase.Notes ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.Date).Value = purchase.CreatedAt == DateTime.MinValue ? DateTime.Now : purchase.CreatedAt;
                cmd.ExecuteNonQuery();
            }

            using var idCmd = new OleDbCommand("SELECT @@IDENTITY", conn, tx);
            var purchaseId = Convert.ToInt32(idCmd.ExecuteScalar());
            purchase.Id = purchaseId;

            // Insert Items & Update Stock
            foreach (var item in purchase.Items)
            {
                using var itemCmd = new OleDbCommand(@"
                    INSERT INTO PurchaseItems (PurchaseId, ItemId, GodownId, GodownName, PackSizeKg, Bags, TotalKg, RatePerKg, Amount, LandedCost)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)", conn, tx);

                itemCmd.Parameters.Add("?", OleDbType.Integer).Value = purchaseId;
                itemCmd.Parameters.Add("?", OleDbType.Integer).Value = item.ItemId;
                itemCmd.Parameters.Add("?", OleDbType.Integer).Value = (object?)item.GodownId ?? (object?)purchase.GodownId ?? DBNull.Value;
                itemCmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)item.GodownName ?? (object?)purchase.GodownName ?? DBNull.Value;
                itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.PackSizeKg;
                itemCmd.Parameters.Add("?", OleDbType.Integer).Value = item.Bags;
                itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.TotalKg;
                itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.RatePerKg;
                itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.Amount;
                itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.LandedCost > 0.0001 ? item.LandedCost : item.RatePerKg;
                itemCmd.ExecuteNonQuery();

                // Current stock
                int? itemGodownId = item.GodownId ?? purchase.GodownId;
                string? itemGodownName = item.GodownName ?? purchase.GodownName;
                var currentStock = _stockRepo.GetStock(item.ItemId, item.PackSizeKg, itemGodownId, conn, tx);
                int oldFullBags = currentStock?.FullBags ?? 0;
                double oldLooseKg = currentStock?.LooseKg ?? 0.0;

                int newFullBags = oldFullBags + item.Bags;
                double newLooseKg = oldLooseKg;
                double newTotalKg = (newFullBags * item.PackSizeKg) + newLooseKg;

                string godownPrefix = !string.IsNullOrWhiteSpace(item.GodownName) ? $"[{item.GodownName}] " : "";
                _stockRepo.UpsertStockAndLedger(
                    item.ItemId,
                    item.ItemName,
                    item.PackSizeKg,
                    newFullBags,
                    newLooseKg,
                    newTotalKg,
                    "PURCHASE",
                    purchase.InvoiceNo,
                    item.Bags,
                    0,
                    item.TotalKg,
                    $"{godownPrefix}Purchased {item.Bags} bags from {purchase.SupplierName}",
                    conn,
                    tx,
                    itemGodownId,
                    itemGodownName);
            }

            // Post Bill Amount + GST Amount to Supplier's Account Ledger
            if (!string.IsNullOrWhiteSpace(purchase.SupplierName))
            {
                var partyAccount = _accountRepo.EnsureAccountExists(purchase.SupplierName, "Supplier", conn, tx);
                double postedAmount = purchase.PartyAmount > 0 ? purchase.PartyAmount : (purchase.SubTotal + purchase.GstAmount);
                _accountRepo.PostLedgerEntry(new AccountLedgerEntry
                {
                    AccountId = partyAccount.Id,
                    AccountName = partyAccount.AccountName,
                    TransactionType = "PURCHASE",
                    RefInvoiceNo = purchase.InvoiceNo,
                    Debit = 0.0,
                    Credit = postedAmount,
                    Balance = 0.0,
                    Remarks = $"Purchase Bill #{purchase.InvoiceNo}: Bill Amount ₹{purchase.SubTotal:N2} + GST ₹{purchase.GstAmount:N2} = ₹{postedAmount:N2}",
                    Timestamp = purchase.PurchaseDate
                }, conn, tx);
            }

            tx.Commit();
            return purchaseId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public void UpdatePurchase(Purchase purchase)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            // 1. Revert previous stock addition for old items of this purchase
            var oldItems = GetItemsForPurchase(conn, purchase.Id, tx);
            foreach (var old in oldItems)
            {
                var curStock = _stockRepo.GetStock(old.ItemId, old.PackSizeKg, conn, tx);
                int fb = (curStock?.FullBags ?? 0) - old.Bags;
                if (fb < 0) fb = 0;
                double lk = curStock?.LooseKg ?? 0.0;
                double tk = (fb * old.PackSizeKg) + lk;

                _stockRepo.UpsertStockAndLedger(
                    old.ItemId,
                    old.ItemName,
                    old.PackSizeKg,
                    fb,
                    lk,
                    tk,
                    "PURCHASE_REVERT",
                    purchase.InvoiceNo,
                    -old.Bags,
                    0,
                    -old.TotalKg,
                    $"Reverting prior purchase line for edit #{purchase.InvoiceNo}",
                    conn,
                    tx);
            }

            // 2. Update Purchases Master Record
            using (var cmd = new OleDbCommand(@"
                UPDATE Purchases
                SET SupplierName = ?, GodownId = ?, GodownName = ?, PurchaseDate = ?, TotalBags = ?, TotalWeightKg = ?, SubTotal = ?, GstPercent = ?, GstAmount = ?, Freight = ?, LoadingUnloading = ?, GrandTotal = ?, TotalAmount = ?, Notes = ?
                WHERE Id = ?", conn, tx))
            {
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = purchase.SupplierName ?? string.Empty;
                cmd.Parameters.Add("?", OleDbType.Integer).Value = (object?)purchase.GodownId ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)purchase.GodownName ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.Date).Value = purchase.PurchaseDate;
                cmd.Parameters.Add("?", OleDbType.Integer).Value = purchase.TotalBags;
                cmd.Parameters.Add("?", OleDbType.Double).Value = purchase.TotalWeightKg;
                cmd.Parameters.Add("?", OleDbType.Double).Value = purchase.SubTotal;
                cmd.Parameters.Add("?", OleDbType.Double).Value = purchase.GstPercent;
                cmd.Parameters.Add("?", OleDbType.Double).Value = purchase.GstAmount;
                cmd.Parameters.Add("?", OleDbType.Double).Value = purchase.Freight;
                cmd.Parameters.Add("?", OleDbType.Double).Value = purchase.LoadingUnloading;
                cmd.Parameters.Add("?", OleDbType.Double).Value = purchase.GrandTotal;
                cmd.Parameters.Add("?", OleDbType.Double).Value = purchase.GrandTotal > 0 ? purchase.GrandTotal : purchase.TotalAmount;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)purchase.Notes ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.Integer).Value = purchase.Id;
                cmd.ExecuteNonQuery();
            }

            // 3. Delete old PurchaseItems
            using (var delCmd = new OleDbCommand("DELETE FROM PurchaseItems WHERE PurchaseId = ?", conn, tx))
            {
                delCmd.Parameters.Add("?", OleDbType.Integer).Value = purchase.Id;
                delCmd.ExecuteNonQuery();
            }

            // 4. Insert new items and add stock
            foreach (var item in purchase.Items)
            {
                using var itemCmd = new OleDbCommand(@"
                    INSERT INTO PurchaseItems (PurchaseId, ItemId, GodownId, GodownName, PackSizeKg, Bags, TotalKg, RatePerKg, Amount, LandedCost)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)", conn, tx);

                itemCmd.Parameters.Add("?", OleDbType.Integer).Value = purchase.Id;
                itemCmd.Parameters.Add("?", OleDbType.Integer).Value = item.ItemId;
                itemCmd.Parameters.Add("?", OleDbType.Integer).Value = (object?)item.GodownId ?? (object?)purchase.GodownId ?? DBNull.Value;
                itemCmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)item.GodownName ?? (object?)purchase.GodownName ?? DBNull.Value;
                itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.PackSizeKg;
                itemCmd.Parameters.Add("?", OleDbType.Integer).Value = item.Bags;
                itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.TotalKg;
                itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.RatePerKg;
                itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.Amount;
                itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.LandedCost > 0.0001 ? item.LandedCost : item.RatePerKg;
                itemCmd.ExecuteNonQuery();

                var currentStock = _stockRepo.GetStock(item.ItemId, item.PackSizeKg, conn, tx);
                int oldFullBags = currentStock?.FullBags ?? 0;
                double oldLooseKg = currentStock?.LooseKg ?? 0.0;

                int newFullBags = oldFullBags + item.Bags;
                double newLooseKg = oldLooseKg;
                double newTotalKg = (newFullBags * item.PackSizeKg) + newLooseKg;

                string godownPrefix = !string.IsNullOrWhiteSpace(item.GodownName) ? $"[{item.GodownName}] " : "";
                _stockRepo.UpsertStockAndLedger(
                    item.ItemId,
                    item.ItemName,
                    item.PackSizeKg,
                    newFullBags,
                    newLooseKg,
                    newTotalKg,
                    "PURCHASE_MODIFIED",
                    purchase.InvoiceNo,
                    item.Bags,
                    0,
                    item.TotalKg,
                    $"{godownPrefix}Modified Purchase: {item.Bags} bags from {purchase.SupplierName}",
                    conn,
                    tx);
            }

            // 5. Update supplier account ledger entry
            if (!string.IsNullOrWhiteSpace(purchase.SupplierName))
            {
                var partyAccount = _accountRepo.EnsureAccountExists(purchase.SupplierName, "Supplier", conn, tx);
                double postedAmount = purchase.PartyAmount > 0 ? purchase.PartyAmount : (purchase.SubTotal + purchase.GstAmount);
                _accountRepo.PostLedgerEntry(new AccountLedgerEntry
                {
                    AccountId = partyAccount.Id,
                    AccountName = partyAccount.AccountName,
                    TransactionType = "PURCHASE_MODIFIED",
                    RefInvoiceNo = purchase.InvoiceNo,
                    Debit = 0.0,
                    Credit = postedAmount,
                    Balance = 0.0,
                    Remarks = $"Modified Purchase Bill #{purchase.InvoiceNo}: Bill Amount ₹{purchase.SubTotal:N2} + GST ₹{purchase.GstAmount:N2} = ₹{postedAmount:N2}",
                    Timestamp = purchase.PurchaseDate
                }, conn, tx);
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
