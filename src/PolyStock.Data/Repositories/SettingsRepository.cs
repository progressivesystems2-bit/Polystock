using System;
using System.Data.OleDb;
using System.Globalization;
using PolyStock.Core.Models;

namespace PolyStock.Data.Repositories;

public class SettingsRepository
{
    private readonly string? _dbPath;

    public const string KeySalesDeductionThresholdKg = "SalesDeductionThresholdKg";

    public SettingsRepository(string? dbPath = null)
    {
        _dbPath = dbPath;
    }

    public string? GetSetting(string key, string? defaultValue = null, OleDbConnection? conn = null, OleDbTransaction? tx = null)
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
            using var cmd = new OleDbCommand("SELECT SettingValue FROM Settings WHERE SettingKey = ?", conn, tx);
            cmd.Parameters.Add("?", OleDbType.VarWChar).Value = key;
            var val = cmd.ExecuteScalar();
            if (val != null && val != DBNull.Value)
            {
                return val.ToString();
            }
            return defaultValue;
        }
        finally
        {
            if (shouldClose) conn.Dispose();
        }
    }

    public void SetSetting(string key, string value, string? description = null, OleDbConnection? conn = null, OleDbTransaction? tx = null)
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
            bool exists = false;
            using (var checkCmd = new OleDbCommand("SELECT COUNT(*) FROM Settings WHERE SettingKey = ?", conn, tx))
            {
                checkCmd.Parameters.Add("?", OleDbType.VarWChar).Value = key;
                exists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;
            }

            if (exists)
            {
                using var updateCmd = new OleDbCommand(@"
                    UPDATE Settings 
                    SET SettingValue = ?, [Description] = ?, LastUpdated = ? 
                    WHERE SettingKey = ?", conn, tx);
                updateCmd.Parameters.Add("?", OleDbType.VarWChar).Value = value;
                updateCmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)description ?? DBNull.Value;
                updateCmd.Parameters.Add("?", OleDbType.Date).Value = DateTime.Now;
                updateCmd.Parameters.Add("?", OleDbType.VarWChar).Value = key;
                updateCmd.ExecuteNonQuery();
            }
            else
            {
                using var insertCmd = new OleDbCommand(@"
                    INSERT INTO Settings (SettingKey, SettingValue, [Description], LastUpdated) 
                    VALUES (?, ?, ?, ?)", conn, tx);
                insertCmd.Parameters.Add("?", OleDbType.VarWChar).Value = key;
                insertCmd.Parameters.Add("?", OleDbType.VarWChar).Value = value;
                insertCmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)description ?? DBNull.Value;
                insertCmd.Parameters.Add("?", OleDbType.Date).Value = DateTime.Now;
                insertCmd.ExecuteNonQuery();
            }
        }
        finally
        {
            if (shouldClose) conn.Dispose();
        }
    }

    public double GetSalesDeductionThresholdKg(OleDbConnection? conn = null, OleDbTransaction? tx = null)
    {
        var raw = GetSetting(KeySalesDeductionThresholdKg, "0", conn, tx);
        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
        {
            return d;
        }
        if (double.TryParse(raw, out var dLocal))
        {
            return dLocal;
        }
        return 0.0;
    }

    public void SetSalesDeductionThresholdKg(double thresholdKg, OleDbConnection? conn = null, OleDbTransaction? tx = null)
    {
        SetSetting(
            KeySalesDeductionThresholdKg,
            thresholdKg.ToString("0.###", CultureInfo.InvariantCulture),
            "Minimum sales line item weight (KG) for inventory deduction. Below this threshold, stock is not subtracted.",
            conn,
            tx);
    }

    public AppSettings GetAppSettings()
    {
        return new AppSettings
        {
            SalesDeductionThresholdKg = GetSalesDeductionThresholdKg()
        };
    }

    public void SaveAppSettings(AppSettings settings)
    {
        SetSalesDeductionThresholdKg(settings.SalesDeductionThresholdKg);
    }
}
