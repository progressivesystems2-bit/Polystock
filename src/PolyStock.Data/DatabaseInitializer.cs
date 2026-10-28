using System;
using System.Data.OleDb;
using System.IO;
using PolyStock.Core.Common;

namespace PolyStock.Data;

public static class DatabaseInitializer
{
    public static void Initialize(string? dbPath = null)
    {
        dbPath ??= AccessConnectionProvider.DefaultDatabasePath;
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        bool isNewFile = !File.Exists(dbPath);
        if (isNewFile)
        {
            CreateDatabaseFile(dbPath);
        }

        CreateTables(dbPath);

        if (isNewFile)
        {
            SeedInitialData(dbPath);
        }
    }

    private static void CreateDatabaseFile(string dbPath)
    {
        // 1. Try DAO.DBEngine.120 (standard for ACE / Access 2007-2021/365)
        string[] daoProgIds = { "DAO.DBEngine.120", "DAO.DBEngine.36", "Access.Application" };
        foreach (var progId in daoProgIds)
        {
            try
            {
                var type = Type.GetTypeFromProgID(progId);
                if (type != null)
                {
                    dynamic? engine = Activator.CreateInstance(type);
                    if (engine != null)
                    {
                        if (progId == "Access.Application")
                        {
                            engine.NewCurrentDatabase(dbPath);
                            engine.CloseCurrentDatabase();
                            return;
                        }
                        else
                        {
                            var db = engine.CreateDatabase(dbPath, ";LANGID=0x0409;CP=1252;COUNTRY=0");
                            db.Close();
                            return;
                        }
                    }
                }
            }
            catch
            {
                // Try next
            }
        }

        // 2. Try ADOX.Catalog
        string[] providers = { "Microsoft.ACE.OLEDB.16.0", "Microsoft.ACE.OLEDB.12.0", "Microsoft.Jet.OLEDB.4.0" };
        foreach (var provider in providers)
        {
            try
            {
                var adoxType = Type.GetTypeFromProgID("ADOX.Catalog");
                if (adoxType != null)
                {
                    object? catalog = Activator.CreateInstance(adoxType);
                    if (catalog != null)
                    {
                        var connStr = $"Provider={provider};Data Source={dbPath};";
                        adoxType.GetMethod("Create")?.Invoke(catalog, new object[] { connStr });
                        return;
                    }
                }
            }
            catch
            {
                // Try next provider
            }
        }

        // 3. Fallback: Copy from existing template if available
        string fallbackSource = @"E:\repos\ConceptJewelleryPro\RGRG2526.mdb";
        if (File.Exists(fallbackSource))
        {
            File.Copy(fallbackSource, dbPath, true);
        }
    }

