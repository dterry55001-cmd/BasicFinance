# Institution statement parser implementation

## Status

A dedicated institution parser has been added at
`src/BasicFinance.Web/Services/InstitutionStatementParser.cs` and registered in dependency injection.

The parser is now connected to the upload, durable preview, duplicate review, and confirmation workflow through `StatementImportPreparationService` and `ImportsController`. Institution metadata is persisted on import rows and copied to posted ledger transactions.

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

- The parser preserves each original CSV header/value pair in `SourceFields`; the masked source payload is persisted as JSON on import rows and posted transactions.
- Source signed amount is preserved separately from the positive ledger amount.
- Provider categories remain source metadata and are not silently assigned as the user's own categories.
- Import previews persist before confirmation and display separate dates, source details, and signed amounts.
- Provider transaction fingerprints are checked against imported rows and posted transactions; unconfirmed previews do not block a later retry.
- Signed amounts are parsed without applying absolute value.
- Decimal parsing is culture invariant and supports comma grouping, dollar signs and parenthesized negatives.
- Institution-specific dates are represented independently.
- Duplicate fingerprints are deterministic SHA-256 hashes scoped by profile and selected internal account key. Atomic's transaction ID is used as the preferred stable identity when present.
- Generic Date/Description/Amount headers are not auto-detected as Robinhood Bank because they do not uniquely identify that institution; users must select that profile explicitly.
- Atomic pending, void, rejected and reversed statuses are flagged for review.
- Robinhood Brokerage rows are held for review until a user explicitly maps each row to Income, Expense, Deposit, or Withdrawal; the parser does not guess transaction semantics. Atomic pending, void, rejected, and reversed rows remain held and cannot be manually released.
- Rows with a column count exceeding the header are rejected at row level.
- Unknown Robinhood transaction codes are retained as source data; this parser does not guess normalized investment action semantics.
- The parser validates the selected profile's required headers.

## Remaining work

1. Add reusable provider transaction-code mappings so repeated Robinhood codes do not need row-by-row review. Atomic held rows should be re-imported after the provider updates their posting status.
2. Add reconciliation screens using Atomic running balances and investment unit/price details.
3. Validate every layout against actual sanitized exports, including institution-specific date and decimal edge cases.
4. Add end-to-end tests for database migration, durable preview, duplicate detection, and confirmation.
5. Consider an import audit/export facility for traceability.

## Safety note

The account key passed to the parser should be the internal selected account identifier, not the full external bank account number. Do not persist full account numbers; retain only a masked suffix if the product requires it.

## Current integration status

- [x] Parser added and registered in dependency injection.
- [x] All six institution profiles supported in the upload workflow.
- [x] Institution-specific field extraction and lossless source payload persistence.
- [x] Separate source and ledger amounts, with institution categories kept distinct.
- [x] Safe review flags for Atomic statuses and unmapped Robinhood brokerage codes.
- [x] EF Core migrations added for institution-specific fields and duplicate fingerprints.
- [x] Automated parser tests added for all six layouts and key safety rules.
- [x] CI build and 8 automated tests pass, including migration execution against a clean in-memory SQLite database.
- [x] Add manual row-level classification and release for Robinhood Brokerage rows.
- [ ] Add reusable Robinhood transaction-code mappings and Atomic status refresh handling.
- [ ] Add reconciliation and audit features.

## Dependency security

- EF Core SQLite and Design packages are updated to 10.0.15.
- The transitive `System.Security.Cryptography.Pkcs` package is pinned to patched 9.0.21.
- CI now runs `dotnet list package --vulnerable --include-transitive`; the audit reported no vulnerable packages.
