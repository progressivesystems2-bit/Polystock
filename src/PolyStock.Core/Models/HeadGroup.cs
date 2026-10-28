using System;

namespace PolyStock.Core.Models;

public class HeadGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public override string ToString() => Name;
}
