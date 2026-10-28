using System;
using System.Collections.Generic;
using System.Data.OleDb;
using PolyStock.Core.Models;

namespace PolyStock.Data.Repositories;

public class AccountRepository
{
    private readonly string? _dbPath;
    public AccountRepository(string? dbPath = null) => _dbPath = dbPath;

    public List<Account> GetAll()
    {
        var list = new List<Account>();
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        using (var cmd = new OleDbCommand("SELECT Id, AccountType, AccountName, Address, Mobile, Email, GSTIN, OpeningBalance, OpeningBalanceType, [Description], CreatedAt FROM Accounts ORDER BY AccountName", conn))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                list.Add(new Account
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    AccountType = reader["AccountType"]?.ToString() ?? "Customer",
                    AccountName = reader["AccountName"]?.ToString() ?? string.Empty,
                    Address = reader["Address"]?.ToString(),
                    Mobile = reader["Mobile"]?.ToString(),
                    Email = reader["Email"]?.ToString(),
                    GSTIN = reader["GSTIN"]?.ToString(),
                    OpeningBalance = reader["OpeningBalance"] != DBNull.Value ? Convert.ToDouble(reader["OpeningBalance"]) : 0.0,
                    OpeningBalanceType = reader["OpeningBalanceType"]?.ToString() ?? "Dr",
                    Description = reader["Description"]?.ToString(),
                    CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue
                });
            }
        }

        // Calculate live balances from AccountLedger
        foreach (var acc in list)
        {
            ComputeAccountBalances(acc, conn);
        }

        return list;
    }

    public Account? GetById(int id)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        using var cmd = new OleDbCommand("SELECT Id, AccountType, AccountName, Address, Mobile, Email, GSTIN, OpeningBalance, OpeningBalanceType, [Description], CreatedAt FROM Accounts WHERE Id = ?", conn);
        cmd.Parameters.Add("?", OleDbType.Integer).Value = id;
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var acc = new Account
            {
                Id = Convert.ToInt32(reader["Id"]),
                AccountType = reader["AccountType"]?.ToString() ?? "Customer",
                AccountName = reader["AccountName"]?.ToString() ?? string.Empty,
                Address = reader["Address"]?.ToString(),
                Mobile = reader["Mobile"]?.ToString(),
                Email = reader["Email"]?.ToString(),
                GSTIN = reader["GSTIN"]?.ToString(),
                OpeningBalance = reader["OpeningBalance"] != DBNull.Value ? Convert.ToDouble(reader["OpeningBalance"]) : 0.0,
                OpeningBalanceType = reader["OpeningBalanceType"]?.ToString() ?? "Dr",
                Description = reader["Description"]?.ToString(),
                CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue
            };

            ComputeAccountBalances(acc, conn);
            return acc;
        }
        return null;
    }

    public Account? GetByName(string accountName, OleDbConnection? conn = null, OleDbTransaction? tx = null)
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
            using var cmd = new OleDbCommand("SELECT Id, AccountType, AccountName, Address, Mobile, Email, GSTIN, OpeningBalance, OpeningBalanceType, [Description], CreatedAt FROM Accounts WHERE AccountName = ?", conn, tx);
            cmd.Parameters.Add("?", OleDbType.VarWChar).Value = accountName.Trim();
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                var acc = new Account
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    AccountType = reader["AccountType"]?.ToString() ?? "Customer",
                    AccountName = reader["AccountName"]?.ToString() ?? string.Empty,
                    Address = reader["Address"]?.ToString(),
                    Mobile = reader["Mobile"]?.ToString(),
                    Email = reader["Email"]?.ToString(),
                    GSTIN = reader["GSTIN"]?.ToString(),
                    OpeningBalance = reader["OpeningBalance"] != DBNull.Value ? Convert.ToDouble(reader["OpeningBalance"]) : 0.0,
                    OpeningBalanceType = reader["OpeningBalanceType"]?.ToString() ?? "Dr",
                    Description = reader["Description"]?.ToString(),
                    CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue
                };
                return acc;
            }
            return null;
        }
        finally
        {
            if (closeConn) conn.Dispose();
        }
    }

    public Account EnsureAccountExists(string accountName, string defaultType, OleDbConnection conn, OleDbTransaction? tx = null)
    {
        var existing = GetByName(accountName, conn, tx);
        if (existing != null) return existing;

        var newAcc = new Account
        {
            AccountName = accountName.Trim(),
            AccountType = defaultType,
            OpeningBalance = 0.0,
            OpeningBalanceType = defaultType == "Supplier" ? "-" : "+",
            CreatedAt = DateTime.Now
        };

        using (var cmd = new OleDbCommand("INSERT INTO Accounts (AccountType, AccountName, Address, Mobile, Email, GSTIN, OpeningBalance, OpeningBalanceType, [Description], CreatedAt) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)", conn, tx))
        {
            cmd.Parameters.Add("?", OleDbType.VarWChar).Value = newAcc.AccountType;
            cmd.Parameters.Add("?", OleDbType.VarWChar).Value = newAcc.AccountName;
            cmd.Parameters.Add("?", OleDbType.VarWChar).Value = DBNull.Value;
            cmd.Parameters.Add("?", OleDbType.VarWChar).Value = DBNull.Value;
            cmd.Parameters.Add("?", OleDbType.VarWChar).Value = DBNull.Value;
            cmd.Parameters.Add("?", OleDbType.VarWChar).Value = DBNull.Value;
            cmd.Parameters.Add("?", OleDbType.Double).Value = 0.0;
            cmd.Parameters.Add("?", OleDbType.VarWChar).Value = newAcc.OpeningBalanceType;
            cmd.Parameters.Add("?", OleDbType.VarWChar).Value = DBNull.Value;
            cmd.Parameters.Add("?", OleDbType.Date).Value = DateTime.Now;
            cmd.ExecuteNonQuery();
        }

        using (var idCmd = new OleDbCommand("SELECT @@IDENTITY", conn, tx))
        {
            newAcc.Id = Convert.ToInt32(idCmd.ExecuteScalar());
        }

        return newAcc;
    }

    public void PostLedgerEntry(AccountLedgerEntry entry, OleDbConnection conn, OleDbTransaction? tx = null)
    {
        using var cmd = new OleDbCommand(@"
            INSERT INTO AccountLedger (AccountId, AccountName, TransactionType, RefInvoiceNo, Debit, Credit, Balance, Remarks, [Timestamp])
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)", conn, tx);

        cmd.Parameters.Add("?", OleDbType.Integer).Value = (object?)entry.AccountId ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = entry.AccountName;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = entry.TransactionType;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)entry.RefInvoiceNo ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.Double).Value = entry.Debit;
        cmd.Parameters.Add("?", OleDbType.Double).Value = entry.Credit;
        cmd.Parameters.Add("?", OleDbType.Double).Value = entry.Balance;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)entry.Remarks ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.Date).Value = entry.Timestamp == DateTime.MinValue ? DateTime.Now : entry.Timestamp;
        cmd.ExecuteNonQuery();
    }

    public List<AccountLedgerEntry> GetLedgerEntries(int? accountId = null, string? accountName = null)
    {
        var list = new List<AccountLedgerEntry>();
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        string sql = "SELECT Id, AccountId, AccountName, TransactionType, RefInvoiceNo, Debit, Credit, Balance, Remarks, [Timestamp] FROM AccountLedger";
        if (accountId.HasValue)
        {
            sql += " WHERE AccountId = ? ORDER BY [Timestamp] DESC, Id DESC";
        }
        else if (!string.IsNullOrWhiteSpace(accountName))
        {
            sql += " WHERE AccountName = ? ORDER BY [Timestamp] DESC, Id DESC";
        }
        else
        {
            sql += " ORDER BY [Timestamp] DESC, Id DESC";
        }

        using var cmd = new OleDbCommand(sql, conn);
        if (accountId.HasValue) cmd.Parameters.Add("?", OleDbType.Integer).Value = accountId.Value;
        else if (!string.IsNullOrWhiteSpace(accountName)) cmd.Parameters.Add("?", OleDbType.VarWChar).Value = accountName.Trim();

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new AccountLedgerEntry
            {
                Id = Convert.ToInt32(reader["Id"]),
                AccountId = reader["AccountId"] != DBNull.Value ? Convert.ToInt32(reader["AccountId"]) : null,
                AccountName = reader["AccountName"]?.ToString() ?? string.Empty,
                TransactionType = reader["TransactionType"]?.ToString() ?? string.Empty,
                RefInvoiceNo = reader["RefInvoiceNo"]?.ToString() ?? string.Empty,
                Debit = reader["Debit"] != DBNull.Value ? Convert.ToDouble(reader["Debit"]) : 0.0,
                Credit = reader["Credit"] != DBNull.Value ? Convert.ToDouble(reader["Credit"]) : 0.0,
                Balance = reader["Balance"] != DBNull.Value ? Convert.ToDouble(reader["Balance"]) : 0.0,
                Remarks = reader["Remarks"]?.ToString(),
                Timestamp = reader["Timestamp"] != DBNull.Value ? Convert.ToDateTime(reader["Timestamp"]) : DateTime.MinValue
            });
        }
        return list;
    }

    private void ComputeAccountBalances(Account acc, OleDbConnection conn)
    {
        try
        {
            using var cmd = new OleDbCommand("SELECT SUM(Debit) AS TotalDebit, SUM(Credit) AS TotalCredit FROM AccountLedger WHERE AccountId = ? OR AccountName = ?", conn);
            cmd.Parameters.Add("?", OleDbType.Integer).Value = acc.Id;
            cmd.Parameters.Add("?", OleDbType.VarWChar).Value = acc.AccountName;
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                acc.TotalDebit = reader["TotalDebit"] != DBNull.Value ? Convert.ToDouble(reader["TotalDebit"]) : 0.0;
                acc.TotalCredit = reader["TotalCredit"] != DBNull.Value ? Convert.ToDouble(reader["TotalCredit"]) : 0.0;
            }

            double openingSigned = (acc.OpeningBalanceType == "Cr" || acc.OpeningBalanceType == "-") ? -acc.OpeningBalance : acc.OpeningBalance;
            acc.CurrentBalance = openingSigned + acc.TotalDebit - acc.TotalCredit;
        }
        catch
        {
            // Table might not exist or empty
            acc.CurrentBalance = (acc.OpeningBalanceType == "Cr" || acc.OpeningBalanceType == "-") ? -acc.OpeningBalance : acc.OpeningBalance;
        }
    }

    public List<LedgerStatementItem> GetAccountStatement(int accountId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var statement = new List<LedgerStatementItem>();
        var acc = GetById(accountId);
        if (acc == null) return statement;

        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        // 1. Calculate Opening Balance before fromDate
        double initialOpeningSigned = (acc.OpeningBalanceType == "Cr" || acc.OpeningBalanceType == "-") ? -acc.OpeningBalance : acc.OpeningBalance;
        double priorDebits = 0.0;
        double priorCredits = 0.0;

        if (fromDate.HasValue)
        {
            using var priorCmd = new OleDbCommand(@"
                SELECT SUM(Debit) AS PriorDebit, SUM(Credit) AS PriorCredit
                FROM AccountLedger
                WHERE (AccountId = ? OR AccountName = ?) AND [Timestamp] < ?", conn);
            priorCmd.Parameters.Add("?", OleDbType.Integer).Value = acc.Id;
            priorCmd.Parameters.Add("?", OleDbType.VarWChar).Value = acc.AccountName;
            priorCmd.Parameters.Add("?", OleDbType.Date).Value = fromDate.Value.Date;

            using var priorReader = priorCmd.ExecuteReader();
            if (priorReader.Read())
            {
                priorDebits = priorReader["PriorDebit"] != DBNull.Value ? Convert.ToDouble(priorReader["PriorDebit"]) : 0.0;
                priorCredits = priorReader["PriorCredit"] != DBNull.Value ? Convert.ToDouble(priorReader["PriorCredit"]) : 0.0;
            }
        }

        double openingBalanceAtStart = initialOpeningSigned + priorDebits - priorCredits;

        // Row 0: Opening Balance Row
        statement.Add(new LedgerStatementItem
        {
            Date = fromDate?.Date ?? (acc.CreatedAt != DateTime.MinValue ? acc.CreatedAt.Date : DateTime.Today),
            TransactionType = "OPENING",
            RefNo = "-",
            Particulars = "Opening Balance b/f",
            Debit = openingBalanceAtStart >= 0 ? openingBalanceAtStart : 0.0,
            Credit = openingBalanceAtStart < 0 ? Math.Abs(openingBalanceAtStart) : 0.0,
            RunningBalance = openingBalanceAtStart,
            RunningBalanceType = openingBalanceAtStart >= 0 ? "+" : "-",
            Narration = "Starting balance brought forward"
        });

        // 2. Fetch Transactions within Date Range
        string sql = "SELECT Id, TransactionType, RefInvoiceNo, Debit, Credit, Remarks, [Timestamp] FROM AccountLedger WHERE (AccountId = ? OR AccountName = ?)";
        if (fromDate.HasValue) sql += " AND [Timestamp] >= ?";
        if (toDate.HasValue) sql += " AND [Timestamp] <= ?";
        sql += " ORDER BY [Timestamp] ASC, Id ASC";

        using var cmd = new OleDbCommand(sql, conn);
        cmd.Parameters.Add("?", OleDbType.Integer).Value = acc.Id;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = acc.AccountName;
        if (fromDate.HasValue) cmd.Parameters.Add("?", OleDbType.Date).Value = fromDate.Value.Date;
        if (toDate.HasValue) cmd.Parameters.Add("?", OleDbType.Date).Value = toDate.Value.Date.AddDays(1).AddTicks(-1);

        double currentRunning = openingBalanceAtStart;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            string txType = reader["TransactionType"]?.ToString() ?? "TX";
            string refNo = reader["RefInvoiceNo"]?.ToString() ?? string.Empty;
            double dr = reader["Debit"] != DBNull.Value ? Convert.ToDouble(reader["Debit"]) : 0.0;
            double cr = reader["Credit"] != DBNull.Value ? Convert.ToDouble(reader["Credit"]) : 0.0;
            string? remarks = reader["Remarks"]?.ToString();
            DateTime dt = reader["Timestamp"] != DBNull.Value ? Convert.ToDateTime(reader["Timestamp"]) : DateTime.Now;

            currentRunning += (dr - cr);

            string particular = txType switch
            {
                "SALE" => $"To Sales Bill #{refNo}",
                "PURCHASE" => $"By Purchase Inward #{refNo}",
                "RECEIPT" => dr > 0 ? $"To Receipt #{refNo}" : $"By Receipt #{refNo}",
                "PAYMENT" => dr > 0 ? $"To Payment #{refNo}" : $"By Payment #{refNo}",
                "JOURNAL" => dr > 0 ? $"To Journal #{refNo}" : $"By Journal #{refNo}",
                _ => $"{txType} #{refNo}"
            };

            statement.Add(new LedgerStatementItem
            {
                Date = dt,
                TransactionType = txType,
                RefNo = refNo,
                Particulars = particular,
                Debit = dr,
                Credit = cr,
                RunningBalance = currentRunning,
                RunningBalanceType = currentRunning >= 0 ? "+" : "-",
                Narration = remarks
            });
        }

        return statement;
    }

    public List<Account> GetGroupSummaryReport(string? groupType = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var all = GetAll();
        if (!string.IsNullOrWhiteSpace(groupType) && !groupType.Equals("All", StringComparison.OrdinalIgnoreCase) && !groupType.Equals("All Groups", StringComparison.OrdinalIgnoreCase))
        {
            if (groupType.Equals("Cash & Bank", StringComparison.OrdinalIgnoreCase))
            {
                all = all.Where(a => a.AccountType.Equals("Cash", StringComparison.OrdinalIgnoreCase) || a.AccountType.Equals("Bank", StringComparison.OrdinalIgnoreCase)).ToList();
            }
            else
            {
                all = all.Where(a => a.AccountType.Equals(groupType, StringComparison.OrdinalIgnoreCase)).ToList();
            }
        }

        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        foreach (var acc in all)
        {
            double initialOpeningSigned = (acc.OpeningBalanceType == "Cr" || acc.OpeningBalanceType == "-") ? -acc.OpeningBalance : acc.OpeningBalance;
            double priorDebits = 0.0;
            double priorCredits = 0.0;

            if (fromDate.HasValue)
            {
                using var priorCmd = new OleDbCommand(@"
                    SELECT SUM(Debit) AS PriorDebit, SUM(Credit) AS PriorCredit
                    FROM AccountLedger
                    WHERE (AccountId = ? OR AccountName = ?) AND [Timestamp] < ?", conn);
                priorCmd.Parameters.Add("?", OleDbType.Integer).Value = acc.Id;
                priorCmd.Parameters.Add("?", OleDbType.VarWChar).Value = acc.AccountName;
                priorCmd.Parameters.Add("?", OleDbType.Date).Value = fromDate.Value.Date;

                using var priorReader = priorCmd.ExecuteReader();
                if (priorReader.Read())
                {
                    priorDebits = priorReader["PriorDebit"] != DBNull.Value ? Convert.ToDouble(priorReader["PriorDebit"]) : 0.0;
                    priorCredits = priorReader["PriorCredit"] != DBNull.Value ? Convert.ToDouble(priorReader["PriorCredit"]) : 0.0;
                }
            }

            acc.PeriodOpeningBalance = Math.Abs(initialOpeningSigned + priorDebits - priorCredits);
            acc.PeriodOpeningBalanceType = (initialOpeningSigned + priorDebits - priorCredits) >= 0 ? "+" : "-";

            // Period Debits & Credits
            string sql = "SELECT SUM(Debit) AS PeriodDebit, SUM(Credit) AS PeriodCredit FROM AccountLedger WHERE (AccountId = ? OR AccountName = ?)";
            if (fromDate.HasValue) sql += " AND [Timestamp] >= ?";
            if (toDate.HasValue) sql += " AND [Timestamp] <= ?";

            using var pCmd = new OleDbCommand(sql, conn);
            pCmd.Parameters.Add("?", OleDbType.Integer).Value = acc.Id;
            pCmd.Parameters.Add("?", OleDbType.VarWChar).Value = acc.AccountName;
            if (fromDate.HasValue) pCmd.Parameters.Add("?", OleDbType.Date).Value = fromDate.Value.Date;
            if (toDate.HasValue) pCmd.Parameters.Add("?", OleDbType.Date).Value = toDate.Value.Date.AddDays(1).AddTicks(-1);

            using var pReader = pCmd.ExecuteReader();
            if (pReader.Read())
            {
                acc.PeriodDebit = pReader["PeriodDebit"] != DBNull.Value ? Convert.ToDouble(pReader["PeriodDebit"]) : 0.0;
                acc.PeriodCredit = pReader["PeriodCredit"] != DBNull.Value ? Convert.ToDouble(pReader["PeriodCredit"]) : 0.0;
            }

            double signedOpening = (acc.PeriodOpeningBalanceType == "Cr" || acc.PeriodOpeningBalanceType == "-") ? -acc.PeriodOpeningBalance : acc.PeriodOpeningBalance;
            acc.PeriodClosingBalance = signedOpening + acc.PeriodDebit - acc.PeriodCredit;
        }

        return all;
    }

    public int Add(Account account)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        using var cmd = new OleDbCommand("INSERT INTO Accounts (AccountType, AccountName, Address, Mobile, Email, GSTIN, OpeningBalance, OpeningBalanceType, [Description], CreatedAt) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)", conn);
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = account.AccountType ?? "Customer";
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = account.AccountName;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)account.Address ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)account.Mobile ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)account.Email ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)account.GSTIN ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.Double).Value = account.OpeningBalance;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = account.OpeningBalanceType ?? "+";
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)account.Description ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.Date).Value = account.CreatedAt == DateTime.MinValue ? DateTime.Now : account.CreatedAt;
        cmd.ExecuteNonQuery();

        using var idCmd = new OleDbCommand("SELECT @@IDENTITY", conn);
        return Convert.ToInt32(idCmd.ExecuteScalar());
    }

    public void Update(Account account)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        using var cmd = new OleDbCommand("UPDATE Accounts SET AccountType = ?, AccountName = ?, Address = ?, Mobile = ?, Email = ?, GSTIN = ?, OpeningBalance = ?, OpeningBalanceType = ?, [Description] = ? WHERE Id = ?", conn);
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = account.AccountType ?? "Customer";
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = account.AccountName;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)account.Address ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)account.Mobile ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)account.Email ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)account.GSTIN ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.Double).Value = account.OpeningBalance;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = account.OpeningBalanceType ?? "+";
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)account.Description ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.Integer).Value = account.Id;
        cmd.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        using var cmd = new OleDbCommand("DELETE FROM Accounts WHERE Id = ?", conn);
        cmd.Parameters.Add("?", OleDbType.Integer).Value = id;
        cmd.ExecuteNonQuery();
    }
}
