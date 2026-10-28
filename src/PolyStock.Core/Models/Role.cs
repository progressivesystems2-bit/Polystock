using System;
using System.Collections.Generic;

namespace PolyStock.Core.Models;

public class Role
{
    public int Id { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public List<RolePermission> Permissions { get; set; } = new();

    public override string ToString() => RoleName;
}
