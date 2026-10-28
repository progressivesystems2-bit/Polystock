using System;
using System.Collections.Generic;
using System.Data.OleDb;
using PolyStock.Core.Models;

namespace PolyStock.Data.Repositories;

public class UserRepository
{
    private readonly string? _dbPath;

    public UserRepository(string? dbPath = null)
    {
        _dbPath = dbPath;
    }

    public List<User> GetAll()
    {
        var list = new List<User>();
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        using var cmd = new OleDbCommand(@"
            SELECT u.Id, u.Username, u.DisplayName, u.PasswordHash, u.Salt, u.RoleId, u.RoleName, u.IsActive, u.CreatedAt, u.LastLoginAt, r.RoleName AS RoleNameFromRoles
            FROM Users u
            LEFT JOIN Roles r ON u.RoleId = r.Id
            ORDER BY u.Username", conn);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var roleName = reader["RoleName"]?.ToString();
            if (string.IsNullOrWhiteSpace(roleName))
            {
                roleName = reader["RoleNameFromRoles"]?.ToString() ?? string.Empty;
            }

            list.Add(new User
            {
                Id = Convert.ToInt32(reader["Id"]),
                Username = reader["Username"]?.ToString() ?? string.Empty,
                DisplayName = reader["DisplayName"]?.ToString() ?? string.Empty,
                PasswordHash = reader["PasswordHash"]?.ToString() ?? string.Empty,
                Salt = reader["Salt"]?.ToString() ?? string.Empty,
                RoleId = Convert.ToInt32(reader["RoleId"]),
                RoleName = roleName,
                IsActive = reader["IsActive"] != DBNull.Value && Convert.ToBoolean(reader["IsActive"]),
                CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue,
                LastLoginAt = reader["LastLoginAt"] != DBNull.Value ? Convert.ToDateTime(reader["LastLoginAt"]) : null
            });
        }

        return list;
    }

    public User? GetById(int id)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        using var cmd = new OleDbCommand(@"
            SELECT u.Id, u.Username, u.DisplayName, u.PasswordHash, u.Salt, u.RoleId, u.RoleName, u.IsActive, u.CreatedAt, u.LastLoginAt, r.RoleName AS RoleNameFromRoles
            FROM Users u
            LEFT JOIN Roles r ON u.RoleId = r.Id
            WHERE u.Id = ?", conn);
        cmd.Parameters.Add("?", OleDbType.Integer).Value = id;

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var roleName = reader["RoleName"]?.ToString();
            if (string.IsNullOrWhiteSpace(roleName))
            {
                roleName = reader["RoleNameFromRoles"]?.ToString() ?? string.Empty;
            }

            return new User
            {
                Id = Convert.ToInt32(reader["Id"]),
                Username = reader["Username"]?.ToString() ?? string.Empty,
                DisplayName = reader["DisplayName"]?.ToString() ?? string.Empty,
                PasswordHash = reader["PasswordHash"]?.ToString() ?? string.Empty,
                Salt = reader["Salt"]?.ToString() ?? string.Empty,
                RoleId = Convert.ToInt32(reader["RoleId"]),
                RoleName = roleName,
                IsActive = reader["IsActive"] != DBNull.Value && Convert.ToBoolean(reader["IsActive"]),
                CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue,
                LastLoginAt = reader["LastLoginAt"] != DBNull.Value ? Convert.ToDateTime(reader["LastLoginAt"]) : null
            };
        }

        return null;
    }

    public User? GetByUsername(string username)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        using var cmd = new OleDbCommand(@"
            SELECT u.Id, u.Username, u.DisplayName, u.PasswordHash, u.Salt, u.RoleId, u.RoleName, u.IsActive, u.CreatedAt, u.LastLoginAt, r.RoleName AS RoleNameFromRoles
            FROM Users u
            LEFT JOIN Roles r ON u.RoleId = r.Id
            WHERE u.Username = ?", conn);
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = username.Trim();

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var roleName = reader["RoleName"]?.ToString();
            if (string.IsNullOrWhiteSpace(roleName))
            {
                roleName = reader["RoleNameFromRoles"]?.ToString() ?? string.Empty;
            }

            return new User
            {
                Id = Convert.ToInt32(reader["Id"]),
                Username = reader["Username"]?.ToString() ?? string.Empty,
                DisplayName = reader["DisplayName"]?.ToString() ?? string.Empty,
                PasswordHash = reader["PasswordHash"]?.ToString() ?? string.Empty,
                Salt = reader["Salt"]?.ToString() ?? string.Empty,
                RoleId = Convert.ToInt32(reader["RoleId"]),
                RoleName = roleName,
                IsActive = reader["IsActive"] != DBNull.Value && Convert.ToBoolean(reader["IsActive"]),
                CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue,
                LastLoginAt = reader["LastLoginAt"] != DBNull.Value ? Convert.ToDateTime(reader["LastLoginAt"]) : null
            };
        }

        return null;
    }

    public int Add(User user)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        using var cmd = new OleDbCommand(@"
            INSERT INTO Users (Username, DisplayName, PasswordHash, Salt, RoleId, RoleName, IsActive, CreatedAt)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?)", conn);
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = user.Username.Trim();
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = user.DisplayName.Trim();
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = user.PasswordHash;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = user.Salt;
        cmd.Parameters.Add("?", OleDbType.Integer).Value = user.RoleId;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)user.RoleName ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.Boolean).Value = user.IsActive;
        cmd.Parameters.Add("?", OleDbType.Date).Value = user.CreatedAt == DateTime.MinValue ? DateTime.Now : user.CreatedAt;
        cmd.ExecuteNonQuery();

        using var idCmd = new OleDbCommand("SELECT @@IDENTITY", conn);
        int id = Convert.ToInt32(idCmd.ExecuteScalar());
        user.Id = id;
        return id;
    }

    public void Update(User user)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        using var cmd = new OleDbCommand(@"
            UPDATE Users
            SET DisplayName = ?, RoleId = ?, RoleName = ?, IsActive = ?
            WHERE Id = ?", conn);
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = user.DisplayName.Trim();
        cmd.Parameters.Add("?", OleDbType.Integer).Value = user.RoleId;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)user.RoleName ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.Boolean).Value = user.IsActive;
        cmd.Parameters.Add("?", OleDbType.Integer).Value = user.Id;
        cmd.ExecuteNonQuery();
    }

    public void UpdatePassword(int id, string passwordHash, string salt)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        using var cmd = new OleDbCommand("UPDATE Users SET PasswordHash = ?, Salt = ? WHERE Id = ?", conn);
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = passwordHash;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = salt;
        cmd.Parameters.Add("?", OleDbType.Integer).Value = id;
        cmd.ExecuteNonQuery();
    }

    public void UpdateLastLogin(int id, DateTime loginTime)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        using var cmd = new OleDbCommand("UPDATE Users SET LastLoginAt = ? WHERE Id = ?", conn);
        cmd.Parameters.Add("?", OleDbType.Date).Value = loginTime;
        cmd.Parameters.Add("?", OleDbType.Integer).Value = id;
        cmd.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        using var cmd = new OleDbCommand("DELETE FROM Users WHERE Id = ?", conn);
        cmd.Parameters.Add("?", OleDbType.Integer).Value = id;
        cmd.ExecuteNonQuery();
    }

    public int GetCount()
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();

        using var cmd = new OleDbCommand("SELECT COUNT(*) FROM Users", conn);
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }
}
