using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public sealed class AccountBalanceSnapshot
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;
    [DataType(DataType.Date)]
    public DateTime SnapshotDate { get; set; } = DateTime.Today;
    public decimal Balance { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
