using System;

namespace PolyStock.Core.Models;

public class AccountLedgerEntry
{
    public int Id { get; set; }
    public int? AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty; // SALE, PURCHASE, SALE_MODIFIED, PURCHASE_MODIFIED, OPENING_BALANCE, PAYMENT
    public string RefInvoiceNo { get; set; } = string.Empty;
    public double Debit { get; set; } // Receivable from customer (Sales)
    public double Credit { get; set; } // Payable to supplier (Purchases)
    public double Balance { get; set; }
    public string? Remarks { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
