# Institution statement parser implementation

## Status

A dedicated institution parser has been added at
`src/BasicFinance.Web/Services/InstitutionStatementParser.cs` and registered in dependency injection.

It remains a parsing foundation, not a complete end-to-end import workflow. It is not yet wired into the upload controller, persistence, preview UI, or posting confirmation.

## Supported profiles and retained values

| Profile | Distinguishing source fields |
|---|---|
| UBS Brokerage | Activity, symbol, CUSIP, source Type, quantity, price, amount, account number and friendly account name are retained in the original source field dictionary |
| LifeSight Retirement | Transaction type, contribution source, fund name, unit count, unit value and transaction amount |
| Atomic Banking | Transaction ID, posting/effective dates, transaction type, posting status, amount, check/reference numbers, category, type, balance and memo fields |
| Robinhood Brokerage | Activity/process/settlement dates, instrument, description, transaction code, quantity, price and amount |
| Robinhood Bank | Date, description and amount |
| Chase Banking | Transaction date, post date, description, category, type, amount and memo |

## Important behaviors

- The parser preserves each original CSV header/value pair in `SourceFields`.
- Signed amounts are parsed without applying absolute value.
- Decimal parsing is culture invariant and supports comma grouping, dollar signs and parenthesized negatives.
- Institution-specific dates are represented independently.
- Duplicate fingerprints are deterministic SHA-256 hashes scoped by profile and selected internal account key. Atomic's transaction ID is used as the preferred stable identity when present.
- Generic Date/Description/Amount headers are not auto-detected as Robinhood Bank because they do not uniquely identify that institution; users must select that profile explicitly.
- Atomic pending, void, rejected and reversed statuses are flagged for review.
- Robinhood Brokerage rows are held for review until a transaction-code mapping is configured; the parser does not guess transaction semantics.
- Rows with a column count exceeding the header are rejected at row level.
- Unknown Robinhood transaction codes are retained as source data; this parser does not guess normalized investment action semantics.
- The parser validates the selected profile's required headers.

## Known follow-up work

1. Add unit tests using real, sanitized statement samples for all six profiles.
2. Add explicit schema fields to persisted import rows and transactions, or a durable JSON source payload with schema/versioning.
3. Wire profile detection/selection and parser into the existing upload and preview workflow.
4. Use Atomic transaction ID as the preferred duplicate key; use fingerprints for other sources.
5. Add posting eligibility rules for Atomic pending/void/rejected/reversed statuses.
6. Add configured Robinhood transaction-code mappings and flag unknown codes for review.
7. Implement import confirmation, idempotent posting, reconciliation and audit history.
8. Validate date formats and profile-specific edge cases against actual exported files before enabling production posting.

## Safety note

The account key passed to the parser should be the internal selected account identifier, not the full external bank account number. Do not persist full account numbers; retain only a masked suffix if the product requires it.

## Current integration status

- [x] Parser added and registered in dependency injection.
- [x] Institution-specific field extraction and source payload retention.
- [x] Safe review flags for Atomic statuses and unmapped Robinhood brokerage codes.
- [ ] Persist parsed source payload and institution-specific fields.
- [ ] Wire into the existing upload/preview/confirm controller and views.
- [ ] Add migrations, idempotent posting, and import audit.
- [ ] Add automated tests and verify a .NET 10 build.
