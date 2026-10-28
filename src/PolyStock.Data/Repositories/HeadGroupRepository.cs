using System;
using System.Collections.Generic;
using System.Data.OleDb;
using PolyStock.Core.Models;

namespace PolyStock.Data.Repositories;

public class HeadGroupRepository
{
    private readonly string? _dbPath;
    public HeadGroupRepository(string? dbPath = null) => _dbPath = dbPath;

    public List<HeadGroup> GetAll()
    {
        var list = new List<HeadGroup>();
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        using var cmd = new OleDbCommand("SELECT Id, [Name], [Description], CreatedAt FROM HeadGroups ORDER BY [Name]", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new HeadGroup
            {
                Id = Convert.ToInt32(reader["Id"]),
                Name = reader["Name"]?.ToString() ?? string.Empty,
                Description = reader["Description"]?.ToString(),
                CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue
            });
        }
        return list;
    }

    public int Add(HeadGroup hg)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        using var cmd = new OleDbCommand("INSERT INTO HeadGroups ([Name], [Description], CreatedAt) VALUES (?, ?, ?)", conn);
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = hg.Name;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)hg.Description ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.Date).Value = hg.CreatedAt == DateTime.MinValue ? DateTime.Now : hg.CreatedAt;
        cmd.ExecuteNonQuery();

        using var idCmd = new OleDbCommand("SELECT @@IDENTITY", conn);
        return Convert.ToInt32(idCmd.ExecuteScalar());
    }

    public void Update(HeadGroup hg)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        using var cmd = new OleDbCommand("UPDATE HeadGroups SET [Name] = ?, [Description] = ? WHERE Id = ?", conn);
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = hg.Name;
        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = (object?)hg.Description ?? DBNull.Value;
        cmd.Parameters.Add("?", OleDbType.Integer).Value = hg.Id;
        cmd.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var conn = AccessConnectionProvider.CreateConnection(_dbPath);
        conn.Open();
        using var cmd = new OleDbCommand("DELETE FROM HeadGroups WHERE Id = ?", conn);
        cmd.Parameters.Add("?", OleDbType.Integer).Value = id;
        cmd.ExecuteNonQuery();
    }
}
