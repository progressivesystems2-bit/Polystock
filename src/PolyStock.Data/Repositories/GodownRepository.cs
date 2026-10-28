using System;
using System.Collections.Generic;
using System.Data.OleDb;
using PolyStock.Core.Models;

namespace PolyStock.Data.Repositories;

public class GodownRepository
{
    private readonly string? _dbPath;
    public GodownRepository(string? dbPath = null) => _dbPath = dbPath;

    public List<Godown> GetAll()
    {
        var list = new List<Godown>();
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        using var cmd = new OleDbCommand("SELECT Id, [Code], [Name], [Location], [ContactPerson], [Phone], [Description], [IsActive], CreatedAt FROM Godowns ORDER BY [Name]", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Godown
            {
                Id = Convert.ToInt32(reader["Id"]),
                Code = reader["Code"]?.ToString() ?? string.Empty,
                Name = reader["Name"]?.ToString() ?? string.Empty,
                Location = reader["Location"]?.ToString(),
                ContactPerson = reader["ContactPerson"]?.ToString(),
                Phone = reader["Phone"]?.ToString(),
                Description = reader["Description"]?.ToString(),
                IsActive = reader["IsActive"] == DBNull.Value || Convert.ToBoolean(reader["IsActive"]),
                CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue
            });
        }
        return list;
    }

    public int Add(Godown godown)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        using var cmd = new OleDbCommand("INSERT INTO Godowns ([Code], [Name], [Location], [ContactPerson], [Phone], [Description], [IsActive], CreatedAt) VALUES (?, ?, ?, ?, ?, ?, ?, ?)", conn);
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)godown.Code ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = godown.Name;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)godown.Location ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)godown.ContactPerson ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)godown.Phone ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)godown.Description ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.Boolean).Value = godown.IsActive;
        cmd.Parameters.Add("?", OleDbType.Date).Value = godown.CreatedAt == DateTime.MinValue ? DateTime.Now : godown.CreatedAt;
        cmd.ExecuteNonQuery();

        using var idCmd = new OleDbCommand("SELECT @@IDENTITY", conn);
        return Convert.ToInt32(idCmd.ExecuteScalar());
    }

    public void Update(Godown godown)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        using var cmd = new OleDbCommand("UPDATE Godowns SET [Code] = ?, [Name] = ?, [Location] = ?, [ContactPerson] = ?, [Phone] = ?, [Description] = ?, [IsActive] = ? WHERE Id = ?", conn);
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)godown.Code ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = godown.Name;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)godown.Location ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)godown.ContactPerson ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)godown.Phone ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)godown.Description ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.Boolean).Value = godown.IsActive;
        cmd.Parameters.Add("?", OleDbType.Integer).Value = godown.Id;
        cmd.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        using var cmd = new OleDbCommand("DELETE FROM Godowns WHERE Id = ?", conn);
        cmd.Parameters.Add("?", OleDbType.Integer).Value = id;
        cmd.ExecuteNonQuery();
    }
}
