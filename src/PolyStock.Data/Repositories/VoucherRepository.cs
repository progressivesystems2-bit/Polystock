using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Linq;
using PolyStock.Core.Models;

namespace PolyStock.Data.Repositories;

public class VoucherRepository
{
    private readonly string? _dbPath;
    private readonly AccountRepository _accountRepo;

    public VoucherRepository(string? dbPath = null)
    {
        _dbPath = dbPath;
        _accountRepo = new AccountRepository(dbPath);
    }

    public List<FinancialVoucher> GetAll()
    {
        var list = new List<FinancialVoucher>();
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        using var cmd = new OleDbCommand(@"
            SELECT Id, VoucherType, VoucherNo, VoucherDate, PrimaryAccountId, PrimaryAccountName, TotalAmount, DebitAccountId, DebitAccountName, CreditAccountId, CreditAccountName, Amount, PaymentMode, ReferenceNo, ReferenceDate, Narration, CreatedAt
            FROM Vouchers
            ORDER BY VoucherDate DESC, Id DESC", conn);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(ReadVoucher(reader));
        }

        // Load items for each voucher
        foreach (var v in list)
        {
            v.Items = GetItemsForVoucher(v.Id, conn);
        }

        return list;
    }

    public FinancialVoucher? GetById(int id)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        using var cmd = new OleDbCommand(@"
            SELECT Id, VoucherType, VoucherNo, VoucherDate, PrimaryAccountId, PrimaryAccountName, TotalAmount, DebitAccountId, DebitAccountName, CreditAccountId, CreditAccountName, Amount, PaymentMode, ReferenceNo, ReferenceDate, Narration, CreatedAt
            FROM Vouchers
            WHERE Id = ?", conn);

        cmd.Parameters.Add("?", OleDbType.Integer).Value = id;
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var v = ReadVoucher(reader);
            v.Items = GetItemsForVoucher(v.Id, conn);
            return v;
        }

