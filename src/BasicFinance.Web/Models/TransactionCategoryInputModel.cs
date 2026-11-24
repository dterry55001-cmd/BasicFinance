using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public sealed class TransactionCategoryInputModel
{
    public int? Id { get; set; }

    [Required, StringLength(80)]
    [Display(Name = "Category name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Category type")]
    public TransactionCategoryType Type { get; set; } = TransactionCategoryType.Expense;

    [Display(Name = "Parent category")]
    public int? ParentCategoryId { get; set; }

    [StringLength(300)]
    public string? Description { get; set; }

    [Display(Name = "Display order")]
    [Range(0, 10000)]
    public int DisplayOrder { get; set; }
}
