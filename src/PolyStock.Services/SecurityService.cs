using System;
using System.Collections.Generic;
using System.Linq;
using PolyStock.Core.Common;
using PolyStock.Core.Models;
using PolyStock.Data.Repositories;

namespace PolyStock.Services;

public class LoginResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public User? User { get; set; }

    public static LoginResult Succeeded(User user) => new() { Success = true, User = user, Message = "Login successful." };
    public static LoginResult Failed(string message) => new() { Success = false, Message = message };
}

public class SecurityService
{
    private readonly UserRepository _userRepo;
    private readonly RoleRepository _roleRepo;

    private User? _currentUser;
    private Role? _currentRole;
    private Dictionary<string, RolePermission> _currentPermissions = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler? UserChanged;

    public SecurityService(string? dbPath = null)
    {
        _userRepo = new UserRepository(dbPath);
        _roleRepo = new RoleRepository(dbPath);
    }

    public User? CurrentUser => _currentUser;
    public Role? CurrentRole => _currentRole;
    public bool IsAuthenticated => _currentUser != null;

    public bool IsAdmin => _currentUser != null &&
        (_currentUser.RoleName.Equals("Administrator", StringComparison.OrdinalIgnoreCase) ||
         (_currentRole != null && _currentRole.IsSystemRole && _currentRole.RoleName.Equals("Administrator", StringComparison.OrdinalIgnoreCase)));

    public LoginResult Authenticate(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return LoginResult.Failed("Please enter your username.");
        }

        if (string.IsNullOrEmpty(password))
        {
            return LoginResult.Failed("Please enter your password.");
        }

        var user = _userRepo.GetByUsername(username);
        if (user == null)
        {
            return LoginResult.Failed("Invalid username or password.");
        }

        if (!user.IsActive)
        {
            return LoginResult.Failed("Your account has been deactivated. Please contact an administrator.");
        }

        bool isValid = PasswordSecurity.VerifyPassword(password, user.PasswordHash, user.Salt);
        if (!isValid)
        {
            return LoginResult.Failed("Invalid username or password.");
        }

        // Update last login
        var now = DateTime.Now;
        _userRepo.UpdateLastLogin(user.Id, now);
        user.LastLoginAt = now;

        SetCurrentUserSession(user);

