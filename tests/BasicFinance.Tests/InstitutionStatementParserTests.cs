using BasicFinance.Web.Services;
using Xunit;

namespace BasicFinance.Tests;

public sealed class InstitutionStatementParserTests
{
    private readonly InstitutionStatementParser _parser = new();

    [Fact]
    public void DetectsUbsBrokerageAndMasksExternalAccountNumber()
    {
        const string csv = "Account Number,Date,Activity,Description,Symbol,Cusip,Type,Quantity,Price,Amount,Friendly Account Name\n1234567890,2026-01-05,Buy,Buy 2 ACME,ACME,123456789,Stock,2,10.25,-20.50,Retirement Brokerage";
        Assert.Equal(StatementSourceProfile.UbsBrokerage, _parser.DetectProfile(csv));

        var row = Assert.Single(_parser.Parse(csv, StatementSourceProfile.UbsBrokerage, "42"));
        Assert.Equal("7890", row.ExternalAccountNumberLast4);
        Assert.Equal("****7890", row.SourceFields["Account Number"]);
        Assert.Equal(-20.50m, row.SignedAmount);
        Assert.Equal("ACME", row.Symbol);
        Assert.Equal("Stock", row.SourceSubType);
    }

    [Fact]
    public void ParsesLifeSightContributionUnitsAndSource()
    {
        const string csv = "Transaction Type,Date,Source,Fund Name,Unit Count,Unit Value,Transaction Amount\nContribution,1/2/26,Employee PreTax,LifeSight Target Retirement 2055 Fund,12.154817,13.742700,167.04";
        Assert.Equal(StatementSourceProfile.LifeSightRetirement, _parser.DetectProfile(csv));

        var row = Assert.Single(_parser.Parse(csv, StatementSourceProfile.LifeSightRetirement, "42"));
        Assert.Equal("Employee PreTax", row.Source);
        Assert.Equal(12.154817m, row.UnitCount);
        Assert.Equal(13.742700m, row.UnitValue);
        Assert.Equal(167.04m, row.SignedAmount);
    }

    [Fact]
    public void ParsesAtomicTransactionIdRunningBalanceAndReviewStatus()
    {
        const string csv = "\"Transaction ID\",\"Posting Date\",\"Effective Date\",\"Transaction Type\",\"Posting Status\",\"Amount\",\"Check Number\",\"Reference Number\",\"Description\",\"Transaction Category\",\"Type\",\"Balance\",\"Memo\",\"Extended Description\"\nA-100,2026-01-05,2026-01-04,ACH,Pending,-45.25,0007,REF-1,Water bill,Utilities,Debit,954.75,January bill,City water";
        Assert.Equal(StatementSourceProfile.AtomicBanking, _parser.DetectProfile(csv));

        var row = Assert.Single(_parser.Parse(csv, StatementSourceProfile.AtomicBanking, "42"));
        Assert.Equal("A-100", row.SourceReference);
        Assert.Equal("0007", row.CheckNumber);
        Assert.Equal(954.75m, row.RunningBalance);
        Assert.Equal("January bill", row.Memo);
        Assert.Equal("City water", row.ExtendedDescription);
        Assert.Equal(new DateTime(2026, 1, 4), row.EffectiveDate);
        Assert.True(row.RequiresReview);
        Assert.False(row.CanPost);
    }

    [Fact]
    public void ParsesRobinhoodBrokerageDatesAndHoldsForCodeReview()
    {
        const string csv = "\"Activity Date\",\"Process Date\",\"Settle Date\",\"Instrument\",\"Description\",\"Trans Code\",\"Quantity\",\"Price\",\"Amount\"\n2026-01-05,2026-01-06,2026-01-07,ACME,Buy ACME,BUY,2,10.25,-20.50";
        Assert.Equal(StatementSourceProfile.RobinhoodBrokerage, _parser.DetectProfile(csv));

        var row = Assert.Single(_parser.Parse(csv, StatementSourceProfile.RobinhoodBrokerage, "42"));
        Assert.Equal(new DateTime(2026, 1, 5), row.ActivityDate);
        Assert.Equal(new DateTime(2026, 1, 6), row.ProcessDate);
        Assert.Equal(new DateTime(2026, 1, 7), row.SettlementDate);
        Assert.Equal("BUY", row.TransactionCode);
        Assert.True(row.RequiresReview);
    }

    [Fact]
    public void RequiresExplicitRobinhoodBankSelectionForGenericThreeColumnCsv()
    {
        const string csv = "Date,Description,Amount\n2026-01-05,Transfer to savings,-50.00";
        Assert.Equal(StatementSourceProfile.Unknown, _parser.DetectProfile(csv));

        var row = Assert.Single(_parser.Parse(csv, StatementSourceProfile.RobinhoodBank, "42"));
        Assert.Equal(-50.00m, row.SignedAmount);
    }

    [Fact]
    public void KeepsChaseTransactionAndPostDatesAndSignedAmount()
    {
        const string csv = "Transaction Date,Post Date,Description,Category,Type,Amount,Memo\n2026-01-04,2026-01-05,Card purchase,Shopping,Sale,(25.99),Store note";
        Assert.Equal(StatementSourceProfile.ChaseBanking, _parser.DetectProfile(csv));

        var row = Assert.Single(_parser.Parse(csv, StatementSourceProfile.ChaseBanking, "42"));
        Assert.Equal(new DateTime(2026, 1, 4), row.TransactionDate);
        Assert.Equal(new DateTime(2026, 1, 5), row.PostingDate);
        Assert.Equal(-25.99m, row.SignedAmount);
        Assert.Equal("Shopping", row.SourceCategory);
        Assert.Equal("Store note", row.Memo);
    }

    [Fact]
    public void MapperKeepsSignedAmountAndDoesNotAssignInstitutionCategoryAsUserCategory()
    {
        const string csv = "Transaction Date,Post Date,Description,Category,Type,Amount,Memo\n2026-01-04,2026-01-05,Card purchase,Shopping,Sale,-25.99,Store note";
        var parsed = Assert.Single(_parser.Parse(csv, StatementSourceProfile.ChaseBanking, "42"));
        var row = StatementImportRowMapper.ToImportRow(parsed, 1, StatementSourceProfile.ChaseBanking);

        Assert.Equal(25.99m, row.Amount);
        Assert.Equal(-25.99m, row.SourceSignedAmount);
        Assert.Null(row.Category);
        Assert.Equal("Shopping", row.SourceCategory);
    }
}
