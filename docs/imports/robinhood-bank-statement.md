# Robinhood Bank/Cash CSV Import Specification

This is a separate import profile from the detailed Robinhood brokerage activity statement.

## Recognized header

```csv
Date,Description,Amount
```

## Field mapping

| Robinhood bank column | Import field | Handling |
|---|---|---|
| Date | Transaction date | Date the bank/cash activity occurred |
| Description | Description | Preserve original text |
| Amount | Amount | Preserve signed decimal value and debit/credit direction |

## Import rules

1. Identify this as the Robinhood Bank/Cash profile by its three-column header. Do not confuse it with the Robinhood Brokerage profile containing Activity Date, Process Date, Settle Date, Instrument, Trans Code, Quantity, Price, and Amount.
2. Require Date, Description, and Amount.
3. Parse quoted CSV fields, escaped double quotes, US dates, and signed decimal amounts with optional currency symbols and grouping separators.
4. Preserve the original signed amount. Normalize the ledger transaction direction without losing the source amount.
5. Use the selected bank or cash account; do not create an account implicitly from the file.
6. Stage all rows for preview and validation before posting.
7. Detect duplicates within the uploaded file and against previous imports for the selected account using a deterministic fingerprint of Date, Description, and signed Amount. A source transaction ID, if added to a future export, should take precedence.
8. Report invalid dates, malformed amounts, and duplicate rows individually.
9. Do not post a row or change an account balance until the user confirms eligible rows.

## Acceptance criteria

- The three-column Date, Description, Amount format is recognized as Robinhood Bank/Cash.
- It remains distinct from Robinhood Brokerage imports.
- Date, full description, and signed amount are preserved.
- Duplicate detection is deterministic across repeated imports and within a file.
- Preview and confirmation are required before ledger posting.
