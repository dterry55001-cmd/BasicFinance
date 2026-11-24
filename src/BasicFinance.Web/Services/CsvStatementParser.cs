using System.Globalization;
using BasicFinance.Web.Models;

namespace BasicFinance.Web.Services;

public sealed class CsvStatementParser
{
    public List<StatementImportRow> Parse(string csv)
    {
        var lines = ReadRecords(csv);
        if (lines.Count < 2) throw new InvalidDataException("The CSV must include a header and at least one transaction row.");

        var headers = lines[0].Select((value, index) => (Key: Normalize(value), Index: index))
            .GroupBy(item => item.Key).ToDictionary(group => group.Key, group => group.First().Index);
        var dateIndex = Find(headers, "date", "transactiondate", "posteddate");
        var descriptionIndex = Find(headers, "description", "payee", "name", "memo", "source");
        var amountIndex = Find(headers, "amount", "transactionamount", "value");
        var typeIndex = Find(headers, "type", "transactiontype");
        var categoryIndex = Find(headers, "category");
        var referenceIndex = Find(headers, "reference", "id", "transactionid");
        var sourceIndex = Find(headers, "source");
        var fundIndex = Find(headers, "fundname", "fund");
        var unitsIndex = Find(headers, "unitcount", "units", "shares");
        var unitValueIndex = Find(headers, "unitvalue", "priceperunit");
        var accountNumberIndex = Find(headers, "accountnumber", "account");
        var activityIndex = Find(headers, "activity", "action");
        var symbolIndex = Find(headers, "symbol", "ticker");
        var cusipIndex = Find(headers, "cusip");
        var securityTypeIndex = Find(headers, "type", "securitytype", "assettype");
        var quantityIndex = Find(headers, "quantity", "shares", "units");
        var priceIndex = Find(headers, "price", "unitprice");
        var friendlyAccountIndex = Find(headers, "friendlyaccountname", "accountname");

        if (dateIndex < 0 || descriptionIndex < 0 || amountIndex < 0)
            throw new InvalidDataException("Required columns were not found. Include Date, Description, and Amount headers.");

        var rows = new List<StatementImportRow>();
        for (var i = 1; i < lines.Count; i++)
        {
            var fields = lines[i];
            if (fields.All(string.IsNullOrWhiteSpace)) continue;
            var row = new StatementImportRow { RowNumber = i + 1 };
            try
            {
                var dateText = Get(fields, dateIndex);
                if (!DateTime.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date))
                    throw new FormatException("Invalid transaction date.");
                row.TransactionDate = date.Date;

                row.Source = sourceIndex >= 0 ? NullIfEmpty(Get(fields, sourceIndex), 120) : null;
                var accountNumber = accountNumberIndex >= 0 ? Get(fields, accountNumberIndex).Trim() : "";
                row.ExternalAccountNumberLast4 = accountNumber.Length >= 4 ? accountNumber[^4..] : NullIfEmpty(accountNumber, 4);
                row.FriendlyAccountName = friendlyAccountIndex >= 0 ? NullIfEmpty(Get(fields, friendlyAccountIndex), 100) : null;
                row.Activity = activityIndex >= 0 ? NullIfEmpty(Get(fields, activityIndex), 100) : null;
                row.Symbol = symbolIndex >= 0 ? NullIfEmpty(Get(fields, symbolIndex), 40) : null;
                row.Cusip = cusipIndex >= 0 ? NullIfEmpty(Get(fields, cusipIndex), 20) : null;
                row.SecurityType = securityTypeIndex >= 0 ? NullIfEmpty(Get(fields, securityTypeIndex), 40) : null;
                if (quantityIndex >= 0 && !string.IsNullOrWhiteSpace(Get(fields, quantityIndex)))
                {
                    if (!decimal.TryParse(Get(fields, quantityIndex), NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity))
                        throw new FormatException("Invalid security quantity.");
                    row.Quantity = quantity;
                }
                if (priceIndex >= 0 && !string.IsNullOrWhiteSpace(Get(fields, priceIndex)))
                {
                    if (!decimal.TryParse(Get(fields, priceIndex).Replace("$", "").Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out var price) || price < 0)
                        throw new FormatException("Invalid security price.");
                    row.Price = price;
                }
                row.FundName = fundIndex >= 0 ? NullIfEmpty(Get(fields, fundIndex), 160) : null;
                row.Description = Get(fields, descriptionIndex).Trim();
                if (!string.IsNullOrWhiteSpace(row.FundName) && !row.Description.Contains(row.FundName, StringComparison.OrdinalIgnoreCase))
                    row.Description = $"{row.Description} — {row.FundName}";
                if (string.IsNullOrWhiteSpace(row.Description) || row.Description.Length > 160)
                    throw new FormatException("Description is required and must be 160 characters or fewer.");

                if (unitsIndex >= 0 && !string.IsNullOrWhiteSpace(Get(fields, unitsIndex)))
                {
                    if (!decimal.TryParse(Get(fields, unitsIndex), NumberStyles.Number, CultureInfo.InvariantCulture, out var units) || units < 0)
                        throw new FormatException("Invalid unit count.");
                    row.UnitCount = units;
                }
                if (unitValueIndex >= 0 && !string.IsNullOrWhiteSpace(Get(fields, unitValueIndex)))
                {
                    if (!decimal.TryParse(Get(fields, unitValueIndex), NumberStyles.Number, CultureInfo.InvariantCulture, out var unitValue) || unitValue < 0)
                        throw new FormatException("Invalid unit value.");
                    row.UnitValue = unitValue;
                }

                var amountText = Get(fields, amountIndex).Trim().Replace("$", "").Replace(",", "");
                if (!decimal.TryParse(amountText, NumberStyles.Number | NumberStyles.AllowParentheses, CultureInfo.InvariantCulture, out var amount))
                    throw new FormatException("Invalid transaction amount.");
                row.Amount = Math.Abs(amount);
                if (row.Amount <= 0) throw new FormatException("Amount must be greater than zero.");

                var typeText = typeIndex >= 0 ? Get(fields, typeIndex).Trim() : "";
                if (!string.IsNullOrWhiteSpace(row.Activity))
                    row.Type = MapActivity(row.Activity, amount);
                else if (string.IsNullOrWhiteSpace(typeText))
                    row.Type = amount < 0 ? TransactionType.Expense : TransactionType.Income;
                else if (!Enum.TryParse<TransactionType>(typeText, true, out var parsedType) || parsedType == TransactionType.Transfer)
                    row.Type = amount < 0 ? TransactionType.Expense : TransactionType.Income;
                else row.Type = parsedType;



                row.Category = categoryIndex >= 0 ? NullIfEmpty(Get(fields, categoryIndex), 80) : null;
                row.Reference = referenceIndex >= 0 ? NullIfEmpty(Get(fields, referenceIndex), 100) : null;
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException)
            {
                row.ValidationError = ex.Message;
            }
            rows.Add(row);
        }