        return null;
    }

    public FinancialVoucher? GetByVoucherNo(string voucherNo)
    {
        if (string.IsNullOrWhiteSpace(voucherNo)) return null;
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        using var cmd = new OleDbCommand("SELECT TOP 1 Id FROM Vouchers WHERE VoucherNo = ?", conn);
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = voucherNo.Trim();
        var val = cmd.ExecuteScalar();
        if (val != null && val != DBNull.Value)
        {
            return GetById(Convert.ToInt32(val));
        }
        return null;
    }

    public List<FinancialVoucher> GetByType(string voucherType)
    {
        var list = new List<FinancialVoucher>();
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        using var cmd = new OleDbCommand(@"
            SELECT Id, VoucherType, VoucherNo, VoucherDate, PrimaryAccountId, PrimaryAccountName, TotalAmount, DebitAccountId, DebitAccountName, CreditAccountId, CreditAccountName, Amount, PaymentMode, ReferenceNo, ReferenceDate, Narration, CreatedAt
            FROM Vouchers
            WHERE VoucherType = ?
            ORDER BY VoucherDate DESC, Id DESC", conn);

        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = voucherType.ToUpperInvariant();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(ReadVoucher(reader));
        }

        foreach (var v in list)
        {
            v.Items = GetItemsForVoucher(v.Id, conn);
        }

        return list;
    }

    public string GetNextVoucherNo(string voucherType, OleDbConnection? conn = null)
    {
        bool closeConn = false;
        if (conn == null)
        {
            conn = AccessConnectionProvider.CreateConnection(_dbPath);
            conn.Open();
            closeConn = true;
        }

        try
        {
            string prefix = voucherType.ToUpperInvariant() switch
            {
                "RECEIPT" => "REC",
                "PAYMENT" => "PAY",
                "JOURNAL" => "JRN",
                _ => "VCH"
            };

            string dateStr = DateTime.Now.ToString("yyyyMMdd");
            using var cmd = new OleDbCommand("SELECT COUNT(*) FROM Vouchers WHERE VoucherType = ?", conn);
            cmd.Parameters.Add("?", OleDbType.VarWChar).Value = voucherType.ToUpperInvariant();
            int count = Convert.ToInt32(cmd.ExecuteScalar() ?? 0) + 1;

            return $"{prefix}-{dateStr}-{count:D4}";
        }
        finally
        {
            if (closeConn) conn.Dispose();
        }
    }

    public int SaveVoucher(FinancialVoucher voucher)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            if (string.IsNullOrWhiteSpace(voucher.VoucherNo))
            {
                voucher.VoucherNo = GetNextVoucherNo(voucher.VoucherType, conn);
            }

            PrepareVoucherForPersistence(voucher);

            // 1. Insert Voucher Master
            using (var cmd = new OleDbCommand(@"
                INSERT INTO Vouchers (VoucherType, VoucherNo, VoucherDate, PrimaryAccountId, PrimaryAccountName, TotalAmount, DebitAccountId, DebitAccountName, CreditAccountId, CreditAccountName, Amount, PaymentMode, ReferenceNo, ReferenceDate, Narration, CreatedAt)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)", conn, tx))
            {
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = voucher.VoucherType.ToUpperInvariant();
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = voucher.VoucherNo;
                cmd.Parameters.Add("?", OleDbType.Date).Value = voucher.VoucherDate;
                cmd.Parameters.Add("?", OleDbType.Integer).Value = (object?)voucher.PrimaryAccountId ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = voucher.PrimaryAccountName;
                cmd.Parameters.Add("?", OleDbType.Double).Value = voucher.TotalAmount;
                cmd.Parameters.Add("?", OleDbType.Integer).Value = (object?)voucher.DebitAccountId ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = voucher.DebitAccountName;
                cmd.Parameters.Add("?", OleDbType.Integer).Value = (object?)voucher.CreditAccountId ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = voucher.CreditAccountName;
                cmd.Parameters.Add("?", OleDbType.Double).Value = voucher.TotalAmount;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)voucher.PaymentMode ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)voucher.ReferenceNo ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.Date).Value = (object?)voucher.ReferenceDate ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)voucher.Narration ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.Date).Value = voucher.CreatedAt == DateTime.MinValue ? DateTime.Now : voucher.CreatedAt;
                cmd.ExecuteNonQuery();
            }

            using var idCmd = new OleDbCommand("SELECT @@IDENTITY", conn, tx);
            int voucherId = Convert.ToInt32(idCmd.ExecuteScalar());
            voucher.Id = voucherId;

            // 2. Insert Voucher Items
            InsertVoucherItems(voucher, conn, tx);

            // 3. Post Compound Double-Entry Ledger
            PostCompoundDoubleEntryLedger(voucher, conn, tx);

            tx.Commit();
            return voucherId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public void UpdateVoucher(FinancialVoucher voucher)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            PrepareVoucherForPersistence(voucher);

            // 1. Revert prior ledger postings and voucher items
            using (var delLedger = new OleDbCommand("DELETE FROM AccountLedger WHERE RefInvoiceNo = ?", conn, tx))
            {
                delLedger.Parameters.Add("?", OleDbType.VarWChar).Value = voucher.VoucherNo;
                delLedger.ExecuteNonQuery();
            }

            using (var delItems = new OleDbCommand("DELETE FROM VoucherItems WHERE VoucherId = ?", conn, tx))
            {
                delItems.Parameters.Add("?", OleDbType.Integer).Value = voucher.Id;
                delItems.ExecuteNonQuery();
            }

            // 2. Update Voucher Record
            using (var cmd = new OleDbCommand(@"
                UPDATE Vouchers
                SET VoucherType = ?, VoucherDate = ?, PrimaryAccountId = ?, PrimaryAccountName = ?, TotalAmount = ?, DebitAccountId = ?, DebitAccountName = ?, CreditAccountId = ?, CreditAccountName = ?, Amount = ?, PaymentMode = ?, ReferenceNo = ?, ReferenceDate = ?, Narration = ?
                WHERE Id = ?", conn, tx))
            {
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = voucher.VoucherType.ToUpperInvariant();
                cmd.Parameters.Add("?", OleDbType.Date).Value = voucher.VoucherDate;
                cmd.Parameters.Add("?", OleDbType.Integer).Value = (object?)voucher.PrimaryAccountId ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = voucher.PrimaryAccountName;
                cmd.Parameters.Add("?", OleDbType.Double).Value = voucher.TotalAmount;
                cmd.Parameters.Add("?", OleDbType.Integer).Value = (object?)voucher.DebitAccountId ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = voucher.DebitAccountName;
                cmd.Parameters.Add("?", OleDbType.Integer).Value = (object?)voucher.CreditAccountId ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = voucher.CreditAccountName;
                cmd.Parameters.Add("?", OleDbType.Double).Value = voucher.TotalAmount;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)voucher.PaymentMode ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)voucher.ReferenceNo ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.Date).Value = (object?)voucher.ReferenceDate ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)voucher.Narration ?? DBNull.Value;
                cmd.Parameters.Add("?", OleDbType.Integer).Value = voucher.Id;
                cmd.ExecuteNonQuery();
            }

            // 3. Insert Items and Post Ledger
            InsertVoucherItems(voucher, conn, tx);
            PostCompoundDoubleEntryLedger(voucher, conn, tx);

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public void DeleteVoucher(int id)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            var voucher = GetById(id);
            if (voucher != null)
            {
                using var delLedger = new OleDbCommand("DELETE FROM AccountLedger WHERE RefInvoiceNo = ?", conn, tx);
                delLedger.Parameters.Add("?", OleDbType.VarWChar).Value = voucher.VoucherNo;
                delLedger.ExecuteNonQuery();

                using var delItems = new OleDbCommand("DELETE FROM VoucherItems WHERE VoucherId = ?", conn, tx);
                delItems.Parameters.Add("?", OleDbType.Integer).Value = id;
                delItems.ExecuteNonQuery();

                using var delCmd = new OleDbCommand("DELETE FROM Vouchers WHERE Id = ?", conn, tx);
                delCmd.Parameters.Add("?", OleDbType.Integer).Value = id;
                delCmd.ExecuteNonQuery();
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private void PrepareVoucherForPersistence(FinancialVoucher voucher)
    {
        if (voucher.Items == null || voucher.Items.Count == 0)
        {
            // Backward-compatibility: create 1 line item from Debit/Credit
            string partyName = voucher.VoucherType.Equals("RECEIPT", StringComparison.OrdinalIgnoreCase)
                ? voucher.CreditAccountName
                : voucher.DebitAccountName;
            int? partyId = voucher.VoucherType.Equals("RECEIPT", StringComparison.OrdinalIgnoreCase)
                ? voucher.CreditAccountId
                : voucher.DebitAccountId;

            voucher.Items = new List<VoucherLineItem>
            {
                new VoucherLineItem
                {
                    AccountId = partyId,
                    AccountName = partyName,
                    Amount = voucher.Amount,
                    ReferenceNo = voucher.ReferenceNo,
                    Remarks = voucher.Narration
                }
            };
        }

        voucher.TotalAmount = voucher.Items.Sum(i => i.Amount);
        voucher.Amount = voucher.TotalAmount;

        if (string.IsNullOrWhiteSpace(voucher.PrimaryAccountName))
        {
            voucher.PrimaryAccountName = voucher.VoucherType.Equals("RECEIPT", StringComparison.OrdinalIgnoreCase)
                ? voucher.DebitAccountName
                : voucher.CreditAccountName;
            voucher.PrimaryAccountId = voucher.VoucherType.Equals("RECEIPT", StringComparison.OrdinalIgnoreCase)
                ? voucher.DebitAccountId
                : voucher.CreditAccountId;
        }

        if (voucher.VoucherType.Equals("RECEIPT", StringComparison.OrdinalIgnoreCase))
        {
            voucher.DebitAccountName = voucher.PrimaryAccountName;
            voucher.DebitAccountId = voucher.PrimaryAccountId;
            voucher.CreditAccountName = voucher.Items.Count > 0 ? voucher.Items[0].AccountName : string.Empty;
        }
        else if (voucher.VoucherType.Equals("PAYMENT", StringComparison.OrdinalIgnoreCase))
        {
            voucher.CreditAccountName = voucher.PrimaryAccountName;
            voucher.CreditAccountId = voucher.PrimaryAccountId;
            voucher.DebitAccountName = voucher.Items.Count > 0 ? voucher.Items[0].AccountName : string.Empty;
        }
    }

    private void InsertVoucherItems(FinancialVoucher voucher, OleDbConnection conn, OleDbTransaction tx)
    {
        foreach (var item in voucher.Items)
        {
            if (item.Amount <= 0.0001) continue;

            using var cmd = new OleDbCommand(@"
                INSERT INTO VoucherItems (VoucherId, AccountId, AccountName, Amount, ReferenceNo, Remarks)
                VALUES (?, ?, ?, ?, ?, ?)", conn, tx);

            cmd.Parameters.Add("?", OleDbType.Integer).Value = voucher.Id;
            cmd.Parameters.Add("?", OleDbType.Integer).Value = (object?)item.AccountId ?? DBNull.Value;
            cmd.Parameters.Add("?", OleDbType.VarWChar).Value = item.AccountName;
            cmd.Parameters.Add("?", OleDbType.Double).Value = item.Amount;
            cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)item.ReferenceNo ?? DBNull.Value;
            cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)item.Remarks ?? DBNull.Value;
            cmd.ExecuteNonQuery();
        }
    }

    private void PostCompoundDoubleEntryLedger(FinancialVoucher voucher, OleDbConnection conn, OleDbTransaction tx)
    {
        string refDetails = !string.IsNullOrWhiteSpace(voucher.ReferenceNo) ? $" [Ref: {voucher.ReferenceNo}]" : "";
        string noteDetails = !string.IsNullOrWhiteSpace(voucher.Narration) ? $" - {voucher.Narration}" : "";

        if (voucher.VoucherType.Equals("RECEIPT", StringComparison.OrdinalIgnoreCase))
        {
            // 1. Primary Account (Cash/Bank) is DEBITED with TotalAmount
            var primaryAccount = _accountRepo.EnsureAccountExists(voucher.PrimaryAccountName, "Cash", conn, tx);
            string primaryRemarks = voucher.Items.Count > 1
                ? $"Receipt into {voucher.PrimaryAccountName} from {voucher.Items.Count} parties via {voucher.PaymentMode}{refDetails}{noteDetails}"
                : $"Receipt from {voucher.Items.FirstOrDefault()?.AccountName} via {voucher.PaymentMode}{refDetails}{noteDetails}";

            _accountRepo.PostLedgerEntry(new AccountLedgerEntry
            {
                AccountId = primaryAccount.Id,
                AccountName = primaryAccount.AccountName,
                TransactionType = "RECEIPT",
                RefInvoiceNo = voucher.VoucherNo,
                Debit = voucher.TotalAmount,
                Credit = 0.0,
                Balance = 0.0,
                Remarks = primaryRemarks,
                Timestamp = voucher.VoucherDate
            }, conn, tx);

            // 2. Each Party/Income line is CREDITED with its line amount
            foreach (var item in voucher.Items)
            {
                if (item.Amount <= 0.0001) continue;

                var partyAccount = _accountRepo.EnsureAccountExists(item.AccountName, "Customer", conn, tx);
                string lineRef = !string.IsNullOrWhiteSpace(item.ReferenceNo) ? $" [Ref: {item.ReferenceNo}]" : refDetails;
                string lineNote = !string.IsNullOrWhiteSpace(item.Remarks) ? $" - {item.Remarks}" : noteDetails;
                string lineRemarks = $"Payment received into {voucher.PrimaryAccountName} via {voucher.PaymentMode}{lineRef}{lineNote}";

                _accountRepo.PostLedgerEntry(new AccountLedgerEntry
                {
                    AccountId = partyAccount.Id,
                    AccountName = partyAccount.AccountName,
                    TransactionType = "RECEIPT",
                    RefInvoiceNo = voucher.VoucherNo,
                    Debit = 0.0,
                    Credit = item.Amount,
                    Balance = 0.0,
                    Remarks = lineRemarks,
                    Timestamp = voucher.VoucherDate
                }, conn, tx);
            }
        }
        else if (voucher.VoucherType.Equals("PAYMENT", StringComparison.OrdinalIgnoreCase))
        {
            // 1. Primary Account (Cash/Bank) is CREDITED with TotalAmount
            var primaryAccount = _accountRepo.EnsureAccountExists(voucher.PrimaryAccountName, "Cash", conn, tx);
            string primaryRemarks = voucher.Items.Count > 1
                ? $"Payment from {voucher.PrimaryAccountName} to {voucher.Items.Count} parties via {voucher.PaymentMode}{refDetails}{noteDetails}"
                : $"Payment to {voucher.Items.FirstOrDefault()?.AccountName} via {voucher.PaymentMode}{refDetails}{noteDetails}";

            _accountRepo.PostLedgerEntry(new AccountLedgerEntry
            {
                AccountId = primaryAccount.Id,
                AccountName = primaryAccount.AccountName,
                TransactionType = "PAYMENT",
                RefInvoiceNo = voucher.VoucherNo,
                Debit = 0.0,
                Credit = voucher.TotalAmount,
                Balance = 0.0,
                Remarks = primaryRemarks,
                Timestamp = voucher.VoucherDate
            }, conn, tx);

            // 2. Each Supplier/Expense line is DEBITED with its line amount
            foreach (var item in voucher.Items)
            {
                if (item.Amount <= 0.0001) continue;

                var partyAccount = _accountRepo.EnsureAccountExists(item.AccountName, "Supplier", conn, tx);
                string lineRef = !string.IsNullOrWhiteSpace(item.ReferenceNo) ? $" [Ref: {item.ReferenceNo}]" : refDetails;
                string lineNote = !string.IsNullOrWhiteSpace(item.Remarks) ? $" - {item.Remarks}" : noteDetails;
                string lineRemarks = $"Payment from {voucher.PrimaryAccountName} via {voucher.PaymentMode}{lineRef}{lineNote}";

                _accountRepo.PostLedgerEntry(new AccountLedgerEntry
                {
                    AccountId = partyAccount.Id,
                    AccountName = partyAccount.AccountName,
                    TransactionType = "PAYMENT",
                    RefInvoiceNo = voucher.VoucherNo,
                    Debit = item.Amount,
                    Credit = 0.0,
                    Balance = 0.0,
                    Remarks = lineRemarks,
                    Timestamp = voucher.VoucherDate
                }, conn, tx);
            }
        }
        else // JOURNAL
        {
            var drAccount = _accountRepo.EnsureAccountExists(voucher.DebitAccountName, "General", conn, tx);
            var crAccount = _accountRepo.EnsureAccountExists(voucher.CreditAccountName, "General", conn, tx);

            _accountRepo.PostLedgerEntry(new AccountLedgerEntry
            {
                AccountId = drAccount.Id,
                AccountName = drAccount.AccountName,
                TransactionType = "JOURNAL",
                RefInvoiceNo = voucher.VoucherNo,
                Debit = voucher.TotalAmount,
                Credit = 0.0,
                Balance = 0.0,
                Remarks = $"Journal Adjustment with {voucher.CreditAccountName}{refDetails}{noteDetails}",
                Timestamp = voucher.VoucherDate
            }, conn, tx);

            _accountRepo.PostLedgerEntry(new AccountLedgerEntry
            {
                AccountId = crAccount.Id,
                AccountName = crAccount.AccountName,
                TransactionType = "JOURNAL",
                RefInvoiceNo = voucher.VoucherNo,
                Debit = 0.0,
                Credit = voucher.TotalAmount,
                Balance = 0.0,
                Remarks = $"Journal Adjustment with {voucher.DebitAccountName}{refDetails}{noteDetails}",
                Timestamp = voucher.VoucherDate
            }, conn, tx);
        }
    }

    private List<VoucherLineItem> GetItemsForVoucher(int voucherId, OleDbConnection conn)
    {
        var items = new List<VoucherLineItem>();
        using var cmd = new OleDbCommand(@"
            SELECT Id, VoucherId, AccountId, AccountName, Amount, ReferenceNo, Remarks
            FROM VoucherItems
            WHERE VoucherId = ?
            ORDER BY Id ASC", conn);

        cmd.Parameters.Add("?", OleDbType.Integer).Value = voucherId;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new VoucherLineItem
            {
                Id = Convert.ToInt32(reader["Id"]),
                VoucherId = Convert.ToInt32(reader["VoucherId"]),
                AccountId = reader["AccountId"] != DBNull.Value ? Convert.ToInt32(reader["AccountId"]) : null,
                AccountName = reader["AccountName"]?.ToString() ?? string.Empty,
                Amount = reader["Amount"] != DBNull.Value ? Convert.ToDouble(reader["Amount"]) : 0.0,
                ReferenceNo = reader["ReferenceNo"]?.ToString(),
                Remarks = reader["Remarks"]?.ToString()
            });
        }

        return items;
    }

    private static FinancialVoucher ReadVoucher(OleDbDataReader reader)
    {
        string vType = reader["VoucherType"]?.ToString() ?? "RECEIPT";
        double totalAmt = 0.0;
        try
        {
            if (reader["TotalAmount"] != DBNull.Value)
            {
                totalAmt = Convert.ToDouble(reader["TotalAmount"]);
            }
        }
        catch { }

        if (totalAmt <= 0 && reader["Amount"] != DBNull.Value)
        {
            totalAmt = Convert.ToDouble(reader["Amount"]);
        }

        string primaryName = string.Empty;
        int? primaryId = null;
        try
        {
            primaryName = reader["PrimaryAccountName"]?.ToString() ?? string.Empty;
            primaryId = reader["PrimaryAccountId"] != DBNull.Value ? Convert.ToInt32(reader["PrimaryAccountId"]) : null;
        }
        catch { }

        string drName = reader["DebitAccountName"]?.ToString() ?? string.Empty;
        string crName = reader["CreditAccountName"]?.ToString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(primaryName))
        {
            primaryName = vType.Equals("RECEIPT", StringComparison.OrdinalIgnoreCase) ? drName : crName;
        }

        return new FinancialVoucher
        {
            Id = Convert.ToInt32(reader["Id"]),
            VoucherType = vType,
            VoucherNo = reader["VoucherNo"]?.ToString() ?? string.Empty,
            VoucherDate = Convert.ToDateTime(reader["VoucherDate"]),
            PrimaryAccountId = primaryId,
            PrimaryAccountName = primaryName,
            TotalAmount = totalAmt,
            DebitAccountId = reader["DebitAccountId"] != DBNull.Value ? Convert.ToInt32(reader["DebitAccountId"]) : null,
            DebitAccountName = drName,
            CreditAccountId = reader["CreditAccountId"] != DBNull.Value ? Convert.ToInt32(reader["CreditAccountId"]) : null,
            CreditAccountName = crName,
            Amount = totalAmt,
            PaymentMode = reader["PaymentMode"]?.ToString() ?? "Cash",
            ReferenceNo = reader["ReferenceNo"]?.ToString(),
            ReferenceDate = reader["ReferenceDate"] != DBNull.Value ? Convert.ToDateTime(reader["ReferenceDate"]) : null,
            Narration = reader["Narration"]?.ToString(),
            CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue
        };
    }
}
