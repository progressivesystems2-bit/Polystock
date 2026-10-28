namespace PolyStock.Core.Models;

public class AppSettings
{
    /// <summary>
    /// Sales line items with total weight (KG) below this threshold will NOT be subtracted from inventory.
    /// Set to 0.0 to disable (all sales deduct inventory regardless of weight).
    /// </summary>
    public double SalesDeductionThresholdKg { get; set; } = 0.0;
}