        if (rows.Count == 0) throw new InvalidDataException("No transaction rows were found in the CSV.");
        return rows;
    }

    private static List<List<string>> ReadRecords(string input)
    {
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new System.Text.StringBuilder();
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
            else if ((ch == '\n' || ch == '\r') && !quoted)
            {
                if (ch == '\r' && i + 1 < input.Length && input[i + 1] == '\n') i++;
                record.Add(field.ToString().Trim()); field.Clear();
                if (record.Any(value => value.Length > 0)) records.Add(record);
                record = [];
            }
            else field.Append(ch);
        }
        if (quoted) throw new InvalidDataException("The CSV contains an unterminated quoted field.");
        record.Add(field.ToString().Trim());
        if (record.Any(value => value.Length > 0)) records.Add(record);
        return records;
    }

    private static TransactionType MapActivity(string activity, decimal signedAmount)
    {
        var value = Normalize(activity);
        if (value.Contains("buy") || value.Contains("purchase") || value.Contains("reinvest"))
            return TransactionType.Expense;
        if (value.Contains("sell") || value.Contains("dividend") || value.Contains("interest") || value.Contains("credit"))
            return TransactionType.Income;
        if (value.Contains("contribution") || value.Contains("deposit"))
            return TransactionType.Deposit;
        if (value.Contains("withdrawal") || value.Contains("debit") || value.Contains("fee") || value.Contains("tax"))
            return TransactionType.Withdrawal;
        return signedAmount < 0 ? TransactionType.Expense : TransactionType.Income;
    }

    private static int Find(Dictionary<string, int> headers, params string[] names)
    {
        foreach (var name in names) if (headers.TryGetValue(name, out var index)) return index;
        return -1;
    }
    private static string Normalize(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static string Get(List<string> fields, int index) => index < fields.Count ? fields[index] : "";
    private static string? NullIfEmpty(string value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];
}
