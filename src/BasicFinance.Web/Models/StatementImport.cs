using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public enum StatementImportStatus { Preview, Completed, CompletedWithErrors }

public sealed class StatementImport
{
    public int Id { get; set; }
    [Required, StringLength(255)]
    public string FileName { get; set; } = string.Empty;
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public StatementImportStatus Status { get; set; } = StatementImportStatus.Preview;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedUtc { get; set; }
    public ICollection<StatementImportRow> Rows { get; set; } = new List<StatementImportRow>();
}

public sealed class StatementImportRow
{
    public int Id { get; set; }
    public int StatementImportId { get; set; }
    public StatementImport StatementImport { get; set; } = null!;
    public int RowNumber { get; set; }
    public DateTime? TransactionDate { get; set; }
    [StringLength(160)]
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    [StringLength(80)]
    public string? Category { get; set; }
    [StringLength(100)]
    public string? Reference { get; set; }
    [StringLength(500)]
    public string? ValidationError { get; set; }
    public bool IsDuplicate { get; set; }
    public bool IsImported { get; set; }
}
