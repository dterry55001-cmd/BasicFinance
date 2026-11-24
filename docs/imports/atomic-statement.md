# Atomic Bank CSV Import Specification

## Recognized header

```csv
"Transaction ID","Posting Date","Effective Date","Transaction Type","Posting Status","Amount","Check Number","Reference Number","Description","Transaction Category","Type","Balance","Memo","Extended Description"
```

## Field mapping

| Atomic column | Import field | Handling |
|---|---|---|
| Transaction ID | External transaction ID | Preserve for deterministic duplicate detection |
| Posting Date | Posted date | Primary ledger date |
| Effective Date | Effective date | Preserve separately from posting date |
| Transaction Type | Transaction classification | Preserve the bank's original label |
| Posting Status | Posting status | Preserve pending/posted status; do not silently post pending items |
| Amount | Amount | Parse signed decimal; derive debit/credit direction without losing the source amount |
| Check Number | Check reference | Preserve as text, including leading zeros |
| Reference Number | Bank reference | Preserve as text |
| Description | Description | Preserve original short description |
| Transaction Category | Bank category | Preserve separately from user-assigned category |
| Type | Bank transaction type | Preserve the source value separately from normalized ledger type |
| Balance | Statement balance | Preserve as the bank-reported running balance; use for reconciliation |
| Memo | Memo | Preserve |
| Extended Description | Extended description | Preserve |

## Import rules

1. Match headers case-insensitively after removing spaces and punctuation.
2. Require Transaction ID, Posting Date, Amount, and Description for the Atomic-specific profile. If the institution omits an identifier, use the generic CSV import flow instead of inventing one.
3. Parse quoted CSV fields and escaped double quotes.
4. Parse dates and decimal amounts using invariant culture, while allowing documented US date formats and currency symbols.
5. Preserve the sign of the source amount in staged data. Normalize the ledger amount and debit/credit type only after interpreting the signed amount and transaction type.
6. Keep Posting Date and Effective Date distinct. The posted date is the default ledger date; the effective date remains available for reporting and reconciliation.
7. Do not post rows whose Posting Status indicates pending, void, rejected, or reversed without explicit review. Preserve the original status label.
8. Use Transaction ID as the primary idempotency key within the same financial account. Also detect repeated IDs within the uploaded file.
9. Preserve Check Number, Reference Number, Transaction Category, Type, Balance, Memo, and Extended Description. Do not combine or discard these source fields.
10. Preview all rows and validation results before confirmation. Report invalid rows and duplicate rows individually.
11. Compare imported Balance values to calculated account balances during reconciliation; do not add the running balance to the transaction amount.

## Acceptance criteria

- An Atomic CSV with all 14 columns is recognized by the dedicated profile.
- Posting Date and Effective Date are stored independently.
- Signed Amount values retain debit/credit meaning.
- Pending rows are staged and clearly identified, not silently posted.
- Transaction ID duplicates are detected both within a file and against prior imports for the selected account.
- Check Number, Reference Number, both source classifications, running Balance, Memo, and Extended Description survive preview and posting.
- A transaction's imported running Balance is available for statement reconciliation and is never treated as another transaction.