    private static void CreateTables(string dbPath)
    {
        using var conn = AccessConnectionProvider.CreateConnection(dbPath);
        conn.Open();

        ExecuteIgnoreError(conn, @"
            CREATE TABLE HeadGroups (
                Id AUTOINCREMENT PRIMARY KEY,
                [Name] TEXT(100) NOT NULL,
                [Description] TEXT(255),
                CreatedAt DATETIME
            )");

        ExecuteIgnoreError(conn, @"
            CREATE TABLE Brands (
                Id AUTOINCREMENT PRIMARY KEY,
                [Name] TEXT(100) NOT NULL,
                [Description] TEXT(255),
                CreatedAt DATETIME
            )");

        ExecuteIgnoreError(conn, @"
            CREATE TABLE Items (
                Id AUTOINCREMENT PRIMARY KEY,
                HeadGroupId INT,
                BrandId INT,
                ItemCode TEXT(50),
                ItemName TEXT(200) NOT NULL,
                DefaultRatePerKg DOUBLE,
                [Description] TEXT(255),
                CreatedAt DATETIME
            )");

        ExecuteIgnoreError(conn, @"
            CREATE TABLE ItemPackSizes (
                Id AUTOINCREMENT PRIMARY KEY,
                ItemId INT NOT NULL,
                PackSizeKg DOUBLE NOT NULL,
                IsActive YESNO
            )");

        ExecuteIgnoreError(conn, @"
            CREATE TABLE Stock (
                Id AUTOINCREMENT PRIMARY KEY,
                ItemId INT NOT NULL,
                PackSizeKg DOUBLE NOT NULL,
                FullBags INT NOT NULL,
                LooseKg DOUBLE NOT NULL,
                TotalKg DOUBLE NOT NULL,
                LastUpdated DATETIME
            )");

        ExecuteIgnoreError(conn, @"
            CREATE TABLE Purchases (
                Id AUTOINCREMENT PRIMARY KEY,
                InvoiceNo TEXT(50) NOT NULL,
                SupplierName TEXT(150),
                GodownId INT,
                GodownName TEXT(100),
                PurchaseDate DATETIME NOT NULL,
                TotalBags INT NOT NULL,
                TotalWeightKg DOUBLE NOT NULL,
                SubTotal DOUBLE,
                GstPercent DOUBLE,
                GstAmount DOUBLE,
                Freight DOUBLE,
                LoadingUnloading DOUBLE,
                GrandTotal DOUBLE,
                TotalAmount DOUBLE NOT NULL,
                Notes TEXT(255),
                CreatedAt DATETIME
            )");

        ExecuteIgnoreError(conn, @"
            CREATE TABLE PurchaseItems (
                Id AUTOINCREMENT PRIMARY KEY,
                PurchaseId INT NOT NULL,
                ItemId INT NOT NULL,
                GodownId INT,
                GodownName TEXT(100),
                PackSizeKg DOUBLE NOT NULL,
                Bags INT NOT NULL,
                TotalKg DOUBLE NOT NULL,
                RatePerKg DOUBLE NOT NULL,
                Amount DOUBLE NOT NULL,
                LandedCost DOUBLE
            )");

        ExecuteIgnoreError(conn, "ALTER TABLE Purchases ADD COLUMN GodownId INT");
        ExecuteIgnoreError(conn, "ALTER TABLE Purchases ADD COLUMN GodownName TEXT(100)");
        ExecuteIgnoreError(conn, "ALTER TABLE PurchaseItems ADD COLUMN GodownId INT");
        ExecuteIgnoreError(conn, "ALTER TABLE PurchaseItems ADD COLUMN GodownName TEXT(100)");
        ExecuteIgnoreError(conn, "ALTER TABLE PurchaseItems ADD COLUMN LandedCost DOUBLE");

        ExecuteIgnoreError(conn, @"
            CREATE TABLE Sales (
                Id AUTOINCREMENT PRIMARY KEY,
                InvoiceNo TEXT(50) NOT NULL,
                CustomerName TEXT(150),
                GodownId INT,
                GodownName TEXT(100),
                SaleDate DATETIME NOT NULL,
                TotalBags INT NOT NULL,
                TotalLooseKg DOUBLE NOT NULL,
                TotalWeightKg DOUBLE NOT NULL,
                SubTotal DOUBLE,
                GstPercent DOUBLE,
                GstAmount DOUBLE,
                Freight DOUBLE,
                LoadingUnloading DOUBLE,
                GrandTotal DOUBLE,
                TotalAmount DOUBLE NOT NULL,
                Notes TEXT(255),
                PaymentMode TEXT(50),
                BankAccountId INT,
                BankAccountName TEXT(150),
                UtrRefNo TEXT(100),
                CashReceived DOUBLE,
                UpiReceived DOUBLE,
                CreditAmount DOUBLE,
                CreatedAt DATETIME
            )");

        ExecuteIgnoreError(conn, @"
            CREATE TABLE SaleItems (
                Id AUTOINCREMENT PRIMARY KEY,
                SaleId INT NOT NULL,
                ItemId INT NOT NULL,
                GodownId INT,
                GodownName TEXT(100),
                PackSizeKg DOUBLE NOT NULL,
                BagsSold INT NOT NULL,
                LooseKgSold DOUBLE NOT NULL,
                TotalKgSold DOUBLE NOT NULL,
                RatePerKg DOUBLE NOT NULL,
                Amount DOUBLE NOT NULL,
                BagsOpened INT,
                SkipStockDeduction YESNO
            )");

        // Column migrations for existing databases
        ExecuteIgnoreError(conn, "ALTER TABLE SaleItems ADD COLUMN SkipStockDeduction YESNO");
        ExecuteIgnoreError(conn, "ALTER TABLE Sales ADD COLUMN GodownId INT");
        ExecuteIgnoreError(conn, "ALTER TABLE Sales ADD COLUMN GodownName TEXT(100)");
        ExecuteIgnoreError(conn, "ALTER TABLE Sales ADD COLUMN SubTotal DOUBLE");
        ExecuteIgnoreError(conn, "ALTER TABLE Sales ADD COLUMN GstPercent DOUBLE");
        ExecuteIgnoreError(conn, "ALTER TABLE Sales ADD COLUMN GstAmount DOUBLE");
        ExecuteIgnoreError(conn, "ALTER TABLE Sales ADD COLUMN Freight DOUBLE");
        ExecuteIgnoreError(conn, "ALTER TABLE Sales ADD COLUMN LoadingUnloading DOUBLE");
        ExecuteIgnoreError(conn, "ALTER TABLE Sales ADD COLUMN GrandTotal DOUBLE");
        ExecuteIgnoreError(conn, "ALTER TABLE Sales ADD COLUMN PaymentMode TEXT(50)");
        ExecuteIgnoreError(conn, "ALTER TABLE Sales ADD COLUMN BankAccountId INT");
        ExecuteIgnoreError(conn, "ALTER TABLE Sales ADD COLUMN BankAccountName TEXT(150)");
        ExecuteIgnoreError(conn, "ALTER TABLE Sales ADD COLUMN UtrRefNo TEXT(100)");
        ExecuteIgnoreError(conn, "ALTER TABLE Sales ADD COLUMN CashReceived DOUBLE");
        ExecuteIgnoreError(conn, "ALTER TABLE Sales ADD COLUMN UpiReceived DOUBLE");
        ExecuteIgnoreError(conn, "ALTER TABLE Sales ADD COLUMN CreditAmount DOUBLE");

        ExecuteIgnoreError(conn, "ALTER TABLE Purchases ADD COLUMN SubTotal DOUBLE");
        ExecuteIgnoreError(conn, "ALTER TABLE Purchases ADD COLUMN GstPercent DOUBLE");
        ExecuteIgnoreError(conn, "ALTER TABLE Purchases ADD COLUMN GstAmount DOUBLE");
        ExecuteIgnoreError(conn, "ALTER TABLE Purchases ADD COLUMN Freight DOUBLE");
        ExecuteIgnoreError(conn, "ALTER TABLE Purchases ADD COLUMN LoadingUnloading DOUBLE");
        ExecuteIgnoreError(conn, "ALTER TABLE Purchases ADD COLUMN GrandTotal DOUBLE");

        ExecuteIgnoreError(conn, "ALTER TABLE SaleItems ADD COLUMN GodownId INT");
        ExecuteIgnoreError(conn, "ALTER TABLE SaleItems ADD COLUMN GodownName TEXT(100)");

        ExecuteIgnoreError(conn, "ALTER TABLE Stock ADD COLUMN GodownId INT");
        ExecuteIgnoreError(conn, "ALTER TABLE Stock ADD COLUMN GodownName TEXT(100)");

        ExecuteIgnoreError(conn, @"
            CREATE TABLE StockLedger (
                Id AUTOINCREMENT PRIMARY KEY,
                ItemId INT NOT NULL,
                GodownId INT,
                GodownName TEXT(100),
                PackSizeKg DOUBLE NOT NULL,
                TransactionType TEXT(50) NOT NULL,
                RefInvoiceNo TEXT(50),
                FullBagsChange INT NOT NULL,
                LooseKgChange DOUBLE NOT NULL,
                TotalKgChange DOUBLE,
                BalanceFullBags INT NOT NULL,
                BalanceLooseKg DOUBLE NOT NULL,
                BalanceTotalKg DOUBLE NOT NULL,
                Remarks TEXT(255),
                [Timestamp] DATETIME NOT NULL
            )");

        ExecuteIgnoreError(conn, "ALTER TABLE StockLedger ADD COLUMN GodownId INT");
        ExecuteIgnoreError(conn, "ALTER TABLE StockLedger ADD COLUMN GodownName TEXT(100)");
        ExecuteIgnoreError(conn, "ALTER TABLE StockLedger ADD COLUMN TotalKgChange DOUBLE");

        ExecuteIgnoreError(conn, @"
            CREATE TABLE AccountLedger (
                Id AUTOINCREMENT PRIMARY KEY,
                AccountId INT,
                AccountName TEXT(150) NOT NULL,
                TransactionType TEXT(50) NOT NULL,
                RefInvoiceNo TEXT(50),
                Debit DOUBLE NOT NULL,
                Credit DOUBLE NOT NULL,
                Balance DOUBLE NOT NULL,
                Remarks TEXT(255),
                [Timestamp] DATETIME NOT NULL
            )");

        ExecuteIgnoreError(conn, @"
            CREATE TABLE Godowns (
                Id AUTOINCREMENT PRIMARY KEY,
                [Code] TEXT(50),
                [Name] TEXT(150) NOT NULL,
                [Location] TEXT(255),
                [ContactPerson] TEXT(100),
                [Phone] TEXT(50),
                [Description] TEXT(255),
                [IsActive] YESNO,
                CreatedAt DATETIME
            )");

        ExecuteIgnoreError(conn, @"
            CREATE TABLE Accounts (
                Id AUTOINCREMENT PRIMARY KEY,
                AccountType TEXT(50) NOT NULL,
                AccountName TEXT(150) NOT NULL,
                Address TEXT(255),
                Mobile TEXT(50),
                Email TEXT(100),
                GSTIN TEXT(50),
                OpeningBalance DOUBLE,
                OpeningBalanceType TEXT(10),
                [Description] TEXT(255),
                CreatedAt DATETIME
            )");

        ExecuteIgnoreError(conn, "ALTER TABLE Accounts ADD COLUMN OpeningBalanceType TEXT(10)");

        ExecuteIgnoreError(conn, @"
            CREATE TABLE Vouchers (
                Id AUTOINCREMENT PRIMARY KEY,
                VoucherType TEXT(50) NOT NULL,
                VoucherNo TEXT(50) NOT NULL,
                VoucherDate DATETIME NOT NULL,
                PrimaryAccountId INT,
                PrimaryAccountName TEXT(150),
                TotalAmount DOUBLE,
                DebitAccountId INT,
                DebitAccountName TEXT(150),
                CreditAccountId INT,
                CreditAccountName TEXT(150),
                Amount DOUBLE NOT NULL,
                PaymentMode TEXT(50),
                ReferenceNo TEXT(100),
                ReferenceDate DATETIME,
                Narration TEXT(255),
                CreatedAt DATETIME
            )");

        ExecuteIgnoreError(conn, "ALTER TABLE Vouchers ADD COLUMN PrimaryAccountId INT");
        ExecuteIgnoreError(conn, "ALTER TABLE Vouchers ADD COLUMN PrimaryAccountName TEXT(150)");
        ExecuteIgnoreError(conn, "ALTER TABLE Vouchers ADD COLUMN TotalAmount DOUBLE");

        ExecuteIgnoreError(conn, @"
            CREATE TABLE VoucherItems (
                Id AUTOINCREMENT PRIMARY KEY,
                VoucherId INT NOT NULL,
                AccountId INT,
                AccountName TEXT(150) NOT NULL,
                Amount DOUBLE NOT NULL,
                ReferenceNo TEXT(100),
                Remarks TEXT(255)
            )");

        ExecuteIgnoreError(conn, @"
            CREATE TABLE StockTransfers (
                Id AUTOINCREMENT PRIMARY KEY,
                TransferNo TEXT(50) NOT NULL,
                TransferDate DATETIME NOT NULL,
                FromGodownId INT NOT NULL,
                FromGodownName TEXT(100),
                ToGodownId INT NOT NULL,
                ToGodownName TEXT(100),
                TotalBags INT NOT NULL,
                TotalLooseKg DOUBLE,
                TotalWeightKg DOUBLE NOT NULL,
                TotalAmount DOUBLE,
                Notes TEXT(255),
                CreatedAt DATETIME
            )");

        ExecuteIgnoreError(conn, @"
            CREATE TABLE StockTransferItems (
                Id AUTOINCREMENT PRIMARY KEY,
                TransferId INT NOT NULL,
                ItemId INT NOT NULL,
                PackSizeKg DOUBLE NOT NULL,
                Bags INT NOT NULL,
                LooseKg DOUBLE,
                TotalKg DOUBLE NOT NULL,
                RatePerKg DOUBLE,
                Amount DOUBLE,
                Remarks TEXT(255)
            )");

        // Security Tables
        ExecuteIgnoreError(conn, @"
            CREATE TABLE Roles (
                Id AUTOINCREMENT PRIMARY KEY,
                RoleName TEXT(50) NOT NULL,
                [Description] TEXT(255),
                IsSystemRole YESNO,
                CreatedAt DATETIME
            )");

        ExecuteIgnoreError(conn, @"
            CREATE TABLE RolePermissions (
                Id AUTOINCREMENT PRIMARY KEY,
                RoleId INT NOT NULL,
                ModuleKey TEXT(50) NOT NULL,
                CanView YESNO,
                CanCreate YESNO,
                CanEdit YESNO,
                CanDelete YESNO,
                CanPrint YESNO
            )");

        ExecuteIgnoreError(conn, @"
            CREATE TABLE Users (
                Id AUTOINCREMENT PRIMARY KEY,
                Username TEXT(50) NOT NULL,
                DisplayName TEXT(100) NOT NULL,
                PasswordHash TEXT(255) NOT NULL,
                Salt TEXT(255) NOT NULL,
                RoleId INT NOT NULL,
                RoleName TEXT(50),
                IsActive YESNO,
                CreatedAt DATETIME,
                LastLoginAt DATETIME
            )");

        // Column migrations for security tables
        ExecuteIgnoreError(conn, "ALTER TABLE Users ADD COLUMN RoleName TEXT(50)");
        ExecuteIgnoreError(conn, "ALTER TABLE Users ADD COLUMN IsActive YESNO");
        ExecuteIgnoreError(conn, "ALTER TABLE Users ADD COLUMN LastLoginAt DATETIME");
        ExecuteIgnoreError(conn, "ALTER TABLE RolePermissions ADD COLUMN CanPrint YESNO");

        // Settings Table
        ExecuteIgnoreError(conn, @"
            CREATE TABLE Settings (
                SettingKey TEXT(100) PRIMARY KEY,
                SettingValue TEXT(255),
                [Description] TEXT(255),
                LastUpdated DATETIME
            )");

        EnsureStandardAccountsExist(conn);
        EnsureSecurityTablesAndDefaultAdmin(conn);
    }

    private static void EnsureStandardAccountsExist(OleDbConnection conn)
    {
        EnsureAccount(conn, "Cash in Hand", "Cash", "Primary Cash in Hand Account");
        EnsureAccount(conn, "Bank Account", "Bank", "Primary Commercial Bank Account");
        EnsureAccount(conn, "Discount / Round Off", "General", "Discount and Round-off Adjustments");
    }

    private static void EnsureAccount(OleDbConnection conn, string name, string type, string desc)
    {
        try
        {
            using var checkCmd = new OleDbCommand("SELECT COUNT(*) FROM Accounts WHERE AccountName = ?", conn);
            checkCmd.Parameters.Add("?", OleDbType.VarWChar).Value = name;
            var exists = Convert.ToInt32(checkCmd.ExecuteScalar() ?? 0) > 0;
            if (!exists)
            {
                using var insCmd = new OleDbCommand("INSERT INTO Accounts (AccountType, AccountName, OpeningBalance, OpeningBalanceType, [Description], CreatedAt) VALUES (?, ?, 0, 'Dr', ?, ?)", conn);
                insCmd.Parameters.Add("?", OleDbType.VarWChar).Value = type;
                insCmd.Parameters.Add("?", OleDbType.VarWChar).Value = name;
                insCmd.Parameters.Add("?", OleDbType.VarWChar).Value = desc;
                insCmd.Parameters.Add("?", OleDbType.Date).Value = DateTime.Now;
                insCmd.ExecuteNonQuery();
            }
        }
        catch
        {
        }
    }

    private static void EnsureSecurityTablesAndDefaultAdmin(OleDbConnection conn)
    {
        try
        {
            // 1. Ensure Roles exist
            int adminRoleId = EnsureRole(conn, "Administrator", "Full access to all masters, entries, reports, and security settings", true);
            int managerRoleId = EnsureRole(conn, "Manager", "Full access to masters, entries, and reports; cannot manage security", false);
            int operatorRoleId = EnsureRole(conn, "Operator", "Allowed billing, inwards, transfers, vouchers and viewing registers; cannot edit masters or security", false);
            int auditorRoleId = EnsureRole(conn, "Auditor / Viewer", "Read-only access to registers, ledgers, and masters", false);

            // 2. Ensure Permissions for each default role if they have no permissions configured yet
            EnsureRolePermissions(conn, adminRoleId, "Administrator");
            EnsureRolePermissions(conn, managerRoleId, "Manager");
            EnsureRolePermissions(conn, operatorRoleId, "Operator");
            EnsureRolePermissions(conn, auditorRoleId, "Auditor / Viewer");

            // 3. Ensure Default Admin user exists
            using var checkUsersCmd = new OleDbCommand("SELECT COUNT(*) FROM Users", conn);
            var userCount = Convert.ToInt32(checkUsersCmd.ExecuteScalar() ?? 0);
            if (userCount == 0)
            {
                var (hash, salt) = PasswordSecurity.HashPassword("admin");
                using var insUserCmd = new OleDbCommand(@"
                    INSERT INTO Users (Username, DisplayName, PasswordHash, Salt, RoleId, RoleName, IsActive, CreatedAt)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?)", conn);
                insUserCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "admin";
                insUserCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "System Administrator";
                insUserCmd.Parameters.Add("?", OleDbType.VarWChar).Value = hash;
                insUserCmd.Parameters.Add("?", OleDbType.VarWChar).Value = salt;
                insUserCmd.Parameters.Add("?", OleDbType.Integer).Value = adminRoleId;
                insUserCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "Administrator";
                insUserCmd.Parameters.Add("?", OleDbType.Boolean).Value = true;
                insUserCmd.Parameters.Add("?", OleDbType.Date).Value = DateTime.Now;
                insUserCmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Security initialization error: {ex.Message}");
        }
    }

    private static int EnsureRole(OleDbConnection conn, string roleName, string description, bool isSystem)
    {
        using var checkCmd = new OleDbCommand("SELECT Id FROM Roles WHERE RoleName = ?", conn);
        checkCmd.Parameters.Add("?", OleDbType.VarWChar).Value = roleName;
        var existingId = checkCmd.ExecuteScalar();
        if (existingId != null && existingId != DBNull.Value)
        {
            return Convert.ToInt32(existingId);
        }

        using var insCmd = new OleDbCommand("INSERT INTO Roles (RoleName, [Description], IsSystemRole, CreatedAt) VALUES (?, ?, ?, ?)", conn);
        insCmd.Parameters.Add("?", OleDbType.VarWChar).Value = roleName;
        insCmd.Parameters.Add("?", OleDbType.VarWChar).Value = description;
        insCmd.Parameters.Add("?", OleDbType.Boolean).Value = isSystem;
        insCmd.Parameters.Add("?", OleDbType.Date).Value = DateTime.Now;
        insCmd.ExecuteNonQuery();

        using var idCmd = new OleDbCommand("SELECT @@IDENTITY", conn);
        return Convert.ToInt32(idCmd.ExecuteScalar());
    }

    private static void EnsureRolePermissions(OleDbConnection conn, int roleId, string roleType)
    {
        foreach (var module in PermissionModules.AllModules)
        {
            using var checkPermCmd = new OleDbCommand("SELECT COUNT(*) FROM RolePermissions WHERE RoleId = ? AND ModuleKey = ?", conn);
            checkPermCmd.Parameters.Add("?", OleDbType.Integer).Value = roleId;
            checkPermCmd.Parameters.Add("?", OleDbType.VarWChar).Value = module.Key;
            var exists = Convert.ToInt32(checkPermCmd.ExecuteScalar() ?? 0) > 0;
            if (exists) continue; // Already configured for this module

            bool canView = false;
            bool canCreate = false;
            bool canEdit = false;
            bool canDelete = false;
            bool canPrint = false;

            if (roleType == "Administrator")
            {
                canView = canCreate = canEdit = canDelete = canPrint = true;
            }
            else if (roleType == "Manager")
            {
                if (module.Key != PermissionModules.UserSecurity)
                {
                    canView = canCreate = canEdit = canDelete = canPrint = true;
                }
            }
            else if (roleType == "Operator")
            {
                if (module.Category == PermissionModules.CategoryMasters)
                {
                    canView = true;
                }
                else if (module.Category == PermissionModules.CategoryEntries)
                {
                    canView = true;
                    canCreate = true;
                    canPrint = true;
                }
                else if (module.Category == PermissionModules.CategoryReports && module.Key != PermissionModules.ProfitLossReport)
                {
                    // Operator gets general registers, but confidential Profit & Loss is restricted by default
                    canView = true;
                    canPrint = true;
                }
            }
            else if (roleType == "Auditor / Viewer")
            {
                if (module.Key != PermissionModules.UserSecurity)
                {
                    canView = true;
                    if (module.Category == PermissionModules.CategoryReports)
                    {
                        canPrint = true;
                    }
                }
            }

            using var insPermCmd = new OleDbCommand(@"
                INSERT INTO RolePermissions (RoleId, ModuleKey, CanView, CanCreate, CanEdit, CanDelete, CanPrint)
                VALUES (?, ?, ?, ?, ?, ?, ?)", conn);
            insPermCmd.Parameters.Add("?", OleDbType.Integer).Value = roleId;
            insPermCmd.Parameters.Add("?", OleDbType.VarWChar).Value = module.Key;
            insPermCmd.Parameters.Add("?", OleDbType.Boolean).Value = canView;
            insPermCmd.Parameters.Add("?", OleDbType.Boolean).Value = canCreate;
            insPermCmd.Parameters.Add("?", OleDbType.Boolean).Value = canEdit;
            insPermCmd.Parameters.Add("?", OleDbType.Boolean).Value = canDelete;
            insPermCmd.Parameters.Add("?", OleDbType.Boolean).Value = canPrint;
            insPermCmd.ExecuteNonQuery();
        }
    }

    private static void ExecuteIgnoreError(OleDbConnection conn, string sql)
    {
        try
        {
            using var cmd = new OleDbCommand(sql, conn);
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // Table already exists or command failed harmlessly
        }
    }

    private static void SeedInitialData(string dbPath)
    {
        using var conn = AccessConnectionProvider.CreateConnection(dbPath);
        conn.Open();

        ExecuteIgnoreError(conn, "CREATE TABLE AppMetadata (KeyName TEXT(50) PRIMARY KEY, KeyValue TEXT(255))");

        using (var metaCheck = new OleDbCommand("SELECT COUNT(*) FROM AppMetadata WHERE KeyName = 'InitialSeedingDone'", conn))
        {
            var alreadySeeded = Convert.ToInt32(metaCheck.ExecuteScalar() ?? 0);
            if (alreadySeeded > 0) return; // Never re-insert dummy data once initialized!
        }

        using (var markCmd = new OleDbCommand("INSERT INTO AppMetadata (KeyName, KeyValue) VALUES ('InitialSeedingDone', 'True')", conn))
        {
            markCmd.ExecuteNonQuery();
        }

        // Check if any Accounts exist
        using (var accCheck = new OleDbCommand("SELECT COUNT(*) FROM Accounts", conn))
        {
            var aCount = Convert.ToInt32(accCheck.ExecuteScalar() ?? 0);
            if (aCount == 0)
            {
                using (var aCmd = new OleDbCommand("INSERT INTO Accounts (AccountType, AccountName, Address, Mobile, Email, GSTIN, OpeningBalance, [Description], CreatedAt) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)", conn))
                {
                    aCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "Customer";
                    aCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "Sri Rama Plastics";
                    aCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "Plot 12, Industrial Area, Hyderabad";
                    aCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "+91 98480 12345";
                    aCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "srirama@example.com";
                    aCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "36ABCDE1234F1Z5";
                    aCmd.Parameters.Add("?", OleDbType.Double).Value = 0.0;
                    aCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "Primary Local Buyer";
                    aCmd.Parameters.Add("?", OleDbType.Date).Value = DateTime.Now;
                    aCmd.ExecuteNonQuery();
                }

                using (var aCmd = new OleDbCommand("INSERT INTO Accounts (AccountType, AccountName, Address, Mobile, Email, GSTIN, OpeningBalance, [Description], CreatedAt) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)", conn))
                {
                    aCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "Supplier";
                    aCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "Balaji Polymers Suppliers";
                    aCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "104 Polymer Tower, Andheri East, Mumbai";
                    aCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "+91 98200 12345";
                    aCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "balaji@example.com";
                    aCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "27XYZAB5678G2Z1";
                    aCmd.Parameters.Add("?", OleDbType.Double).Value = 0.0;
                    aCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "Main Raw Material Supplier";
                    aCmd.Parameters.Add("?", OleDbType.Date).Value = DateTime.Now;
                    aCmd.ExecuteNonQuery();
                }

                using (var aCmd = new OleDbCommand("INSERT INTO Accounts (AccountType, AccountName, Address, Mobile, Email, GSTIN, OpeningBalance, [Description], CreatedAt) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)", conn))
                {
                    aCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "Cash";
                    aCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "Cash in Hand";
                    aCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "Counter Register";
                    aCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "";
                    aCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "";
                    aCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "";
                    aCmd.Parameters.Add("?", OleDbType.Double).Value = 0.0;
                    aCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "Default Cash in Hand Drawer";
                    aCmd.Parameters.Add("?", OleDbType.Date).Value = DateTime.Now;
                    aCmd.ExecuteNonQuery();
                }

                using (var aCmd = new OleDbCommand("INSERT INTO Accounts (AccountType, AccountName, Address, Mobile, Email, GSTIN, OpeningBalance, [Description], CreatedAt) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)", conn))
                {
                    aCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "Bank";
                    aCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "Primary UPI / Bank Account";
                    aCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "Main Branch";
                    aCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "";
                    aCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "";
                    aCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "";
                    aCmd.Parameters.Add("?", OleDbType.Double).Value = 0.0;
                    aCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "Primary UPI QR & Online Collection A/c";
                    aCmd.Parameters.Add("?", OleDbType.Date).Value = DateTime.Now;
                    aCmd.ExecuteNonQuery();
                }
            }
        }

        // Check if any Godown exists
        using (var godownCheck = new OleDbCommand("SELECT COUNT(*) FROM Godowns", conn))
        {
            var gCount = Convert.ToInt32(godownCheck.ExecuteScalar() ?? 0);
            if (gCount == 0)
            {
                using var gCmd = new OleDbCommand("INSERT INTO Godowns ([Code], [Name], [Location], [ContactPerson], [Phone], [Description], [IsActive], CreatedAt) VALUES (?, ?, ?, ?, ?, ?, ?, ?)", conn);
                gCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "GW-01";
                gCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "Main Warehouse Godown";
                gCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "Plot 42, Phase II Industrial Zone";
                gCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "Ramesh Kumar";
                gCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "+91 98765 00001";
                gCmd.Parameters.Add("?", OleDbType.VarWChar).Value = "Primary central stock godown";
                gCmd.Parameters.Add("?", OleDbType.Boolean).Value = true;
                gCmd.Parameters.Add("?", OleDbType.Date).Value = DateTime.Now;
                gCmd.ExecuteNonQuery();
            }
        }

        // Check if any HeadGroup exists
        using var checkCmd = new OleDbCommand("SELECT COUNT(*) FROM HeadGroups", conn);
        var count = Convert.ToInt32(checkCmd.ExecuteScalar() ?? 0);
        if (count == 0)
        {
            var now = DateTime.Now;

            // Seed HeadGroup: CC
            using (var cmd = new OleDbCommand("INSERT INTO HeadGroups ([Name], [Description], CreatedAt) VALUES (?, ?, ?)", conn))
            {
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = "CC";
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = "Calcium Carbonate Masterbatch";
                cmd.Parameters.Add("?", OleDbType.Date).Value = now;
                cmd.ExecuteNonQuery();
            }

            // Seed Brands: Balaji, Tirupati
            using (var cmd = new OleDbCommand("INSERT INTO Brands ([Name], [Description], CreatedAt) VALUES (?, ?, ?)", conn))
            {
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = "Balaji";
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = "Balaji Polymers";
                cmd.Parameters.Add("?", OleDbType.Date).Value = now;
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new OleDbCommand("INSERT INTO Brands ([Name], [Description], CreatedAt) VALUES (?, ?, ?)", conn))
            {
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = "Tirupati";
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = "Tirupati Polymers";
                cmd.Parameters.Add("?", OleDbType.Date).Value = now;
                cmd.ExecuteNonQuery();
            }

            // Get HeadGroupId and BrandId
            int headGroupId = 1;
            int brandId = 1;
            using (var cmd = new OleDbCommand("SELECT MIN(Id) FROM HeadGroups", conn))
            {
                headGroupId = Convert.ToInt32(cmd.ExecuteScalar() ?? 1);
            }
            using (var cmd = new OleDbCommand("SELECT MIN(Id) FROM Brands", conn))
            {
                brandId = Convert.ToInt32(cmd.ExecuteScalar() ?? 1);
            }

            // Seed Item: CC Balaji 7
            int itemId = 1;
            using (var cmd = new OleDbCommand("INSERT INTO Items (HeadGroupId, BrandId, ItemCode, ItemName, DefaultRatePerKg, [Description], CreatedAt) VALUES (?, ?, ?, ?, ?, ?, ?)", conn))
            {
                cmd.Parameters.Add("?", OleDbType.Integer).Value = headGroupId;
                cmd.Parameters.Add("?", OleDbType.Integer).Value = brandId;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = "7";
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = "CC Balaji 7";
                cmd.Parameters.Add("?", OleDbType.Double).Value = 40.0;
                cmd.Parameters.Add("?", OleDbType.VarWChar).Value = "Standard 7 Grade Filler";
                cmd.Parameters.Add("?", OleDbType.Date).Value = now;
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new OleDbCommand("SELECT @@IDENTITY", conn))
            {
                itemId = Convert.ToInt32(cmd.ExecuteScalar() ?? 1);
            }

            // Seed Pack Sizes: 30kg, 25kg, 20kg
            double[] sizes = { 30.0, 25.0, 20.0 };
            foreach (var size in sizes)
            {
                using var packCmd = new OleDbCommand("INSERT INTO ItemPackSizes (ItemId, PackSizeKg, IsActive) VALUES (?, ?, ?)", conn);
                packCmd.Parameters.Add("?", OleDbType.Integer).Value = itemId;
                packCmd.Parameters.Add("?", OleDbType.Double).Value = size;
                packCmd.Parameters.Add("?", OleDbType.Boolean).Value = true;
                packCmd.ExecuteNonQuery();

                // Initialize empty stock row
                using var stockCmd = new OleDbCommand("INSERT INTO Stock (ItemId, PackSizeKg, FullBags, LooseKg, TotalKg, LastUpdated) VALUES (?, ?, 0, 0, 0, ?)", conn);
                stockCmd.Parameters.Add("?", OleDbType.Integer).Value = itemId;
                stockCmd.Parameters.Add("?", OleDbType.Double).Value = size;
                stockCmd.Parameters.Add("?", OleDbType.Date).Value = now;
                stockCmd.ExecuteNonQuery();
            }
        }
    }
}
