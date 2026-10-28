using System;

namespace PolyStock.Core.Models;

public class Godown
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public override string ToString() => string.IsNullOrWhiteSpace(Code) ? Name : $"[{Code}] {Name}";
}
