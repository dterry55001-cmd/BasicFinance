using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Services;

/// <summary>
/// Creates a durable, unposted import batch. This service never writes ledger transactions.
/// </summary>
public sealed class StatementImportPreparationService(
    ApplicationDbContext database,
    InstitutionStatementParser parser)
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

        var seenInFile = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parsed in parsedRows)
        {
            var row = StatementImportRowMapper.ToImportRow(parsed, 0, profile);
            if (!string.IsNullOrWhiteSpace(row.DuplicateFingerprint))
            {
                row.IsDuplicate = !seenInFile.Add(row.DuplicateFingerprint)
                    || await database.StatementImportRows.AnyAsync(
                        existing => existing.DuplicateFingerprint == row.DuplicateFingerprint,
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
