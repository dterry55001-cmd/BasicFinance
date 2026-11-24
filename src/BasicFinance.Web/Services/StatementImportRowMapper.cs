using System.Text.Json;
using BasicFinance.Web.Models;

namespace BasicFinance.Web.Services;

/// <summary>Maps parsed source rows into durable preview records without posting them.</summary>
public static class StatementImportRowMapper
{
    public static StatementImportRow ToImportRow(
        ParsedStatementRow parsed,
        int statementImportId,
        StatementSourceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        var signedAmount = parsed.SignedAmount ?? 0m;
        var investmentProfile = profile is StatementSourceProfile.UbsBrokerage
            or StatementSourceProfile.LifeSightRetirement
            or StatementSourceProfile.RobinhoodBrokerage;

        return new StatementImportRow
        {
            StatementImportId = statementImportId,
            RowNumber = parsed.RowNumber,
            TransactionDate = parsed.TransactionDate,
            PostingDate = parsed.PostingDate,
            EffectiveDate = parsed.EffectiveDate,
            ActivityDate = parsed.ActivityDate,
            ProcessDate = parsed.ProcessDate,
            SettlementDate = parsed.SettlementDate,
            Description = Limit(parsed.Description, 160) ?? "(missing description)",
            Amount = Math.Abs(signedAmount),
            SourceSignedAmount = parsed.SignedAmount,
            Type = investmentProfile
                ? (signedAmount < 0 ? TransactionType.Withdrawal : TransactionType.Deposit)
                : (signedAmount < 0 ? TransactionType.Expense : TransactionType.Income),
            // Institution categories remain source metadata; do not silently assign
            // a bank's category taxonomy as the user's own category.
            Category = null,
            Reference = Limit(parsed.SourceReference ?? parsed.ReferenceNumber, 100),
            Activity = profile == StatementSourceProfile.UbsBrokerage ? Limit(parsed.SourceCategory, 100) : null,
            SourceType = Limit(parsed.SourceType, 100),
            SourceSubType = Limit(parsed.SourceSubType, 100),
            ExtendedDescription = Limit(parsed.ExtendedDescription, 200),
            SecurityType = profile == StatementSourceProfile.UbsBrokerage ? Limit(parsed.SourceSubType, 40) : null,
            SourceStatus = Limit(parsed.SourceStatus, 100),
            SourceCategory = Limit(parsed.SourceCategory, 100),
            SourceReference = Limit(parsed.SourceReference, 120),
            Instrument = Limit(parsed.Instrument, 160),
            Memo = Limit(parsed.Memo, 200),
            CheckNumber = Limit(parsed.CheckNumber, 80),
            ReferenceNumber = Limit(parsed.ReferenceNumber, 120),
            TransactionCode = Limit(parsed.TransactionCode, 120),
            Symbol = Limit(parsed.Symbol, 40),
            Cusip = Limit(parsed.Cusip, 20),
            ExternalAccountNumberLast4 = Limit(parsed.ExternalAccountNumberLast4, 4),
            FriendlyAccountName = Limit(parsed.FriendlyAccountName, 100),
            Quantity = parsed.Quantity,
            Price = parsed.Price,
            FundName = Limit(parsed.FundName, 160),
            Source = Limit(parsed.Source, 120),
            UnitCount = parsed.UnitCount,
            UnitValue = parsed.UnitValue,
            RunningBalance = parsed.RunningBalance,
            DuplicateFingerprint = parsed.DuplicateFingerprint,
            RequiresReview = parsed.RequiresReview,
            ValidationError = Limit(parsed.ValidationError, 500),
            SourceFieldsJson = JsonSerializer.Serialize(parsed.SourceFields)
        };
    }

    private static string? Limit(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
