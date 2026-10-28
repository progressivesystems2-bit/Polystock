using System;
using System.Data.OleDb;
using System.IO;

namespace PolyStock.Data;

public static class AccessConnectionProvider
{
    private static string? _cachedWorkingProvider;
    private static readonly object _lock = new();

    public static string[] CandidateProviders => new[]
    {
        "Microsoft.ACE.OLEDB.16.0",
        "Microsoft.ACE.OLEDB.12.0",
        "Microsoft.Jet.OLEDB.4.0"
    };

    public static string DefaultDatabasePath
    {
        get
        {
            // 1. Dev environment: Check if solution/repo root Data directory exists with database
            var repoDataDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "Data"));
            var repoDb = Path.Combine(repoDataDir, "PolyStock.accdb");
            if (Directory.Exists(repoDataDir) && File.Exists(repoDb))
            {
                return repoDb;
            }

            var localRepoData = @"E:\repos\PolyStock\Data";
            var localRepoDb = Path.Combine(localRepoData, "PolyStock.accdb");
            if (Directory.Exists(localRepoData) && File.Exists(localRepoDb))
            {
                return localRepoDb;
            }

            // 2. Production / Installed environment:
            // Store database in %LocalAppData%\PolyStock\Data\PolyStock.accdb so updates do NOT overwrite user data
            var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PolyStock", "Data");
            var appDataDb = Path.Combine(appDataDir, "PolyStock.accdb");
            if (!Directory.Exists(appDataDir))
            {
                Directory.CreateDirectory(appDataDir);
            }

            if (!File.Exists(appDataDb))
            {
                var bundledDb = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "PolyStock.accdb");
                if (File.Exists(bundledDb))
                {
                    File.Copy(bundledDb, appDataDb, overwrite: false);
                }
            }

            return appDataDb;
        }
    }

    public static string GetConnectionString(string? databasePath = null)
    {
        databasePath ??= DefaultDatabasePath;
        if (string.IsNullOrWhiteSpace(databasePath)) return string.Empty;

        lock (_lock)
        {
            if (_cachedWorkingProvider != null)
            {
                return $"Provider={_cachedWorkingProvider};Data Source={databasePath};Persist Security Info=False;";
            }

            foreach (var provider in CandidateProviders)
            {
                var connStr = $"Provider={provider};Data Source={databasePath};Persist Security Info=False;";
                try
                {
                    using var conn = new OleDbConnection(connStr);
                    conn.Open();
                    _cachedWorkingProvider = provider;
                    return connStr;
                }
                catch
                {
                    // Try next provider
                }
            }

            // Default fallback
            return $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};Persist Security Info=False;";
        }
    }

    public static OleDbConnection CreateConnection(string? databasePath = null)
    {
        return new OleDbConnection(GetConnectionString(databasePath));
    }
}
