using System;
using System.Collections.Generic;

namespace PolyStock.Core.Models;

public class Item
{
    public int Id { get; set; }
    public int HeadGroupId { get; set; }
    public string HeadGroupName { get; set; } = string.Empty;
    public int BrandId { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public double DefaultRatePerKg { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public List<double> PackSizes { get; set; } = new();

    public override string ToString() => ItemName;
}

public class ItemPackSize
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public double PackSizeKg { get; set; }
    public bool IsActive { get; set; } = true;
}
