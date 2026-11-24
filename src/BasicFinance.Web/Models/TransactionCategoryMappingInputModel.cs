using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public sealed class TransactionCategoryMappingInputModel
{
    public int ImportId { get; set; }
    public int RowId { get; set; }
    public int CategoryId { get; set; }
    public bool ApplyToFutureImports { get; set; } = true;
}
