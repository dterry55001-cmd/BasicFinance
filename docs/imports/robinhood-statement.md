# Robinhood Brokerage CSV Import Specification

## Recognized header

```csv
"Activity Date","Process Date","Settle Date","Instrument","Description","Trans Code","Quantity","Price","Amount"
```

## Field mapping

| Robinhood column | Import field | Handling |
|---|---|---|
| Activity Date | Activity date | Date the brokerage activity occurred |
| Process Date | Processing date | Date Robinhood processed the activity |
| Settle Date | Settlement date | Settlement date, kept distinct |
| Instrument | Instrument | Preserve security/fund/crypto identifier exactly as supplied |
| Description | Description | Preserve original text |
| Trans Code | Transaction code | Preserve original brokerage code; map to normalized type only with a known mapping |
| Quantity | Quantity | Signed decimal quantity, preserving direction |
| Price | Unit price | Decimal price per unit |
| Amount | Amount | Signed decimal cash impact |

## Import rules

1. Recognize headers case-insensitively after removing punctuation and whitespace.
2. Require Activity Date, Description, Trans Code, and Amount. Quantity, Price, Instrument, Process Date, and Settle Date may be blank where the source legitimately omits them.
3. Parse quoted CSV fields, escaped double quotes, invariant decimal values, and US date formats.
4. Preserve Activity Date, Process Date, and Settle Date independently. Activity Date is the default transaction date for ledger ordering; settlement date is used for settlement/reconciliation views.
5. Preserve the original Trans Code. A configurable mapping table should translate known codes to normalized ledger actions such as buy, sell, dividend, interest, fee, deposit, withdrawal, transfer, split, or option activity. Unknown codes must be staged as unmapped for review, not guessed.
6. Preserve signed Quantity and Amount. Do not use absolute values to erase direction. Do not assume every positive amount is income or every negative amount is an expense: transaction code determines meaning.
7. Preserve Instrument as supplied. Support optional later enrichment to symbol/CUSIP/security master records without making an external lookup a requirement for import.
8. Stage all rows for preview. Do not post unmapped, invalid, or duplicate rows automatically.
9. Detect duplicates using a stable fingerprint of the selected brokerage account plus Activity Date, Process Date, Settle Date, Instrument, Trans Code, Quantity, Price, Amount, and Description. Where a stable source transaction ID is available in a future statement format, prefer that ID.
10. Keep original source values alongside normalized ledger fields for audit and re-import.

## Acceptance criteria

- A CSV with the nine Robinhood columns is recognized.
- All three dates are stored independently and can be viewed in the import preview.
- Instrument, Description, and original Trans Code survive import.
- Quantity, Price, and Amount retain decimal precision and sign.
- Known transaction codes map through configurable mappings; unknown codes are visibly flagged for review.
- Duplicate detection is deterministic across repeated imports and within a single file.
- No row affects account balances until explicitly confirmed and eligible for posting.
- The import preview distinguishes activity date from settlement date.
