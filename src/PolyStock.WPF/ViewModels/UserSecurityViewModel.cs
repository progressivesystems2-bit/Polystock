using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using PolyStock.Core.Common;
using PolyStock.Core.Models;
using PolyStock.Services;

namespace PolyStock.WPF.ViewModels;

public class UserSecurityViewModel : ObservableObject
{
    private readonly InventoryService _inventoryService;
    private readonly SecurityService _security;

    private User? _selectedUser;
    private Role? _selectedRole;
    private string _searchText = string.Empty;

    // User Edit Form Fields
    private int _editingUserId;
    private string _username = string.Empty;
    private string _displayName = string.Empty;
    private int _selectedUserRoleId;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private bool _isActive = true;
    private bool _isNewUserMode = true;

    // Role Form Fields
    private string _newRoleName = string.Empty;
    private string _newRoleDescription = string.Empty;

    // Feedback message
    private string _statusMessage = string.Empty;
    private bool _isErrorMessage;

    public UserSecurityViewModel(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
        _security = inventoryService.Security;

        SaveUserCommand = new RelayCommand(SaveUser);
        NewUserCommand = new RelayCommand(ResetUserForm);
        DeleteUserCommand = new RelayCommand(DeleteUser, () => SelectedUser != null && !IsNewUserMode);
        ToggleUserStatusCommand = new RelayCommand(ToggleUserStatus, () => SelectedUser != null && !IsNewUserMode);

        SavePermissionsCommand = new RelayCommand(SavePermissions, () => SelectedRole != null);
        SelectAllPermissionsCommand = new RelayCommand(SelectAllPermissions, () => SelectedRole != null);
        ClearAllPermissionsCommand = new RelayCommand(ClearAllPermissions, () => SelectedRole != null);
        ResetRoleToDefaultsCommand = new RelayCommand(ResetRoleToDefaults, () => SelectedRole != null);

        AddRoleCommand = new RelayCommand(AddRole);
        DeleteRoleCommand = new RelayCommand(DeleteRole, () => SelectedRole != null && !SelectedRole.IsSystemRole);

        LoadData();
    }

    public ObservableCollection<User> Users { get; } = new();
    public ObservableCollection<User> FilteredUsers { get; } = new();
    public ObservableCollection<Role> Roles { get; } = new();
    public ObservableCollection<RolePermission> RolePermissions { get; } = new();