        return LoginResult.Succeeded(user);
    }

    public void SetCurrentUserSession(User user)
    {
        _currentUser = user;
        _currentRole = _roleRepo.GetById(user.RoleId);

        ReloadCurrentPermissions();

        UserChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Logout()
    {
        _currentUser = null;
        _currentRole = null;
        _currentPermissions.Clear();

        UserChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ReloadCurrentPermissions()
    {
        _currentPermissions.Clear();
        if (_currentUser != null)
        {
            var perms = _roleRepo.GetPermissionsByRoleId(_currentUser.RoleId);
            foreach (var p in perms)
            {
                _currentPermissions[p.ModuleKey] = p;
            }
        }
    }

    public bool HasPermission(string moduleKey, PermissionAction action)
    {
        if (_currentUser == null) return true;

        // Administrator role has full master privileges
        if (IsAdmin) return true;

        if (_currentPermissions.TryGetValue(moduleKey, out var perm))
        {
            return action switch
            {
                PermissionAction.View => perm.CanView,
                PermissionAction.Create => perm.CanCreate,
                PermissionAction.Edit => perm.CanEdit,
                PermissionAction.Delete => perm.CanDelete,
                PermissionAction.Print => perm.CanPrint,
                _ => false
            };
        }

        return false;
    }

    public bool CanView(string moduleKey) => HasPermission(moduleKey, PermissionAction.View);
    public bool CanCreate(string moduleKey) => HasPermission(moduleKey, PermissionAction.Create);
    public bool CanEdit(string moduleKey) => HasPermission(moduleKey, PermissionAction.Edit);
    public bool CanDelete(string moduleKey) => HasPermission(moduleKey, PermissionAction.Delete);
    public bool CanPrint(string moduleKey) => HasPermission(moduleKey, PermissionAction.Print);

    public bool CanAccessTab(string tabViewId)
    {
        if (_currentUser == null) return true;
        if (IsAdmin) return true;

        var moduleKey = PermissionModules.GetModuleKeyForTab(tabViewId);
        if (string.IsNullOrEmpty(moduleKey))
        {
            // Default accessible for tabs not explicitly restricted (e.g. Shortcuts)
            return true;
        }

        return CanView(moduleKey);
    }

    // User Management
    public List<User> GetAllUsers() => _userRepo.GetAll();
    public User? GetUserById(int id) => _userRepo.GetById(id);

    public int SaveUser(User user, string? plainTextPassword = null)
    {
        if (user.Id == 0)
        {
            if (string.IsNullOrWhiteSpace(plainTextPassword))
            {
                throw new ArgumentException("Password is required for new users.", nameof(plainTextPassword));
            }

            var (hash, salt) = PasswordSecurity.HashPassword(plainTextPassword);
            user.PasswordHash = hash;
            user.Salt = salt;
            user.CreatedAt = DateTime.Now;

            return _userRepo.Add(user);
        }
        else
        {
            _userRepo.Update(user);

            if (!string.IsNullOrWhiteSpace(plainTextPassword))
            {
                var (hash, salt) = PasswordSecurity.HashPassword(plainTextPassword);
                _userRepo.UpdatePassword(user.Id, hash, salt);
            }

            // If updated user is current user, update session
            if (_currentUser != null && _currentUser.Id == user.Id)
            {
                _currentUser.DisplayName = user.DisplayName;
                _currentUser.RoleId = user.RoleId;
                _currentUser.RoleName = user.RoleName;
                _currentUser.IsActive = user.IsActive;
                _currentRole = _roleRepo.GetById(user.RoleId);
                ReloadCurrentPermissions();
                UserChanged?.Invoke(this, EventArgs.Empty);
            }

            return user.Id;
        }
    }

    public void DeleteUser(int id)
    {
        if (_currentUser != null && _currentUser.Id == id)
        {
            throw new InvalidOperationException("You cannot delete the currently logged-in account.");
        }

        var user = _userRepo.GetById(id);
        if (user != null && user.Username.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The default 'admin' account cannot be deleted.");
        }

        _userRepo.Delete(id);
    }

    // Role & Permission Management
    public List<Role> GetAllRoles() => _roleRepo.GetAll();
    public Role? GetRoleById(int id) => _roleRepo.GetById(id);

    public int SaveRole(Role role)
    {
        if (role.Id == 0)
        {
            return _roleRepo.Add(role);
        }
        else
        {
            _roleRepo.Update(role);
            if (_currentRole != null && _currentRole.Id == role.Id)
            {
                _currentRole = role;
                ReloadCurrentPermissions();
                UserChanged?.Invoke(this, EventArgs.Empty);
            }
            return role.Id;
        }
    }

    public void DeleteRole(int id)
    {
        var role = _roleRepo.GetById(id);
        if (role != null && role.IsSystemRole)
        {
            throw new InvalidOperationException($"System role '{role.RoleName}' cannot be deleted.");
        }

        var usersInRole = _userRepo.GetAll().Where(u => u.RoleId == id).ToList();
        if (usersInRole.Count > 0)
        {
            throw new InvalidOperationException($"Cannot delete role '{role?.RoleName}' because {usersInRole.Count} user(s) are currently assigned to it.");
        }

        _roleRepo.Delete(id);
    }

    public List<RolePermission> GetRolePermissions(int roleId) => _roleRepo.GetPermissionsByRoleId(roleId);

    public void SaveRolePermissions(int roleId, IEnumerable<RolePermission> permissions)
    {
        _roleRepo.SaveRolePermissions(roleId, permissions);

        // If the updated role is the currently logged in user's role, reload immediately!
        if (_currentUser != null && _currentUser.RoleId == roleId)
        {
            ReloadCurrentPermissions();
            UserChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
