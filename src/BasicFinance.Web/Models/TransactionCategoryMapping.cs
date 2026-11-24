using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

/// <summary>Maps an institution's source category label to a BasicFinance category.</summary>
public sealed class TransactionCategoryMapping
{
    public int Id { get; set; }
    public StatementImportProfile Profile { get; set; }
    [Required, StringLength(100)]
    public string SourceCategory { get; set; } = string.Empty;
    [Required, StringLength(100)]
    public string NormalizedSourceCategory { get; set; } = string.Empty;
    public int TransactionCategoryId { get; set; }
    public TransactionCategory TransactionCategory { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
