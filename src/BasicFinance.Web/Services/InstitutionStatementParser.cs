using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace BasicFinance.Web.Services;

/// <summary>Supported source layouts. Each profile is deliberately distinct.</summary>
public enum StatementSourceProfile
{
    Unknown,
    UbsBrokerage,
    LifeSightRetirement,
    AtomicBanking,
    RobinhoodBrokerage,
    RobinhoodBank,
    ChaseBanking
}

/// <summary>
/// A lossless source row plus commonly used normalized values. SourceFields retains
/// the original header/value pairs so later mapping changes do not destroy source data.
/// </summary>
public sealed class ParsedStatementRow
{
    public int RowNumber { get; init; }
    public Dictionary<string, string> SourceFields { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTime? TransactionDate { get; set; }
    public DateTime? PostingDate { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ActivityDate { get; set; }
    public DateTime? ProcessDate { get; set; }
    public DateTime? SettlementDate { get; set; }
    public string? Description { get; set; }
    public string? SourceType { get; set; }
    public string? SourceSubType { get; set; }
    public string? ExtendedDescription { get; set; }
    public string? SourceStatus { get; set; }
    public string? SourceCategory { get; set; }
    public string? SourceReference { get; set; }
    public string? ExternalAccountNumberLast4 { get; set; }
    public string? FriendlyAccountName { get; set; }
    public string? Instrument { get; set; }
    public string? Symbol { get; set; }
    public string? Cusip { get; set; }
    public string? FundName { get; set; }
    public string? Source { get; set; }
    public string? Memo { get; set; }
    public string? CheckNumber { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? TransactionCode { get; set; }
    public string? PostingStatus { get; set; }
    public decimal? SignedAmount { get; set; }
    public decimal? RunningBalance { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? Price { get; set; }
    public decimal? UnitCount { get; set; }
    public decimal? UnitValue { get; set; }
    public string? ValidationError { get; set; }
    public bool RequiresReview { get; set; }
    public bool CanPost => ValidationError is null && !RequiresReview;
    public string? DuplicateFingerprint { get; set; }
}

public sealed class InstitutionStatementParser
{
    private static readonly string[] UbsHeaders =
        ["accountnumber", "date", "activity", "description", "symbol", "cusip", "type", "quantity", "price", "amount", "friendlyaccountname"];
    private static readonly string[] LifeSightHeaders =
        ["transactiontype", "date", "source", "fundname", "unitcount", "unitvalue", "transactionamount"];
    private static readonly string[] AtomicHeaders =
        ["transactionid", "postingdate", "effectivedate", "transactiontype", "postingstatus", "amount", "checknumber", "referencenumber", "description", "transactioncategory", "type", "balance", "memo", "extendeddescription"];
    private static readonly string[] RobinhoodBrokerageHeaders =
        ["activitydate", "processdate", "settledate", "instrument", "description", "transcode", "quantity", "price", "amount"];
    private static readonly string[] RobinhoodBankHeaders = ["date", "description", "amount"];
    private static readonly string[] ChaseHeaders =
        ["transactiondate", "postdate", "description", "category", "type", "amount", "memo"];

    public StatementSourceProfile DetectProfile(string csv)
    {
        var records = ReadCsv(csv);
        if (records.Count == 0) return StatementSourceProfile.Unknown;
        var headers = records[0].Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Prefer the most specific header sets before generic layouts.
        if (ContainsAll(headers, UbsHeaders)) return StatementSourceProfile.UbsBrokerage;
        if (ContainsAll(headers, LifeSightHeaders)) return StatementSourceProfile.LifeSightRetirement;
        if (ContainsAll(headers, AtomicHeaders)) return StatementSourceProfile.AtomicBanking;
        if (ContainsAll(headers, RobinhoodBrokerageHeaders)) return StatementSourceProfile.RobinhoodBrokerage;
        if (ContainsAll(headers, ChaseHeaders)) return StatementSourceProfile.ChaseBanking;
        // Date/Description/Amount is generic and cannot identify Robinhood uniquely.
        // Require the user to explicitly select Robinhood Bank for this layout.
        return StatementSourceProfile.Unknown;
    }

    public IReadOnlyList<ParsedStatementRow> Parse(string csv, StatementSourceProfile profile, string accountKey)
    {
        if (profile == StatementSourceProfile.Unknown)
            throw new InvalidDataException("The statement format was not recognized. Select a supported institution profile.");
        if (string.IsNullOrWhiteSpace(accountKey))
            throw new ArgumentException("A selected internal account key is required.", nameof(accountKey));
        var records = ReadCsv(csv);
        if (records.Count < 2) throw new InvalidDataException("The CSV must contain a header and at least one data row.");

        var headers = records[0].Select(h => h.Trim()).ToArray();
        var normalized = headers.Select(Normalize).ToArray();
        ValidateProfileHeaders(profile, normalized);
        var rows = new List<ParsedStatementRow>();

        for (var i = 1; i < records.Count; i++)
        {
            var values = records[i];
            if (values.All(string.IsNullOrWhiteSpace)) continue;
            var row = new ParsedStatementRow { RowNumber = i + 1 };
            if (values.Count > headers.Length)
            {
                row.ValidationError = $"This row has {values.Count} values but the header has {headers.Length} columns.";
                rows.Add(row);
                continue;
            }
            for (var h = 0; h < headers.Length; h++)
            {
                var sourceValue = h < values.Count ? values[h] : string.Empty;
                // Never retain a full brokerage account number in the source payload.
                if (profile == StatementSourceProfile.UbsBrokerage && Normalize(headers[h]) == "accountnumber")
                {
                    var digits = new string(sourceValue.Where(char.IsDigit).ToArray());
                    sourceValue = digits.Length >= 4 ? $"****{digits[^4..]}" : string.Empty;
                }
                row.SourceFields[headers[h]] = sourceValue;
            }

            string Get(string key) {
                var index = Array.IndexOf(normalized, Normalize(key));
                return index >= 0 && index < values.Count ? values[index].Trim() : string.Empty;
            }
            DateTime? Date(string key) => TryDate(Get(key));
            decimal? Number(string key) => TryDecimal(Get(key));

            try
            {
                switch (profile)
                {
                    case StatementSourceProfile.UbsBrokerage:
                        row.TransactionDate = Date("Date");
                        var accountDigits = new string(Get("Account Number").Where(char.IsDigit).ToArray());
                        row.ExternalAccountNumberLast4 = accountDigits.Length >= 4 ? accountDigits[^4..] : null;
                        row.FriendlyAccountName = Get("Friendly Account Name");
                        row.Description = FirstNonEmpty(Get("Description"), Get("Activity"));
                        row.SourceSubType = Get("Type");
                        row.SourceType = Get("Type");
                        row.SourceCategory = Get("Activity");
                        row.Symbol = Get("Symbol");
                        row.Cusip = Get("Cusip");
                        row.Quantity = Number("Quantity");
                        row.Price = Number("Price");
                        row.SignedAmount = Number("Amount");
                        break;
                    case StatementSourceProfile.LifeSightRetirement:
                        row.TransactionDate = Date("Date");
                        row.Description = Get("Fund Name");
                        row.SourceType = Get("Transaction Type");
                        row.Source = Get("Source");
                        row.FundName = Get("Fund Name");
                        row.UnitCount = Number("Unit Count");
                        row.UnitValue = Number("Unit Value");
                        row.SignedAmount = Number("Transaction Amount");
                        break;
                    case StatementSourceProfile.AtomicBanking:
                        row.SourceReference = Get("Transaction ID");
                        row.TransactionDate = Date("Posting Date");
                        row.PostingDate = Date("Posting Date");
                        row.EffectiveDate = Date("Effective Date");
                        row.SourceType = Get("Transaction Type");
                        row.SourceSubType = Get("Type");
                        row.SourceStatus = Get("Posting Status");
                        row.PostingStatus = Get("Posting Status");
                        row.SignedAmount = Number("Amount");
                        row.CheckNumber = Get("Check Number");
                        row.ReferenceNumber = Get("Reference Number");
                        row.Description = Get("Description");
                        row.SourceCategory = Get("Transaction Category");
                        row.Memo = Get("Memo");
                        row.ExtendedDescription = Get("Extended Description");
                        row.RunningBalance = Number("Balance");
                        break;
                    case StatementSourceProfile.RobinhoodBrokerage:
                        row.ActivityDate = Date("Activity Date");
                        row.ProcessDate = Date("Process Date");
                        row.SettlementDate = Date("Settle Date");
                        row.TransactionDate = row.ActivityDate;
                        row.Instrument = Get("Instrument");
                        row.Description = Get("Description");
                        row.TransactionCode = Get("Trans Code");
                        row.SourceType = row.TransactionCode;
                        row.Quantity = Number("Quantity");
                        row.Price = Number("Price");
                        row.SignedAmount = Number("Amount");
                        break;
                    case StatementSourceProfile.RobinhoodBank:
                        row.TransactionDate = Date("Date");
                        row.Description = Get("Description");
                        row.SignedAmount = Number("Amount");
                        break;
                    case StatementSourceProfile.ChaseBanking:
                        row.TransactionDate = Date("Transaction Date");
                        row.PostingDate = Date("Post Date");
                        row.Description = Get("Description");
                        row.SourceCategory = Get("Category");
                        row.SourceType = Get("Type");
                        row.SignedAmount = Number("Amount");
                        row.Memo = Get("Memo");
                        break;
                }

                if (row.TransactionDate is null)
                    throw new FormatException("A valid transaction or posting date is required.");
                if (row.SignedAmount is null)
                    throw new FormatException("A valid signed amount is required.");
                if (string.IsNullOrWhiteSpace(row.Description))
                    throw new FormatException("A transaction description is required.");

                if (profile == StatementSourceProfile.AtomicBanking &&
                    IsNonPostableAtomicStatus(row.PostingStatus))
                    row.RequiresReview = true;

                // No Robinhood transaction-code mapping is configured yet. Keep these rows
                // staged until a reviewed mapping is supplied rather than guessing semantics.
                if (profile == StatementSourceProfile.RobinhoodBrokerage)
                    row.RequiresReview = true;

                row.DuplicateFingerprint = BuildFingerprint(profile, accountKey, row);
            }
            catch (FormatException ex)
            {
                row.ValidationError = ex.Message;
            }

            rows.Add(row);
        }

        if (rows.Count == 0) throw new InvalidDataException("No transaction rows were found.");
        return rows;
    }

    public static string BuildFingerprint(StatementSourceProfile profile, string accountKey, ParsedStatementRow row)
    {
        // A provider transaction ID is the strongest identity when present. Keep it
        // independent of mutable descriptions/status/amount fields for idempotency.
        if (!string.IsNullOrWhiteSpace(row.SourceReference))
        {
            var sourceIdentity = string.Join("|", profile.ToString(), accountKey.Trim(), row.SourceReference.Trim());
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceIdentity)));
        }

        // Stable, culture-invariant fingerprint. Account scope prevents cross-account collisions.
        var parts = new[]
        {
            profile.ToString(), accountKey.Trim(),
            row.TransactionDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            row.PostingDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            row.ActivityDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            row.ProcessDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            row.SettlementDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            row.Instrument, row.Symbol, row.TransactionCode, row.SourceType,
            row.Quantity?.ToString(CultureInfo.InvariantCulture),
            row.Price?.ToString(CultureInfo.InvariantCulture),
            row.SignedAmount?.ToString(CultureInfo.InvariantCulture),
            row.Description, row.SourceCategory, row.Memo
        };
        var canonical = string.Join("|", parts.Select(p => (p ?? string.Empty).Trim()));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static void ValidateProfileHeaders(StatementSourceProfile profile, string[] headers)
    {
        var required = profile switch
        {
            StatementSourceProfile.UbsBrokerage => UbsHeaders,
            StatementSourceProfile.LifeSightRetirement => LifeSightHeaders,
            StatementSourceProfile.AtomicBanking => AtomicHeaders,
            StatementSourceProfile.RobinhoodBrokerage => RobinhoodBrokerageHeaders,
            StatementSourceProfile.RobinhoodBank => RobinhoodBankHeaders,
            StatementSourceProfile.ChaseBanking => ChaseHeaders,
            _ => []
        };
        var set = headers.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = required.Where(h => !set.Contains(h)).ToArray();
        if (missing.Length > 0)
            throw new InvalidDataException($"The selected {profile} profile is missing required columns: {string.Join(", ", missing)}.");
    }

    private static bool IsNonPostableAtomicStatus(string? status)
    {
        var normalized = Normalize(status ?? string.Empty);
        return normalized is "pending" or "void" or "rejected" or "reversed";
    }

    private static bool ContainsAll(HashSet<string> headers, string[] required) => required.All(headers.Contains);
    private static string Normalize(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;

    private static DateTime? TryDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed)
            ? parsed.Date : null;
    }

    private static decimal? TryDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Trim().Replace("$", string.Empty).Replace(",", string.Empty);
        if (cleaned.StartsWith("(") && cleaned.EndsWith(")")) cleaned = "-" + cleaned[1..^1];
        return decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed)
            ? parsed : null;
    }

    private static List<List<string>> ReadCsv(string input)
    {
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < input.Length; i++)
        {
            var ch = input[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < input.Length && input[i + 1] == '"') { field.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (ch == ',' && !quoted) { record.Add(field.ToString().Trim()); field.Clear(); }
            else if ((ch == '\r' || ch == '\n') && !quoted)
            {
                if (ch == '\r' && i + 1 < input.Length && input[i + 1] == '\n') i++;
                record.Add(field.ToString().Trim()); field.Clear();
                if (record.Any(v => v.Length > 0)) records.Add(record);
                record = [];
            }
            else field.Append(ch);
        }
        if (quoted) throw new InvalidDataException("The CSV contains an unterminated quoted field.");
        record.Add(field.ToString().Trim());
        if (record.Any(v => v.Length > 0)) records.Add(record);
        return records;
    }
}