    public User? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (SetProperty(ref _selectedUser, value) && value != null)
            {
                PopulateUserForm(value);
            }
        }
    }

    public Role? SelectedRole
    {
        get => _selectedRole;
        set
        {
            if (SetProperty(ref _selectedRole, value) && value != null)
            {
                LoadRolePermissions(value.Id);
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                FilterUsers();
            }
        }
    }

    public int EditingUserId
    {
        get => _editingUserId;
        set => SetProperty(ref _editingUserId, value);
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public int SelectedUserRoleId
    {
        get => _selectedUserRoleId;
        set => SetProperty(ref _selectedUserRoleId, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => SetProperty(ref _confirmPassword, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public bool IsNewUserMode
    {
        get => _isNewUserMode;
        set
        {
            if (SetProperty(ref _isNewUserMode, value))
            {
                OnPropertyChanged(nameof(UserFormTitle));
                OnPropertyChanged(nameof(CanEditUsername));
                OnPropertyChanged(nameof(IsExistingUser));
            }
        }
    }

    public string UserFormTitle => IsNewUserMode ? "Create New User Account" : $"Edit User: {Username}";
    public bool CanEditUsername => IsNewUserMode;
    public bool IsExistingUser => !IsNewUserMode;

    public string NewRoleName
    {
        get => _newRoleName;
        set => SetProperty(ref _newRoleName, value);
    }

    public string NewRoleDescription
    {
        get => _newRoleDescription;
        set => SetProperty(ref _newRoleDescription, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsErrorMessage
    {
        get => _isErrorMessage;
        set => SetProperty(ref _isErrorMessage, value);
    }

    public ICommand SaveUserCommand { get; }
    public ICommand NewUserCommand { get; }
    public ICommand DeleteUserCommand { get; }
    public ICommand ToggleUserStatusCommand { get; }
    public ICommand SavePermissionsCommand { get; }
    public ICommand SelectAllPermissionsCommand { get; }
    public ICommand ClearAllPermissionsCommand { get; }
    public ICommand ResetRoleToDefaultsCommand { get; }
    public ICommand AddRoleCommand { get; }
    public ICommand DeleteRoleCommand { get; }

    public void LoadData()
    {
        // 1. Load Roles
        Roles.Clear();
        var allRoles = _security.GetAllRoles();
        foreach (var r in allRoles)
        {
            Roles.Add(r);
        }

        if (SelectedRole == null && Roles.Count > 0)
        {
            SelectedRole = Roles.FirstOrDefault(r => r.RoleName == "Administrator") ?? Roles[0];
        }
        else if (SelectedRole != null)
        {
            LoadRolePermissions(SelectedRole.Id);
        }

        // 2. Load Users
        Users.Clear();
        var allUsers = _security.GetAllUsers();
        foreach (var u in allUsers)
        {
            Users.Add(u);
        }

        FilterUsers();

        if (IsNewUserMode && Roles.Count > 0 && SelectedUserRoleId == 0)
        {
            SelectedUserRoleId = Roles[0].Id;
        }
    }

    private void FilterUsers()
    {
        FilteredUsers.Clear();
        var q = _searchText?.Trim() ?? string.Empty;
        var list = string.IsNullOrEmpty(q)
            ? Users
            : Users.Where(u => u.Username.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                               u.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                               u.RoleName.Contains(q, StringComparison.OrdinalIgnoreCase));

        foreach (var u in list)
        {
            FilteredUsers.Add(u);
        }
    }

    private void PopulateUserForm(User user)
    {
        EditingUserId = user.Id;
        Username = user.Username;
        DisplayName = user.DisplayName;
        SelectedUserRoleId = user.RoleId;
        IsActive = user.IsActive;
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        IsNewUserMode = false;
        ClearMessage();
    }

    public void ResetUserForm()
    {
        EditingUserId = 0;
        Username = string.Empty;
        DisplayName = string.Empty;
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        IsActive = true;
        IsNewUserMode = true;
        _selectedUser = null;
        OnPropertyChanged(nameof(SelectedUser));

        if (Roles.Count > 0)
        {
            SelectedUserRoleId = Roles[0].Id;
        }

        ClearMessage();
    }

    private void SaveUser()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Username))
            {
                SetMessage("Username cannot be empty.", isError: true);
                return;
            }

            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                SetMessage("Full name cannot be empty.", isError: true);
                return;
            }

            if (SelectedUserRoleId <= 0)
            {
                SetMessage("Please select a security role.", isError: true);
                return;
            }

            if (IsNewUserMode)
            {
                if (string.IsNullOrEmpty(Password))
                {
                    SetMessage("Password is required for new users.", isError: true);
                    return;
                }

                if (Password != ConfirmPassword)
                {
                    SetMessage("Passwords do not match.", isError: true);
                    return;
                }

                var existing = _security.GetAllUsers().FirstOrDefault(u => u.Username.Equals(Username.Trim(), StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    SetMessage($"Username '{Username}' already exists. Choose another.", isError: true);
                    return;
                }

                var role = Roles.FirstOrDefault(r => r.Id == SelectedUserRoleId);
                var newUser = new User
                {
                    Username = Username.Trim(),
                    DisplayName = DisplayName.Trim(),
                    RoleId = SelectedUserRoleId,
                    RoleName = role?.RoleName ?? string.Empty,
                    IsActive = IsActive,
                    CreatedAt = DateTime.Now
                };

                _security.SaveUser(newUser, Password);
                SetMessage($"User '{newUser.Username}' created successfully.", isError: false);
            }
            else
            {
                if (!string.IsNullOrEmpty(Password))
                {
                    if (Password != ConfirmPassword)
                    {
                        SetMessage("Passwords do not match.", isError: true);
                        return;
                    }
                }

                var role = Roles.FirstOrDefault(r => r.Id == SelectedUserRoleId);
                var user = new User
                {
                    Id = EditingUserId,
                    Username = Username.Trim(),
                    DisplayName = DisplayName.Trim(),
                    RoleId = SelectedUserRoleId,
                    RoleName = role?.RoleName ?? string.Empty,
                    IsActive = IsActive
                };

                _security.SaveUser(user, string.IsNullOrEmpty(Password) ? null : Password);
                SetMessage($"User '{user.Username}' updated successfully.", isError: false);
            }

            LoadData();
            var updated = Users.FirstOrDefault(u => u.Username.Equals(Username, StringComparison.OrdinalIgnoreCase));
            if (updated != null)
            {
                SelectedUser = updated;
            }
        }
        catch (Exception ex)
        {
            SetMessage($"Error saving user: {ex.Message}", isError: true);
        }
    }

    private void DeleteUser()
    {
        if (SelectedUser == null || IsNewUserMode) return;

        try
        {
            var uName = SelectedUser.Username;
            _security.DeleteUser(SelectedUser.Id);
            SetMessage($"User '{uName}' was deleted.", isError: false);
            ResetUserForm();
            LoadData();
        }
        catch (Exception ex)
        {
            SetMessage(ex.Message, isError: true);
        }
    }

    private void ToggleUserStatus()
    {
        if (SelectedUser == null || IsNewUserMode) return;

        try
        {
            SelectedUser.IsActive = !SelectedUser.IsActive;
            _security.SaveUser(SelectedUser);
            IsActive = SelectedUser.IsActive;
            SetMessage($"User '{SelectedUser.Username}' status changed to {(SelectedUser.IsActive ? "Active" : "Inactive")}.", isError: false);
            FilterUsers();
        }
        catch (Exception ex)
        {
            SetMessage(ex.Message, isError: true);
        }
    }

    private void LoadRolePermissions(int roleId)
    {
        RolePermissions.Clear();
        var perms = _security.GetRolePermissions(roleId);
        foreach (var p in perms)
        {
            RolePermissions.Add(p);
        }
    }

    private void SavePermissions()
    {
        if (SelectedRole == null) return;

        try
        {
            _security.SaveRolePermissions(SelectedRole.Id, RolePermissions);
            SetMessage($"Permissions for role '{SelectedRole.RoleName}' saved successfully.", isError: false);
        }
        catch (Exception ex)
        {
            SetMessage($"Error saving permissions: {ex.Message}", isError: true);
        }
    }

    private void SelectAllPermissions()
    {
        foreach (var p in RolePermissions)
        {
            p.CanView = true;
            p.CanCreate = true;
            p.CanEdit = true;
            p.CanDelete = true;
            p.CanPrint = true;
        }
    }

    private void ClearAllPermissions()
    {
        foreach (var p in RolePermissions)
        {
            p.CanView = false;
            p.CanCreate = false;
            p.CanEdit = false;
            p.CanDelete = false;
            p.CanPrint = false;
        }
    }

    private void ResetRoleToDefaults()
    {
        if (SelectedRole == null) return;

        var rName = SelectedRole.RoleName;
        foreach (var p in RolePermissions)
        {
            p.CanView = false;
            p.CanCreate = false;
            p.CanEdit = false;
            p.CanDelete = false;
            p.CanPrint = false;

            if (rName.Equals("Administrator", StringComparison.OrdinalIgnoreCase))
            {
                p.CanView = p.CanCreate = p.CanEdit = p.CanDelete = p.CanPrint = true;
            }
            else if (rName.Equals("Manager", StringComparison.OrdinalIgnoreCase))
            {
                if (p.ModuleKey != PermissionModules.UserSecurity)
                {
                    p.CanView = p.CanCreate = p.CanEdit = p.CanDelete = p.CanPrint = true;
                }
            }
            else if (rName.Equals("Operator", StringComparison.OrdinalIgnoreCase))
            {
                if (p.Category == PermissionModules.CategoryMasters)
                {
                    p.CanView = true;
                }
                else if (p.Category == PermissionModules.CategoryEntries)
                {
                    p.CanView = true;
                    p.CanCreate = true;
                    p.CanPrint = true;
                }
                else if (p.Category == PermissionModules.CategoryReports && p.ModuleKey != PermissionModules.ProfitLossReport)
                {
                    p.CanView = true;
                    p.CanPrint = true;
                }
            }
            else if (rName.Equals("Auditor / Viewer", StringComparison.OrdinalIgnoreCase))
            {
                if (p.ModuleKey != PermissionModules.UserSecurity)
                {
                    p.CanView = true;
                    if (p.Category == PermissionModules.CategoryReports)
                    {
                        p.CanPrint = true;
                    }
                }
            }
        }
    }

    private void AddRole()
    {
        if (string.IsNullOrWhiteSpace(NewRoleName))
        {
            SetMessage("Role name cannot be empty.", isError: true);
            return;
        }

        var trimmed = NewRoleName.Trim();
        if (Roles.Any(r => r.RoleName.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            SetMessage($"Role '{trimmed}' already exists.", isError: true);
            return;
        }

        try
        {
            var newRole = new Role
            {
                RoleName = trimmed,
                Description = string.IsNullOrWhiteSpace(NewRoleDescription) ? $"{trimmed} Role" : NewRoleDescription.Trim(),
                IsSystemRole = false,
                CreatedAt = DateTime.Now
            };

            int newId = _security.SaveRole(newRole);
            newRole.Id = newId;

            // Generate initial permissions from template
            var initialPerms = PermissionModules.AllModules.Select(m => new RolePermission
            {
                RoleId = newId,
                ModuleKey = m.Key,
                CanView = false,
                CanCreate = false,
                CanEdit = false,
                CanDelete = false,
                CanPrint = false
            }).ToList();

            _security.SaveRolePermissions(newId, initialPerms);

            Roles.Add(newRole);
            SelectedRole = newRole;
            NewRoleName = string.Empty;
            NewRoleDescription = string.Empty;

            SetMessage($"Custom role '{trimmed}' created. You can now configure its permissions below.", isError: false);
        }
        catch (Exception ex)
        {
            SetMessage($"Error creating role: {ex.Message}", isError: true);
        }
    }

    private void DeleteRole()
    {
        if (SelectedRole == null || SelectedRole.IsSystemRole) return;

        try
        {
            var roleName = SelectedRole.RoleName;
            _security.DeleteRole(SelectedRole.Id);
            Roles.Remove(SelectedRole);
            SelectedRole = Roles.FirstOrDefault();
            SetMessage($"Role '{roleName}' deleted successfully.", isError: false);
        }
        catch (Exception ex)
        {
            SetMessage(ex.Message, isError: true);
        }
    }

    private void SetMessage(string message, bool isError)
    {
        StatusMessage = message;
        IsErrorMessage = isError;
    }

    private void ClearMessage()
    {
        StatusMessage = string.Empty;
        IsErrorMessage = false;
    }
}
