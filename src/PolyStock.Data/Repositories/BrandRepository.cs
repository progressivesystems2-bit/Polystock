using System;
using System.Collections.Generic;
using System.Data.OleDb;
using PolyStock.Core.Models;

namespace PolyStock.Data.Repositories;

public class BrandRepository
{
    private readonly string? _dbPath;
    public BrandRepository(string? dbPath = null) => _dbPath = dbPath;

    public List<Brand> GetAll()
    {
        var list = new List<Brand>();
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        using var cmd = new OleDbCommand("SELECT Id, [Name], [Description], CreatedAt FROM Brands ORDER BY [Name]", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Brand
            {
                Id = Convert.ToInt32(reader["Id"]),
                Name = reader["Name"]?.ToString() ?? string.Empty,
                Description = reader["Description"]?.ToString(),
                CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue
            });
        }
        return list;
    }

    public int Add(Brand brand)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        using var cmd = new OleDbCommand("INSERT INTO Brands ([Name], [Description], CreatedAt) VALUES (?, ?, ?)", conn);
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = brand.Name;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)brand.Description ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.Date).Value = brand.CreatedAt == DateTime.MinValue ? DateTime.Now : brand.CreatedAt;
        cmd.ExecuteNonQuery();

        using var idCmd = new OleDbCommand("SELECT @@IDENTITY", conn);
        return Convert.ToInt32(idCmd.ExecuteScalar());
    }

    public void Update(Brand brand)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        using var cmd = new OleDbCommand("UPDATE Brands SET [Name] = ?, [Description] = ? WHERE Id = ?", conn);
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = brand.Name;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)brand.Description ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.Integer).Value = brand.Id;
        cmd.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        using var cmd = new OleDbCommand("DELETE FROM Brands WHERE Id = ?", conn);
        cmd.Parameters.Add("?", OleDbType.Integer).Value = id;
        cmd.ExecuteNonQuery();
    }
}
