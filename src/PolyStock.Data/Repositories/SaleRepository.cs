using System;
using System.Collections.Generic;
using System.Data.OleDb;
using PolyStock.Core.Common;
using PolyStock.Core.Models;

namespace PolyStock.Data.Repositories;

public class SaleRepository
{
    private readonly string? _dbPath;
    private readonly StockRepository _stockRepo;
    private readonly AccountRepository _accountRepo;
    private readonly SettingsRepository _settingsRepo;

    public SaleRepository(string? dbPath = null)
    {
        _dbPath = dbPath;
        _stockRepo = new StockRepository(dbPath);
        _accountRepo = new AccountRepository(dbPath);
        _settingsRepo = new SettingsRepository(dbPath);
    }

    private Sale ReadSale(OleDbDataReader reader)
    {
        return new Sale
        {
            Id = Convert.ToInt32(reader["Id"]),
            InvoiceNo = reader["InvoiceNo"]?.ToString() ?? string.Empty,
            CustomerName = reader["CustomerName"]?.ToString() ?? string.Empty,
            GodownId = reader["GodownId"] != DBNull.Value ? Convert.ToInt32(reader["GodownId"]) : null,
            GodownName = reader["GodownName"]?.ToString(),
            SaleDate = Convert.ToDateTime(reader["SaleDate"]),
            TotalBags = Convert.ToInt32(reader["TotalBags"]),
            TotalLooseKg = Convert.ToDouble(reader["TotalLooseKg"]),
            TotalWeightKg = Convert.ToDouble(reader["TotalWeightKg"]),
            SubTotal = reader["SubTotal"] != DBNull.Value ? Convert.ToDouble(reader["SubTotal"]) : Convert.ToDouble(reader["TotalAmount"]),
            GstPercent = reader["GstPercent"] != DBNull.Value ? Convert.ToDouble(reader["GstPercent"]) : 0.0,
            GstAmount = reader["GstAmount"] != DBNull.Value ? Convert.ToDouble(reader["GstAmount"]) : 0.0,
            Freight = reader["Freight"] != DBNull.Value ? Convert.ToDouble(reader["Freight"]) : 0.0,
            LoadingUnloading = reader["LoadingUnloading"] != DBNull.Value ? Convert.ToDouble(reader["LoadingUnloading"]) : 0.0,
            GrandTotal = reader["GrandTotal"] != DBNull.Value ? Convert.ToDouble(reader["GrandTotal"]) : Convert.ToDouble(reader["TotalAmount"]),
            Notes = reader["Notes"]?.ToString(),
            PaymentMode = reader["PaymentMode"] != DBNull.Value ? reader["PaymentMode"]?.ToString() ?? "Credit" : "Credit",
            BankAccountId = reader["BankAccountId"] != DBNull.Value ? Convert.ToInt32(reader["BankAccountId"]) : null,
            BankAccountName = reader["BankAccountName"]?.ToString(),
            UtrRefNo = reader["UtrRefNo"]?.ToString(),
            CashReceived = reader["CashReceived"] != DBNull.Value ? Convert.ToDouble(reader["CashReceived"]) : 0.0,
            UpiReceived = reader["UpiReceived"] != DBNull.Value ? Convert.ToDouble(reader["UpiReceived"]) : 0.0,
            CreditAmount = reader["CreditAmount"] != DBNull.Value ? Convert.ToDouble(reader["CreditAmount"]) : 0.0,
            CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue
        };
    }

    public List<Sale> GetAll()
    {
        var list = new List<Sale>();
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        using var cmd = new OleDbCommand(@"
            SELECT Id, InvoiceNo, CustomerName, GodownId, GodownName, SaleDate, TotalBags, TotalLooseKg, TotalWeightKg, 
                   SubTotal, GstPercent, GstAmount, Freight, LoadingUnloading, GrandTotal, TotalAmount, Notes, 
                   PaymentMode, BankAccountId, BankAccountName, UtrRefNo, CashReceived, UpiReceived, CreditAmount, CreatedAt 
            FROM Sales 
            ORDER BY SaleDate DESC, Id DESC", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(ReadSale(reader));
        }

        foreach (var s in list)
        {
            s.Items = GetItemsForSale(conn, s.Id);
        }

        return list;
    }

