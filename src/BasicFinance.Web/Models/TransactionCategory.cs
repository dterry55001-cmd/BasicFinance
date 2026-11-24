using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public enum TransactionCategoryType
{
    Income,
    Expense,
    Transfer
}

public sealed class TransactionCategory
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    public string Name { get; set; } = string.Empty;

    public TransactionCategoryType Type { get; set; }

    public int? ParentCategoryId { get; set; }
    public TransactionCategory? ParentCategory { get; set; }
    public ICollection<TransactionCategory> Children { get; set; } = new List<TransactionCategory>();

    [StringLength(300)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsSystem { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
