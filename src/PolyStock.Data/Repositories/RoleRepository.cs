using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Linq;
using PolyStock.Core.Common;
using PolyStock.Core.Models;

namespace PolyStock.Data.Repositories;

public class RoleRepository
{
    private readonly string? _dbPath;

    public RoleRepository(string? dbPath = null)
    {
        _dbPath = dbPath;
    }

    public List<Role> GetAll()
    {
        var list = new List<Role>();
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        using var cmd = new OleDbCommand("SELECT Id, RoleName, [Description], IsSystemRole, CreatedAt FROM Roles ORDER BY Id", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Role
            {
                Id = Convert.ToInt32(reader["Id"]),
                RoleName = reader["RoleName"]?.ToString() ?? string.Empty,
                Description = reader["Description"]?.ToString(),
                IsSystemRole = reader["IsSystemRole"] != DBNull.Value && Convert.ToBoolean(reader["IsSystemRole"]),
                CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue
            });
        }

        return list;
    }

    public Role? GetById(int id)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        using var cmd = new OleDbCommand("SELECT Id, RoleName, [Description], IsSystemRole, CreatedAt FROM Roles WHERE Id = ?", conn);
        cmd.Parameters.Add("?", OleDbType.Integer).Value = id;

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var role = new Role
            {
                Id = Convert.ToInt32(reader["Id"]),
                RoleName = reader["RoleName"]?.ToString() ?? string.Empty,
                Description = reader["Description"]?.ToString(),
                IsSystemRole = reader["IsSystemRole"] != DBNull.Value && Convert.ToBoolean(reader["IsSystemRole"]),
                CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue
            };
            return role;
        }

        return null;
    }

    public Role? GetByName(string name)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        using var cmd = new OleDbCommand("SELECT Id, RoleName, [Description], IsSystemRole, CreatedAt FROM Roles WHERE RoleName = ?", conn);
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = name;

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new Role
            {
                Id = Convert.ToInt32(reader["Id"]),
                RoleName = reader["RoleName"]?.ToString() ?? string.Empty,
                Description = reader["Description"]?.ToString(),
                IsSystemRole = reader["IsSystemRole"] != DBNull.Value && Convert.ToBoolean(reader["IsSystemRole"]),
                CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue
            };
        }

        return null;
    }

    public int Add(Role role)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        using var cmd = new OleDbCommand("INSERT INTO Roles (RoleName, [Description], IsSystemRole, CreatedAt) VALUES (?, ?, ?, ?)", conn);
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = role.RoleName;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)role.Description ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.Boolean).Value = role.IsSystemRole;
        cmd.Parameters.Add("?", OleDbType.Date).Value = role.CreatedAt == DateTime.MinValue ? DateTime.Now : role.CreatedAt;
        cmd.ExecuteNonQuery();

        using var idCmd = new OleDbCommand("SELECT @@IDENTITY", conn);
        int roleId = Convert.ToInt32(idCmd.ExecuteScalar());
        role.Id = roleId;

        if (role.Permissions != null && role.Permissions.Count > 0)
        {
            SaveRolePermissions(roleId, role.Permissions, conn);
        }

        return roleId;
    }

    public void Update(Role role)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        using var cmd = new OleDbCommand("UPDATE Roles SET RoleName = ?, [Description] = ?, IsSystemRole = ? WHERE Id = ?", conn);
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = role.RoleName;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)role.Description ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.Boolean).Value = role.IsSystemRole;
        cmd.Parameters.Add("?", OleDbType.Integer).Value = role.Id;
        cmd.ExecuteNonQuery();

        if (role.Permissions != null && role.Permissions.Count > 0)
        {
            SaveRolePermissions(role.Id, role.Permissions, conn);
        }
    }

    public void Delete(int id)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        // Delete permissions first
        using (var delPerms = new OleDbCommand("DELETE FROM RolePermissions WHERE RoleId = ?", conn))
        {
            delPerms.Parameters.Add("?", OleDbType.Integer).Value = id;
            delPerms.ExecuteNonQuery();
        }

        // Delete role
        using (var delRole = new OleDbCommand("DELETE FROM Roles WHERE Id = ?", conn))
        {
            delRole.Parameters.Add("?", OleDbType.Integer).Value = id;
            delRole.ExecuteNonQuery();
        }
    }

    public List<RolePermission> GetPermissionsByRoleId(int roleId)
    {
        var dbPermissions = new Dictionary<string, RolePermission>(StringComparer.OrdinalIgnoreCase);

        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        using (var cmd = new OleDbCommand("SELECT Id, RoleId, ModuleKey, CanView, CanCreate, CanEdit, CanDelete, CanPrint FROM RolePermissions WHERE RoleId = ?", conn))
        {
            cmd.Parameters.Add("?", OleDbType.Integer).Value = roleId;
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var modKey = reader["ModuleKey"]?.ToString() ?? string.Empty;
                var perm = new RolePermission
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    RoleId = roleId,
                    ModuleKey = modKey,
                    CanView = reader["CanView"] != DBNull.Value && Convert.ToBoolean(reader["CanView"]),
                    CanCreate = reader["CanCreate"] != DBNull.Value && Convert.ToBoolean(reader["CanCreate"]),
                    CanEdit = reader["CanEdit"] != DBNull.Value && Convert.ToBoolean(reader["CanEdit"]),
                    CanDelete = reader["CanDelete"] != DBNull.Value && Convert.ToBoolean(reader["CanDelete"]),
                    CanPrint = reader["CanPrint"] != DBNull.Value && Convert.ToBoolean(reader["CanPrint"])
                };
                dbPermissions[modKey] = perm;
            }
        }

        // Ensure all defined system modules are represented in the list
        var result = new List<RolePermission>();
        foreach (var def in PermissionModules.AllModules)
        {
            if (dbPermissions.TryGetValue(def.Key, out var existing))
            {
                existing.ModuleName = def.DisplayName;
                existing.Category = def.Category;
                existing.Description = def.Description;
                existing.Icon = def.Icon;
                result.Add(existing);
            }
            else
            {
                // Default unassigned permission
                result.Add(new RolePermission
                {
                    RoleId = roleId,
                    ModuleKey = def.Key,
                    ModuleName = def.DisplayName,
                    Category = def.Category,
                    Description = def.Description,
                    Icon = def.Icon,
                    CanView = false,
                    CanCreate = false,
                    CanEdit = false,
                    CanDelete = false,
                    CanPrint = false
                });
            }
        }

        return result;
    }

    public void SaveRolePermissions(int roleId, IEnumerable<RolePermission> permissions, OleDbConnection? existingConn = null)
    {
        bool closeConn = false;
        var conn = existingConn;
        if (conn == null)
        {
            conn = AccessConnectionProvider.CreateConnection(_dbPath);
            conn.Open();
            closeConn = true;
        }

        try
        {
            // Delete existing
            using (var delCmd = new OleDbCommand("DELETE FROM RolePermissions WHERE RoleId = ?", conn))
            {
                delCmd.Parameters.Add("?", OleDbType.Integer).Value = roleId;
                delCmd.ExecuteNonQuery();
            }

            // Insert new permissions
            foreach (var perm in permissions)
            {
                using var insCmd = new OleDbCommand(@"
                    INSERT INTO RolePermissions (RoleId, ModuleKey, CanView, CanCreate, CanEdit, CanDelete, CanPrint)
                    VALUES (?, ?, ?, ?, ?, ?, ?)", conn);
                insCmd.Parameters.Add("?", OleDbType.Integer).Value = roleId;
                insCmd.Parameters.Add("?", OleDbType.VarWChar).Value = perm.ModuleKey;
                insCmd.Parameters.Add("?", OleDbType.Boolean).Value = perm.CanView;
                insCmd.Parameters.Add("?", OleDbType.Boolean).Value = perm.CanCreate;
                insCmd.Parameters.Add("?", OleDbType.Boolean).Value = perm.CanEdit;
                insCmd.Parameters.Add("?", OleDbType.Boolean).Value = perm.CanDelete;
                insCmd.Parameters.Add("?", OleDbType.Boolean).Value = perm.CanPrint;
                insCmd.ExecuteNonQuery();
            }
        }
        finally
        {
            if (closeConn)
            {
                conn?.Dispose();
            }
        }
    }
}