    public Sale? GetById(int id)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        using var cmd = new OleDbCommand(@"
            SELECT Id, InvoiceNo, CustomerName, GodownId, GodownName, SaleDate, TotalBags, TotalLooseKg, TotalWeightKg, 
                   SubTotal, GstPercent, GstAmount, Freight, LoadingUnloading, GrandTotal, TotalAmount, Notes, 
                   PaymentMode, BankAccountId, BankAccountName, UtrRefNo, CashReceived, UpiReceived, CreditAmount, CreatedAt 
            FROM Sales 
            WHERE Id = ?", conn);
        cmd.Parameters.Add("?", OleDbType.Integer).Value = id;
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var sale = ReadSale(reader);
            sale.Items = GetItemsForSale(conn, sale.Id);
            return sale;
        }
        return null;
    }

    public Sale? GetByInvoiceNo(string invoiceNo)
    {
        if (string.IsNullOrWhiteSpace(invoiceNo)) return null;
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        using var cmd = new OleDbCommand("SELECT TOP 1 Id FROM Sales WHERE InvoiceNo = ?", conn);
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = invoiceNo.Trim();
        var val = cmd.ExecuteScalar();
        if (val != null && val != DBNull.Value)
        {
            return GetById(Convert.ToInt32(val));
        }
        return null;
    }

    public List<SaleItem> GetItemsForSale(OleDbConnection conn, int saleId, OleDbTransaction? tx = null)
    {
        var items = new List<SaleItem>();
        var sql = @"
            SELECT si.Id, si.SaleId, si.ItemId, i.ItemName, si.GodownId, si.GodownName, si.PackSizeKg, si.BagsSold, si.LooseKgSold, si.TotalKgSold, si.RatePerKg, si.Amount, si.BagsOpened, si.SkipStockDeduction
            FROM SaleItems si
            LEFT JOIN Items i ON si.ItemId = i.Id
            WHERE si.SaleId = ?";

        using var cmd = new OleDbCommand(sql, conn, tx);
        cmd.Parameters.Add("?", OleDbType.Integer).Value = saleId;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new SaleItem
            {
                Id = Convert.ToInt32(reader["Id"]),
                SaleId = Convert.ToInt32(reader["SaleId"]),
                ItemId = Convert.ToInt32(reader["ItemId"]),
                ItemName = reader["ItemName"]?.ToString() ?? string.Empty,
                GodownId = reader["GodownId"] != DBNull.Value ? Convert.ToInt32(reader["GodownId"]) : null,
                GodownName = reader["GodownName"]?.ToString(),
                PackSizeKg = Convert.ToDouble(reader["PackSizeKg"]),
                BagsSold = Convert.ToInt32(reader["BagsSold"]),
                LooseKgSold = Convert.ToDouble(reader["LooseKgSold"]),
                TotalKgSold = Convert.ToDouble(reader["TotalKgSold"]),
                RatePerKg = Convert.ToDouble(reader["RatePerKg"]),
                Amount = Convert.ToDouble(reader["Amount"]),
                BagsOpened = reader["BagsOpened"] != DBNull.Value ? Convert.ToInt32(reader["BagsOpened"]) : 0,
                SkipStockDeduction = reader["SkipStockDeduction"] != DBNull.Value && Convert.ToBoolean(reader["SkipStockDeduction"])
            });
        }
        return items;
    }

    public int SaveSale(Sale sale)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            double thresholdKg = _settingsRepo.GetSalesDeductionThresholdKg(conn, tx);

            // Pre-validation of stock for items that deduct inventory
            foreach (var item in sale.Items)
            {
                bool skipDeduction = item.SkipStockDeduction || !StockCalculationEngine.ShouldDeductInventory(item.TotalKgSold, thresholdKg);
                item.SkipStockDeduction = skipDeduction;

                if (!skipDeduction)
                {
                    var stock = _stockRepo.GetStock(item.ItemId, item.PackSizeKg, conn, tx);
                    int currentBags = stock?.FullBags ?? 0;
                    var morphCheck = StockCalculationEngine.CalculateBagMorph(currentBags, item.PackSizeKg, item.BagsSold, item.LooseKgSold);
                    if (!morphCheck.Success)
                    {
                        throw new InvalidOperationException($"Cannot complete sale: {morphCheck.ErrorMessage}");
                    }
                }
            }

            // Insert Sale Master
            using (var cmd = new OleDbCommand(@"
                INSERT INTO Sales (InvoiceNo, CustomerName, GodownId, GodownName, SaleDate, TotalBags, TotalLooseKg, TotalWeightKg, SubTotal, GstPercent, GstAmount, Freight, LoadingUnloading, GrandTotal, TotalAmount, Notes, PaymentMode, BankAccountId, BankAccountName, UtrRefNo, CashReceived, UpiReceived, CreditAmount, CreatedAt)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)", conn, tx))
            {
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = sale.InvoiceNo;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = sale.CustomerName ?? string.Empty;
                cmd.Parameters.Add("?", OleDbType.Integer).Value = (object?)sale.GodownId ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)sale.GodownName ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.Date).Value = sale.SaleDate;
                cmd.Parameters.Add("?", OleDbType.Integer).Value = sale.TotalBags;
                cmd.Parameters.Add("?", OleDbType.Double).Value = sale.TotalLooseKg;
                cmd.Parameters.Add("?", OleDbType.Double).Value = sale.TotalWeightKg;
                cmd.Parameters.Add("?", OleDbType.Double).Value = sale.SubTotal;
                cmd.Parameters.Add("?", OleDbType.Double).Value = sale.GstPercent;
                cmd.Parameters.Add("?", OleDbType.Double).Value = sale.GstAmount;
                cmd.Parameters.Add("?", OleDbType.Double).Value = sale.Freight;
                cmd.Parameters.Add("?", OleDbType.Double).Value = sale.LoadingUnloading;
                cmd.Parameters.Add("?", OleDbType.Double).Value = sale.GrandTotal;
                cmd.Parameters.Add("?", OleDbType.Double).Value = sale.GrandTotal > 0 ? sale.GrandTotal : sale.TotalAmount;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)sale.Notes ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = string.IsNullOrWhiteSpace(sale.PaymentMode) ? "Credit" : sale.PaymentMode;
                cmd.Parameters.Add("?", OleDbType.Integer).Value = (object?)sale.BankAccountId ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)sale.BankAccountName ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)sale.UtrRefNo ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.Double).Value = sale.CashReceived;
                cmd.Parameters.Add("?", OleDbType.Double).Value = sale.UpiReceived;
                cmd.Parameters.Add("?", OleDbType.Double).Value = sale.CreditAmount;
                cmd.Parameters.Add("?", OleDbType.Date).Value = sale.CreatedAt == DateTime.MinValue ? DateTime.Now : sale.CreatedAt;
                cmd.ExecuteNonQuery();
            }

            using var idCmd = new OleDbCommand("SELECT @@IDENTITY", conn, tx);
            var saleId = Convert.ToInt32(idCmd.ExecuteScalar());
            sale.Id = saleId;

            // Process each sale line & execute dynamic bag morphing
            foreach (var item in sale.Items)
            {
                bool skipDeduction = item.SkipStockDeduction;

                if (!skipDeduction)
                {
                    var stock = _stockRepo.GetStock(item.ItemId, item.PackSizeKg, conn, tx);
                    int currentBags = stock?.FullBags ?? 0;
                    var morph = StockCalculationEngine.CalculateBagMorph(currentBags, item.PackSizeKg, item.BagsSold, item.LooseKgSold);

                    item.BagsOpened = morph.MorphedBagsAdded;

                    // Insert SaleItem row
                    using (var itemCmd = new OleDbCommand(@"
                        INSERT INTO SaleItems (SaleId, ItemId, GodownId, GodownName, PackSizeKg, BagsSold, LooseKgSold, TotalKgSold, RatePerKg, Amount, BagsOpened, SkipStockDeduction)
                        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)", conn, tx))
                    {
                        itemCmd.Parameters.Add("?", OleDbType.Integer).Value = saleId;
                        itemCmd.Parameters.Add("?", OleDbType.Integer).Value = item.ItemId;
                        itemCmd.Parameters.Add("?", OleDbType.Integer).Value = (object?)item.GodownId ?? (object?)sale.GodownId ?? DBNull.Value;
                        itemCmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)item.GodownName ?? (object?)sale.GodownName ?? DBNull.Value;
                        itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.PackSizeKg;
                        itemCmd.Parameters.Add("?", OleDbType.Integer).Value = item.BagsSold;
                        itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.LooseKgSold;
                        itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.TotalKgSold;
                        itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.RatePerKg;
                        itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.Amount;
                        itemCmd.Parameters.Add("?", OleDbType.Integer).Value = item.BagsOpened;
                        itemCmd.Parameters.Add("?", OleDbType.Boolean).Value = false;
                        itemCmd.ExecuteNonQuery();
                    }

                    // Apply Bag Morphing in Stock & StockLedger
                    _stockRepo.ApplySaleBagMorph(
                        item.ItemId,
                        item.ItemName,
                        item.PackSizeKg,
                        morph.SourceBagsDeducted,
                        morph.MorphedPackSizeKg,
                        morph.MorphedBagsAdded,
                        item.TotalKgSold,
                        sale.InvoiceNo,
                        sale.CustomerName ?? string.Empty,
                        item.GodownId ?? sale.GodownId,
                        item.GodownName ?? sale.GodownName,
                        conn,
                        tx);
                }
                else
                {
                    item.BagsOpened = 0;

                    using (var itemCmd = new OleDbCommand(@"
                        INSERT INTO SaleItems (SaleId, ItemId, GodownId, GodownName, PackSizeKg, BagsSold, LooseKgSold, TotalKgSold, RatePerKg, Amount, BagsOpened, SkipStockDeduction)
                        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)", conn, tx))
                    {
                        itemCmd.Parameters.Add("?", OleDbType.Integer).Value = saleId;
                        itemCmd.Parameters.Add("?", OleDbType.Integer).Value = item.ItemId;
                        itemCmd.Parameters.Add("?", OleDbType.Integer).Value = (object?)item.GodownId ?? (object?)sale.GodownId ?? DBNull.Value;
                        itemCmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)item.GodownName ?? (object?)sale.GodownName ?? DBNull.Value;
                        itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.PackSizeKg;
                        itemCmd.Parameters.Add("?", OleDbType.Integer).Value = item.BagsSold;
                        itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.LooseKgSold;
                        itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.TotalKgSold;
                        itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.RatePerKg;
                        itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.Amount;
                        itemCmd.Parameters.Add("?", OleDbType.Integer).Value = 0;
                        itemCmd.Parameters.Add("?", OleDbType.Boolean).Value = true;
                        itemCmd.ExecuteNonQuery();
                    }
                }
            }

            // Post Double-Entry Ledgers (Sale Debit & Cash/UPI Receipts)
            PostSaleLedgers(sale, conn, tx, isModification: false);

            tx.Commit();
            return saleId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public void UpdateSale(Sale sale)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            // 1. Revert previous stock deduction & morphing for old items of this sale (only those that actually deducted stock)
            var oldItems = GetItemsForSale(conn, sale.Id, tx);
            foreach (var old in oldItems)
            {
                if (old.SkipStockDeduction) continue;

                int bagsToCut = old.LooseKgSold > 0.0001 ? 1 : 0;
                int sourceBagsDeducted = old.BagsSold + bagsToCut;
                double morphedPackSize = old.LooseKgSold > 0.0001 ? Math.Round(old.PackSizeKg - old.LooseKgSold, 3) : 0.0;
                int morphedBagsAdded = (old.LooseKgSold > 0.0001 && morphedPackSize > 0.0001) ? 1 : 0;

                _stockRepo.RevertSaleBagMorph(
                    old.ItemId,
                    old.ItemName,
                    old.PackSizeKg,
                    sourceBagsDeducted,
                    morphedPackSize,
                    morphedBagsAdded,
                    sale.InvoiceNo,
                    conn,
                    tx);
            }

            double thresholdKg = _settingsRepo.GetSalesDeductionThresholdKg(conn, tx);

            // 2. Validate stock for updated items that deduct inventory
            foreach (var item in sale.Items)
            {
                bool skipDeduction = item.SkipStockDeduction || !StockCalculationEngine.ShouldDeductInventory(item.TotalKgSold, thresholdKg);
                item.SkipStockDeduction = skipDeduction;

                if (!skipDeduction)
                {
                    var stock = _stockRepo.GetStock(item.ItemId, item.PackSizeKg, conn, tx);
                    int currentBags = stock?.FullBags ?? 0;
                    var morphCheck = StockCalculationEngine.CalculateBagMorph(currentBags, item.PackSizeKg, item.BagsSold, item.LooseKgSold);
                    if (!morphCheck.Success)
                    {
                        throw new InvalidOperationException($"Cannot update sale: {morphCheck.ErrorMessage}");
                    }
                }
            }

            // 3. Update Sale Master
            using (var cmd = new OleDbCommand(@"
                UPDATE Sales 
                SET CustomerName = ?, GodownId = ?, GodownName = ?, SaleDate = ?, TotalBags = ?, TotalLooseKg = ?, TotalWeightKg = ?, SubTotal = ?, GstPercent = ?, GstAmount = ?, Freight = ?, LoadingUnloading = ?, GrandTotal = ?, TotalAmount = ?, Notes = ?, PaymentMode = ?, BankAccountId = ?, BankAccountName = ?, UtrRefNo = ?, CashReceived = ?, UpiReceived = ?, CreditAmount = ?
                WHERE Id = ?", conn, tx))
            {
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = sale.CustomerName ?? string.Empty;
                cmd.Parameters.Add("?", OleDbType.Integer).Value = (object?)sale.GodownId ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)sale.GodownName ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.Date).Value = sale.SaleDate;
                cmd.Parameters.Add("?", OleDbType.Integer).Value = sale.TotalBags;
                cmd.Parameters.Add("?", OleDbType.Double).Value = sale.TotalLooseKg;
                cmd.Parameters.Add("?", OleDbType.Double).Value = sale.TotalWeightKg;
                cmd.Parameters.Add("?", OleDbType.Double).Value = sale.SubTotal;
                cmd.Parameters.Add("?", OleDbType.Double).Value = sale.GstPercent;
                cmd.Parameters.Add("?", OleDbType.Double).Value = sale.GstAmount;
                cmd.Parameters.Add("?", OleDbType.Double).Value = sale.Freight;
                cmd.Parameters.Add("?", OleDbType.Double).Value = sale.LoadingUnloading;
                cmd.Parameters.Add("?", OleDbType.Double).Value = sale.GrandTotal;
                cmd.Parameters.Add("?", OleDbType.Double).Value = sale.GrandTotal > 0 ? sale.GrandTotal : sale.TotalAmount;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)sale.Notes ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = string.IsNullOrWhiteSpace(sale.PaymentMode) ? "Credit" : sale.PaymentMode;
                cmd.Parameters.Add("?", OleDbType.Integer).Value = (object?)sale.BankAccountId ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)sale.BankAccountName ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)sale.UtrRefNo ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.Double).Value = sale.CashReceived;
                cmd.Parameters.Add("?", OleDbType.Double).Value = sale.UpiReceived;
                cmd.Parameters.Add("?", OleDbType.Double).Value = sale.CreditAmount;
                cmd.Parameters.Add("?", OleDbType.Integer).Value = sale.Id;
                cmd.ExecuteNonQuery();
            }

            // 4. Delete old SaleItems
            using (var delCmd = new OleDbCommand("DELETE FROM SaleItems WHERE SaleId = ?", conn, tx))
            {
                delCmd.Parameters.Add("?", OleDbType.Integer).Value = sale.Id;
                delCmd.ExecuteNonQuery();
            }

            // 5. Insert new items and deduct stock with bag morphing (if not skipped)
            foreach (var item in sale.Items)
            {
                bool skipDeduction = item.SkipStockDeduction;

                if (!skipDeduction)
                {
                    var stock = _stockRepo.GetStock(item.ItemId, item.PackSizeKg, conn, tx);
                    int currentBags = stock?.FullBags ?? 0;
                    var morph = StockCalculationEngine.CalculateBagMorph(currentBags, item.PackSizeKg, item.BagsSold, item.LooseKgSold);

                    item.BagsOpened = morph.MorphedBagsAdded;

                    using (var itemCmd = new OleDbCommand(@"
                        INSERT INTO SaleItems (SaleId, ItemId, GodownId, GodownName, PackSizeKg, BagsSold, LooseKgSold, TotalKgSold, RatePerKg, Amount, BagsOpened, SkipStockDeduction)
                        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)", conn, tx))
                    {
                        itemCmd.Parameters.Add("?", OleDbType.Integer).Value = sale.Id;
                        itemCmd.Parameters.Add("?", OleDbType.Integer).Value = item.ItemId;
                        itemCmd.Parameters.Add("?", OleDbType.Integer).Value = (object?)item.GodownId ?? (object?)sale.GodownId ?? DBNull.Value;
                        itemCmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)item.GodownName ?? (object?)sale.GodownName ?? DBNull.Value;
                        itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.PackSizeKg;
                        itemCmd.Parameters.Add("?", OleDbType.Integer).Value = item.BagsSold;
                        itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.LooseKgSold;
                        itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.TotalKgSold;
                        itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.RatePerKg;
                        itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.Amount;
                        itemCmd.Parameters.Add("?", OleDbType.Integer).Value = item.BagsOpened;
                        itemCmd.Parameters.Add("?", OleDbType.Boolean).Value = false;
                        itemCmd.ExecuteNonQuery();
                    }

                    _stockRepo.ApplySaleBagMorph(
                        item.ItemId,
                        item.ItemName,
                        item.PackSizeKg,
                        morph.SourceBagsDeducted,
                        morph.MorphedPackSizeKg,
                        morph.MorphedBagsAdded,
                        item.TotalKgSold,
                        sale.InvoiceNo,
                        sale.CustomerName ?? string.Empty,
                        item.GodownId ?? sale.GodownId,
                        item.GodownName ?? sale.GodownName,
                        conn,
                        tx);
                }
                else
                {
                    item.BagsOpened = 0;

                    using (var itemCmd = new OleDbCommand(@"
                        INSERT INTO SaleItems (SaleId, ItemId, GodownId, GodownName, PackSizeKg, BagsSold, LooseKgSold, TotalKgSold, RatePerKg, Amount, BagsOpened, SkipStockDeduction)
                        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)", conn, tx))
                    {
                        itemCmd.Parameters.Add("?", OleDbType.Integer).Value = sale.Id;
                        itemCmd.Parameters.Add("?", OleDbType.Integer).Value = item.ItemId;
                        itemCmd.Parameters.Add("?", OleDbType.Integer).Value = (object?)item.GodownId ?? (object?)sale.GodownId ?? DBNull.Value;
                        itemCmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)item.GodownName ?? (object?)sale.GodownName ?? DBNull.Value;
                        itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.PackSizeKg;
                        itemCmd.Parameters.Add("?", OleDbType.Integer).Value = item.BagsSold;
                        itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.LooseKgSold;
                        itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.TotalKgSold;
                        itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.RatePerKg;
                        itemCmd.Parameters.Add("?", OleDbType.Double).Value = item.Amount;
                        itemCmd.Parameters.Add("?", OleDbType.Integer).Value = 0;
                        itemCmd.Parameters.Add("?", OleDbType.Boolean).Value = true;
                        itemCmd.ExecuteNonQuery();
                    }
                }
            }

            // 6. Update customer account ledger entries
            PostSaleLedgers(sale, conn, tx, isModification: true);

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private void PostSaleLedgers(Sale sale, OleDbConnection conn, OleDbTransaction tx, bool isModification)
    {
        if (string.IsNullOrWhiteSpace(sale.CustomerName)) return;

        // 1. Remove previous ledger postings for this invoice
        using (var delLedger = new OleDbCommand("DELETE FROM AccountLedger WHERE RefInvoiceNo = ?", conn, tx))
        {
            delLedger.Parameters.Add("?", OleDbType.VarWChar).Value = sale.InvoiceNo;
            delLedger.ExecuteNonQuery();
        }

        var partyAccount = _accountRepo.EnsureAccountExists(sale.CustomerName, "Customer", conn, tx);
        double postedAmount = sale.PartyAmount > 0 ? sale.PartyAmount : (sale.SubTotal + sale.GstAmount);
        string prefix = isModification ? "Modified " : "";

        // 2. Post Sale Invoice DEBIT to Party
        _accountRepo.PostLedgerEntry(new AccountLedgerEntry
        {
            AccountId = partyAccount.Id,
            AccountName = partyAccount.AccountName,
            TransactionType = isModification ? "SALE_MODIFIED" : "SALE",
            RefInvoiceNo = sale.InvoiceNo,
            Debit = postedAmount,
            Credit = 0.0,
            Balance = 0.0,
            Remarks = $"{prefix}Sales Bill #{sale.InvoiceNo}: Bill Amount ₹{sale.SubTotal:N2} + GST ₹{sale.GstAmount:N2} = ₹{postedAmount:N2} [Mode: {sale.PaymentMode}]",
            Timestamp = sale.SaleDate
        }, conn, tx);

        // 3. Post Immediate Cash Receipt if CashReceived > 0
        if (sale.CashReceived > 0.0001)
        {
            var cashAccount = _accountRepo.EnsureAccountExists("Cash in Hand", "Cash", conn, tx);

            // Debit Cash in Hand
            _accountRepo.PostLedgerEntry(new AccountLedgerEntry
            {
                AccountId = cashAccount.Id,
                AccountName = cashAccount.AccountName,
                TransactionType = "RECEIPT",
                RefInvoiceNo = sale.InvoiceNo,
                Debit = sale.CashReceived,
                Credit = 0.0,
                Balance = 0.0,
                Remarks = $"Cash received for Bill #{sale.InvoiceNo} from {partyAccount.AccountName}",
                Timestamp = sale.SaleDate
            }, conn, tx);

            // Credit Party
            _accountRepo.PostLedgerEntry(new AccountLedgerEntry
            {
                AccountId = partyAccount.Id,
                AccountName = partyAccount.AccountName,
                TransactionType = "RECEIPT",
                RefInvoiceNo = sale.InvoiceNo,
                Debit = 0.0,
                Credit = sale.CashReceived,
                Balance = 0.0,
                Remarks = $"Cash paid for Bill #{sale.InvoiceNo}",
                Timestamp = sale.SaleDate
            }, conn, tx);
        }

        // 4. Post Immediate UPI Receipt if UpiReceived > 0
        if (sale.UpiReceived > 0.0001)
        {
            string bankName = !string.IsNullOrWhiteSpace(sale.BankAccountName) ? sale.BankAccountName : "Primary UPI / Bank Account";
            var bankAccount = _accountRepo.EnsureAccountExists(bankName, "Bank", conn, tx);
            string utrNote = !string.IsNullOrWhiteSpace(sale.UtrRefNo) ? $" [UTR: {sale.UtrRefNo}]" : "";

            // Debit Bank/UPI Account
            _accountRepo.PostLedgerEntry(new AccountLedgerEntry
            {
                AccountId = bankAccount.Id,
                AccountName = bankAccount.AccountName,
                TransactionType = "RECEIPT",
                RefInvoiceNo = sale.InvoiceNo,
                Debit = sale.UpiReceived,
                Credit = 0.0,
                Balance = 0.0,
                Remarks = $"UPI received into {bankAccount.AccountName} for Bill #{sale.InvoiceNo} from {partyAccount.AccountName}{utrNote}",
                Timestamp = sale.SaleDate
            }, conn, tx);

            // Credit Party
            _accountRepo.PostLedgerEntry(new AccountLedgerEntry
            {
                AccountId = partyAccount.Id,
                AccountName = partyAccount.AccountName,
                TransactionType = "RECEIPT",
                RefInvoiceNo = sale.InvoiceNo,
                Debit = 0.0,
                Credit = sale.UpiReceived,
                Balance = 0.0,
                Remarks = $"UPI payment for Bill #{sale.InvoiceNo} into {bankAccount.AccountName}{utrNote}",
                Timestamp = sale.SaleDate
            }, conn, tx);
        }
    }
}
