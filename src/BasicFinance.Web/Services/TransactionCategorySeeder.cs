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

        var roots = new[]
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

        var now = DateTime.UtcNow;
        foreach (var category in roots)
        {
            category.IsSystem = true;
            category.CreatedUtc = now;
            category.UpdatedUtc = now;
        }
        database.TransactionCategories.AddRange(roots);
        await database.SaveChangesAsync(cancellationToken);

        var rootByName = roots.ToDictionary(category => category.Name);
        var children = new (string Parent, string[] Names)[]
        {
            ("Housing", ["Mortgage or Rent", "Property Taxes", "Home Insurance", "Utilities", "Internet", "Home Maintenance", "Home Improvement"]),
            ("Transportation", ["Fuel", "Auto Payment", "Auto Insurance", "Maintenance and Repairs", "Registration", "Parking and Tolls", "Public Transportation"]),
            ("Food and Dining", ["Groceries", "Restaurants", "Coffee", "Takeout", "Delivery"]),
            ("Health and Personal", ["Medical", "Dental", "Vision", "Pharmacy", "Personal Care", "Fitness"]),
            ("Shopping and Lifestyle", ["Clothing", "Electronics", "Household Supplies", "Entertainment", "Hobbies", "Games", "Subscriptions"]),
            ("Financial", ["Credit Card Payments", "Loan Payments", "Bank Fees", "Savings Contributions"]),
            ("Family and Personal", ["Education", "Childcare", "Gifts", "Holidays", "Travel", "Charity", "Pet Expenses"]),
            ("Taxes and Insurance", ["Income Taxes", "Life Insurance", "Other Insurance"])
        };

        var subcategories = children.SelectMany(group => group.Names.Select((name, index) => new TransactionCategory
        {
            Name = name,
            Type = TransactionCategoryType.Expense,
            ParentCategory = rootByName[group.Parent],
            IsSystem = true,
            DisplayOrder = (index + 1) * 10,
            CreatedUtc = now,
            UpdatedUtc = now
        })).ToArray();

        database.TransactionCategories.AddRange(subcategories);
        await database.SaveChangesAsync(cancellationToken);
    }
}
