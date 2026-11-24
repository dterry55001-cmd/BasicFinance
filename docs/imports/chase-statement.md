# Chase Bank CSV Import Specification

## Recognized header

```csv
Transaction Date,Post Date,Description,Category,Type,Amount,Memo
```

## Field mapping

| Chase column | Import field | Handling |
|---|---|---|
| Transaction Date | Transaction date | Date the activity occurred |
| Post Date | Posting date | Date the transaction posted |
| Description | Description | Preserve original bank description |
| Category | Bank category | Preserve separately from user-assigned category |
| Type | Bank transaction type | Preserve source value and map only known values |
| Amount | Amount | Preserve signed amount and derive debit/credit direction |
| Memo | Memo | Preserve original memo |

## Import rules

1. Recognize headers case-insensitively after removing punctuation and whitespace.
2. Require Transaction Date, Description, and Amount. Post Date, Category, Type, and Memo may be blank if omitted by a row.
3. Parse quoted CSV fields, escaped double quotes, US dates, and signed decimal amounts with optional currency symbols and grouping separators.
4. Preserve Transaction Date and Post Date independently. Transaction Date is the activity date; Post Date is used for posted-date reporting and reconciliation.
5. Preserve Category and Type as source fields. Do not overwrite a user's category with the bank's category.
6. Preserve the original signed Amount. Normalize debit/credit direction without losing the source value. Do not infer transaction meaning from sign alone when a recognized Type is available.
7. Stage rows for preview and validation. Do not post until the user confirms eligible rows.
8. Detect duplicates within the file and against prior imports for the selected account using a deterministic fingerprint of Transaction Date, Post Date, Description, Category, Type, signed Amount, and Memo. Use a stable source transaction ID if a future Chase export supplies one.
9. Preserve the original description and memo for auditability and later search.
10. Report invalid dates, malformed amounts, duplicate rows, and ambiguous transaction types in the preview.

## Acceptance criteria

- A CSV containing the seven Chase columns is recognized.
- Transaction Date and Post Date are independently visible and persisted.
- Description and Memo are both preserved without concatenating or truncating them.
- Bank Category and Type remain distinct from user-managed categorization.
- Signed Amount retains its debit/credit meaning and decimal precision.
- Duplicate detection is deterministic across repeat imports and within one file.
- No imported row changes account balances until explicitly confirmed and eligible for posting.
