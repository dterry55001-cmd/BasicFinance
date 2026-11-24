using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Xunit;

namespace BasicFinance.Tests;

public sealed class TransactionCategoryMappingTests
{
    [Theory]
    [InlineData("  Food   and Dining ", "FOOD AND DINING")]
    [InlineData("salary", "SALARY")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormalizeCategory_ProducesStableLookupKey(string? source, string expected)
    {
        Assert.Equal(expected, StatementImportPreparationService.NormalizeCategory(source));
    }

    [Theory]
    [InlineData(TransactionCategoryType.Income, TransactionType.Income, true)]
    [InlineData(TransactionCategoryType.Income, TransactionType.Deposit, true)]
    [InlineData(TransactionCategoryType.Income, TransactionType.Expense, false)]
    [InlineData(TransactionCategoryType.Expense, TransactionType.Expense, true)]
    [InlineData(TransactionCategoryType.Expense, TransactionType.Withdrawal, true)]
    [InlineData(TransactionCategoryType.Expense, TransactionType.Income, false)]
    [InlineData(TransactionCategoryType.Transfer, TransactionType.Transfer, true)]
    [InlineData(TransactionCategoryType.Transfer, TransactionType.Deposit, false)]
    [InlineData(TransactionCategoryType.Transfer, TransactionType.Withdrawal, false)]
    public void IsCompatible_OnlyAllowsMatchingTransactionTypes(
        TransactionCategoryType categoryType,
        TransactionType transactionType,
        bool expected)
    {
        Assert.Equal(expected,
            StatementImportPreparationService.IsCompatible(categoryType, transactionType));
    }
}
