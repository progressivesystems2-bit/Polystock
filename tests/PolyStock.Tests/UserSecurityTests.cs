using System;
using System.IO;
using System.Linq;
using PolyStock.Core.Common;
using PolyStock.Core.Models;
using PolyStock.Data;
using PolyStock.Services;
using PolyStock.WPF.ViewModels;
using Xunit;

namespace PolyStock.Tests;

public class UserSecurityTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly InventoryService _inventoryService;

    public UserSecurityTests()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "PolyStockSecurityTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        _testDbPath = Path.Combine(tempDir, "TestSecurityPolyStock.accdb");

        try
        {
            DatabaseInitializer.Initialize(_testDbPath);
        }
        catch { }

        _inventoryService = new InventoryService(_testDbPath);
    }

    public void Dispose()
    {
        try
        {
            var dir = Path.GetDirectoryName(_testDbPath);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
        catch { }
    }

    [Fact]
    public void PasswordSecurity_HashAndVerify_Success()
    {
        var password = "SecretMasterPassword123!";
        var (hash, salt) = PasswordSecurity.HashPassword(password);

        Assert.False(string.IsNullOrEmpty(hash));
        Assert.False(string.IsNullOrEmpty(salt));
        Assert.NotEqual(password, hash);

        // Verification with correct password
        Assert.True(PasswordSecurity.VerifyPassword(password, hash, salt));

        // Verification with incorrect password
        Assert.False(PasswordSecurity.VerifyPassword("WrongPassword", hash, salt));
        Assert.False(PasswordSecurity.VerifyPassword("", hash, salt));
    }

    [Fact]
    public void DatabaseInitializer_SeedsSecurityTablesAndDefaultRolesAndAdmin()
    {
        var sec = _inventoryService.Security;
        var roles = sec.GetAllRoles();

        Assert.NotEmpty(roles);
        Assert.Contains(roles, r => r.RoleName == "Administrator" && r.IsSystemRole);
        Assert.Contains(roles, r => r.RoleName == "Manager");
        Assert.Contains(roles, r => r.RoleName == "Operator");
        Assert.Contains(roles, r => r.RoleName == "Auditor / Viewer");

        var users = sec.GetAllUsers();
        Assert.NotEmpty(users);

        var adminUser = users.FirstOrDefault(u => u.Username.Equals("admin", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(adminUser);
        Assert.Equal("Administrator", adminUser.RoleName);
        Assert.True(adminUser.IsActive);

        // Verify default admin password is 'admin'
        Assert.True(PasswordSecurity.VerifyPassword("admin", adminUser.PasswordHash, adminUser.Salt));
    }

    [Fact]
    public void SecurityService_Authentication_ValidAndInvalidCredentials()
    {
        var sec = _inventoryService.Security;

        // Valid credentials
        var result = sec.Authenticate("admin", "admin");
        Assert.True(result.Success);
        Assert.NotNull(result.User);
        Assert.Equal("admin", result.User.Username);
        Assert.True(sec.IsAuthenticated);
        Assert.True(sec.IsAdmin);

        // Invalid password
        var failedResult = sec.Authenticate("admin", "wrong_pass");
        Assert.False(failedResult.Success);
        Assert.Contains("Invalid", failedResult.Message);

        // Non-existent username
        var notFoundResult = sec.Authenticate("non_existent_user", "password");
        Assert.False(notFoundResult.Success);
        Assert.Contains("Invalid", notFoundResult.Message);
    }

    [Fact]
    public void SecurityService_RolePermissions_OperatorRestrictions()
    {
        var sec = _inventoryService.Security;
        var operatorRole = sec.GetAllRoles().FirstOrDefault(r => r.RoleName == "Operator");
        Assert.NotNull(operatorRole);

        // Create an operator user
        var operatorUser = new User
        {
            Username = "john_operator",
            DisplayName = "John Operator",
            RoleId = operatorRole.Id,
            RoleName = operatorRole.RoleName,
            IsActive = true
        };
        sec.SaveUser(operatorUser, "operator123");

        // Authenticate as operator
        var login = sec.Authenticate("john_operator", "operator123");
        Assert.True(login.Success);
        Assert.False(sec.IsAdmin);

        // Check Permissions:
        // Masters: Can View, Cannot Create/Delete
        Assert.True(sec.CanView(PermissionModules.ItemMaster));
        Assert.False(sec.CanCreate(PermissionModules.ItemMaster));
        Assert.False(sec.CanDelete(PermissionModules.ItemMaster));

        // Entries: Can View and Create Sales/Purchase, Cannot Delete
        Assert.True(sec.CanAccessTab("Sales"));
        Assert.True(sec.CanCreate(PermissionModules.SalesEntry));
        Assert.False(sec.CanDelete(PermissionModules.SalesEntry));

        // Reports: Can View Dashboard & Stock Register
        Assert.True(sec.CanAccessTab("Dashboard"));
        Assert.True(sec.CanAccessTab("StockRegister"));

        // UserSecurity: Strictly Denied for Operator
        Assert.False(sec.CanAccessTab("UserSecurity"));
        Assert.False(sec.CanView(PermissionModules.UserSecurity));
    }

    [Fact]
    public void SecurityService_ConfigurableCustomRole_EnforcesConfiguredPermissions()
    {
        var sec = _inventoryService.Security;

        // 1. Create a custom role: "Warehouse Clerk"
        var customRole = new Role
        {
            RoleName = "Warehouse Clerk",
            Description = "Only godowns and stock transfers allowed",
            IsSystemRole = false
        };
        int roleId = sec.SaveRole(customRole);

        // 2. Configure specific permissions across masters, entries, and reports
        var perms = sec.GetRolePermissions(roleId);
        foreach (var p in perms)
        {
            p.CanView = false;
            p.CanCreate = false;
            p.CanEdit = false;
            p.CanDelete = false;
            p.CanPrint = false;

            if (p.ModuleKey == PermissionModules.GodownMaster)
            {
                p.CanView = true;
                p.CanEdit = true;
            }
            else if (p.ModuleKey == PermissionModules.StockTransferEntry)
            {
                p.CanView = true;
                p.CanCreate = true;
                p.CanPrint = true;
            }
            else if (p.ModuleKey == PermissionModules.StockRegisterReport)
            {
                p.CanView = true;
                p.CanPrint = true;
            }
        }
        sec.SaveRolePermissions(roleId, perms);

        // 3. Create a user with this role
        var clerkUser = new User
        {
            Username = "clerk1",
            DisplayName = "Warehouse Clerk 1",
            RoleId = roleId,
            RoleName = "Warehouse Clerk",
            IsActive = true
        };
        sec.SaveUser(clerkUser, "clerkpass");

        // 4. Authenticate as clerk
        var login = sec.Authenticate("clerk1", "clerkpass");
        Assert.True(login.Success);

        // Allowed:
        Assert.True(sec.CanAccessTab("Godown"));
        Assert.True(sec.CanEdit(PermissionModules.GodownMaster));
        Assert.True(sec.CanAccessTab("StockTransfer"));
        Assert.True(sec.CanCreate(PermissionModules.StockTransferEntry));
        Assert.True(sec.CanAccessTab("StockRegister"));

        // Denied:
        Assert.False(sec.CanAccessTab("Sales"));
        Assert.False(sec.CanAccessTab("Purchase"));
        Assert.False(sec.CanAccessTab("Vouchers"));
        Assert.False(sec.CanAccessTab("AccountCreation"));
        Assert.False(sec.CanAccessTab("UserSecurity"));
        Assert.False(sec.CanAccessTab("LedgerReports"));
    }

    [Fact]
    public void UserSecurityViewModel_FullCrudAndMatrixWorkflow()
    {
        var vm = new UserSecurityViewModel(_inventoryService);
        vm.LoadData();

        Assert.NotEmpty(vm.Users);
        Assert.NotEmpty(vm.Roles);

        // Create a new user
        vm.ResetUserForm();
        vm.Username = "test_cashier";
        vm.DisplayName = "Test Cashier";
        vm.Password = "cashier123";
        vm.ConfirmPassword = "cashier123";
        var opRole = vm.Roles.FirstOrDefault(r => r.RoleName == "Operator");
        Assert.NotNull(opRole);
        vm.SelectedUserRoleId = opRole.Id;

        vm.SaveUserCommand.Execute(null);

        Assert.False(vm.IsErrorMessage);
        Assert.Contains(vm.Users, u => u.Username == "test_cashier");

        // Select and update user status
        var user = vm.Users.FirstOrDefault(u => u.Username == "test_cashier");
        Assert.NotNull(user);
        vm.SelectedUser = user;
        Assert.Equal("test_cashier", vm.Username);

        vm.ToggleUserStatusCommand.Execute(null);
        Assert.False(user.IsActive);

        // Test Role Permissions Tab
        vm.SelectedRole = opRole;
        Assert.NotEmpty(vm.RolePermissions);

        var salesPerm = vm.RolePermissions.FirstOrDefault(p => p.ModuleKey == PermissionModules.SalesEntry);
        Assert.NotNull(salesPerm);
        salesPerm.CanDelete = true; // Grant delete permission to test saving

        vm.SavePermissionsCommand.Execute(null);
        Assert.False(vm.IsErrorMessage);

        // Verify reloaded from repository
        var reloadedPerms = _inventoryService.Security.GetRolePermissions(opRole.Id);
        var reloadedSales = reloadedPerms.FirstOrDefault(p => p.ModuleKey == PermissionModules.SalesEntry);
        Assert.NotNull(reloadedSales);
        Assert.True(reloadedSales.CanDelete);
    }

    [Fact]
    public void Views_CanInitializeOnSTAThread()
    {
        Exception? threadEx = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                if (System.Windows.Application.Current == null)
                {
                    var app = new System.Windows.Application();
                    app.Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary
                    {
                        Source = new Uri("pack://application:,,,/PolyStock.WPF;component/Themes/FluentLight.xaml", UriKind.Absolute)
                    });
                    app.Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary
                    {
                        Source = new Uri("pack://application:,,,/PolyStock.WPF;component/Themes/ControlStyles.xaml", UriKind.Absolute)
                    });
                }

                var view = new PolyStock.WPF.Views.UserSecurityView();
                Assert.NotNull(view);

                var login = new PolyStock.WPF.Views.LoginWindow(_inventoryService.Security);
                Assert.NotNull(login);
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(threadEx);
    }
}
