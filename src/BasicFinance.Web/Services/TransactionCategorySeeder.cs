using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Services;

public sealed class TransactionCategorySeeder(ApplicationDbContext database)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await database.TransactionCategories.AnyAsync(cancellationToken))
            return;

        var categories = new[]
        {
            new TransactionCategory { Name = "Salary and Wages", Type = TransactionCategoryType.Income, DisplayOrder = 10 },
            new TransactionCategory { Name = "Bonus", Type = TransactionCategoryType.Income, DisplayOrder = 20 },
            new TransactionCategory { Name = "Interest Income", Type = TransactionCategoryType.Income, DisplayOrder = 30 },
            new TransactionCategory { Name = "Dividends", Type = TransactionCategoryType.Income, DisplayOrder = 40 },
            new TransactionCategory { Name = "Retirement Income", Type = TransactionCategoryType.Income, DisplayOrder = 50 },
            new TransactionCategory { Name = "Rental Income", Type = TransactionCategoryType.Income, DisplayOrder = 60 },
            new TransactionCategory { Name = "Other Income", Type = TransactionCategoryType.Income, DisplayOrder = 90 },

            new TransactionCategory { Name = "Housing", Type = TransactionCategoryType.Expense, DisplayOrder = 10 },
            new TransactionCategory { Name = "Transportation", Type = TransactionCategoryType.Expense, DisplayOrder = 20 },
            new TransactionCategory { Name = "Food and Dining", Type = TransactionCategoryType.Expense, DisplayOrder = 30 },
            new TransactionCategory { Name = "Health and Personal", Type = TransactionCategoryType.Expense, DisplayOrder = 40 },
            new TransactionCategory { Name = "Shopping and Lifestyle", Type = TransactionCategoryType.Expense, DisplayOrder = 50 },
            new TransactionCategory { Name = "Financial", Type = TransactionCategoryType.Expense, DisplayOrder = 60 },
            new TransactionCategory { Name = "Family and Personal", Type = TransactionCategoryType.Expense, DisplayOrder = 70 },
            new TransactionCategory { Name = "Taxes and Insurance", Type = TransactionCategoryType.Expense, DisplayOrder = 80 },
            new TransactionCategory { Name = "Other Expense", Type = TransactionCategoryType.Expense, DisplayOrder = 90 },
            new TransactionCategory { Name = "Uncategorized", Type = TransactionCategoryType.Expense, DisplayOrder = 100 },

            new TransactionCategory { Name = "Account Transfer", Type = TransactionCategoryType.Transfer, DisplayOrder = 10 },
            new TransactionCategory { Name = "Savings Transfer", Type = TransactionCategoryType.Transfer, DisplayOrder = 20 },
            new TransactionCategory { Name = "Other Transfer", Type = TransactionCategoryType.Transfer, DisplayOrder = 90 }
        };

        foreach (var category in categories)
        {
            category.IsSystem = true;
            category.CreatedUtc = DateTime.UtcNow;
            category.UpdatedUtc = DateTime.UtcNow;
        }

        database.TransactionCategories.AddRange(categories);
        await database.SaveChangesAsync(cancellationToken);
    }
}
