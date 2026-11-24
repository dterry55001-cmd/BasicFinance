using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Services;

/// <summary>
/// Creates a durable, unposted import batch. This service never writes ledger transactions.
/// </summary>
public sealed class StatementImportPreparationService(
    ApplicationDbContext database,
    InstitutionStatementParser parser, TransactionRuleService ruleService)
{
    public async Task<StatementImport> PrepareAsync(
        int accountId,
        string fileName,
        string csv,
        StatementSourceProfile selectedProfile = StatementSourceProfile.Unknown,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("A statement filename is required.", nameof(fileName));
        if (string.IsNullOrWhiteSpace(csv))
            throw new InvalidDataException("The uploaded statement is empty.");

        var account = await database.Accounts
            .SingleOrDefaultAsync(a => a.Id == accountId, cancellationToken)
            ?? throw new InvalidOperationException("The selected account does not exist.");

        var profile = selectedProfile == StatementSourceProfile.Unknown
            ? parser.DetectProfile(csv)
            : selectedProfile;
        if (profile == StatementSourceProfile.Unknown)
            throw new InvalidDataException("The statement format could not be identified. Select the institution profile manually.");

        var parsedRows = parser.Parse(csv, profile, account.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var import = new StatementImport
        {
            FileName = Path.GetFileName(fileName),
            AccountId = account.Id,
            AccountKey = account.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Profile = ToModelProfile(profile),
            Status = StatementImportStatus.Preview,
            CreatedUtc = DateTime.UtcNow
        };

        var modelProfile = ToModelProfile(profile);
        var mappings = await database.TransactionCategoryMappings.AsNoTracking()
            .Include(mapping => mapping.TransactionCategory)
            .Where(mapping => mapping.Profile == modelProfile && mapping.IsActive && mapping.TransactionCategory.IsActive)
            .ToListAsync(cancellationToken);
        var mappingLookup = mappings.ToDictionary(
            mapping => mapping.NormalizedSourceCategory,
            mapping => mapping.TransactionCategory,
            StringComparer.OrdinalIgnoreCase);

        var seenInFile = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parsed in parsedRows)
        {
            var row = StatementImportRowMapper.ToImportRow(parsed, 0, profile);
            var sourceCategory = NormalizeCategory(row.SourceCategory);
            if (sourceCategory.Length > 0 && mappingLookup.TryGetValue(sourceCategory, out var mappedCategory)
                && IsCompatible(mappedCategory.Type, row.Type))
                row.CategoryId = mappedCategory.Id;

            if (!row.CategoryId.HasValue)
            {
                var suggestion = await ruleService.SuggestAsync(row.Description, account.Id, row.Type, cancellationToken);
                if (suggestion is not null)
                {
                    row.CategoryId = suggestion.Id;
                    row.CategorySuggestedByRule = true;
                }
            }
            if (!string.IsNullOrWhiteSpace(row.DuplicateFingerprint))
            {
                row.IsDuplicate = !seenInFile.Add(row.DuplicateFingerprint)
                    || await database.StatementImportRows.AnyAsync(
                        existing => existing.DuplicateFingerprint == row.DuplicateFingerprint && existing.IsImported,
                        cancellationToken)
                    || await database.Transactions.AnyAsync(
                        existing => existing.ImportFingerprint == row.DuplicateFingerprint,
                        cancellationToken);
            }
            import.Rows.Add(row);
        }

        var firstUbsRow = parsedRows.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.ExternalAccountNumberLast4));
        import.ExternalAccountNumberLast4 = firstUbsRow?.ExternalAccountNumberLast4;
        import.FriendlyAccountName = parsedRows.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.FriendlyAccountName))?.FriendlyAccountName;

        database.StatementImports.Add(import);
        await database.SaveChangesAsync(cancellationToken);
        return import;
    }

    public static string NormalizeCategory(string? value) => string.Join(" ", (value ?? string.Empty)
        .Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    public static bool IsCompatible(TransactionCategoryType categoryType, TransactionType transactionType) => categoryType switch
    {
        TransactionCategoryType.Income => transactionType is TransactionType.Income or TransactionType.Deposit,
        TransactionCategoryType.Expense => transactionType is TransactionType.Expense or TransactionType.Withdrawal,
        TransactionCategoryType.Transfer => transactionType == TransactionType.Transfer,
        _ => false
    };

    private static StatementImportProfile ToModelProfile(StatementSourceProfile profile) => profile switch
    {
        StatementSourceProfile.UbsBrokerage => StatementImportProfile.UbsBrokerage,
        StatementSourceProfile.LifeSightRetirement => StatementImportProfile.LifeSightRetirement,
        StatementSourceProfile.AtomicBanking => StatementImportProfile.AtomicBanking,
        StatementSourceProfile.RobinhoodBrokerage => StatementImportProfile.RobinhoodBrokerage,
        StatementSourceProfile.RobinhoodBank => StatementImportProfile.RobinhoodBank,
        StatementSourceProfile.ChaseBanking => StatementImportProfile.ChaseBanking,
        _ => StatementImportProfile.Unknown
    };
}
