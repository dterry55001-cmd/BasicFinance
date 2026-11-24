# BasicFinance

A clean, modular personal finance application.

## Current foundation

- ASP.NET Core MVC / .NET 10
- Entity Framework Core with SQLite
- Responsive Razor Views
- Docker-ready
- Application port: 7002
- EF Core migrations applied at startup

## Implemented modules

### Account management

- Checking, savings, cash, credit card, brokerage, and retirement accounts
- Institution, currency, opening balance, and optional opened date
- Only the last four account-number digits may be stored
- Active, inactive, and archived account statuses
- Account list, details, create, edit, and archive workflows
- Archived accounts remain stored and can be included in the account list

The application starts with no user financial records. No legacy finance application code or data is included.

### Transaction ledger

- Income, expense, deposit, withdrawal, and transfer entries
- Account-linked transactions with category, reference, date, and description
- Transfers link source and destination accounts and require matching currencies
- Create, edit, and delete transaction workflows
- Date, account, description, category, and reference filtering
- Account balances are calculated from the opening balance and posted ledger activity

### Statement imports

- CSV upload with a 10 MB size limit, institution profile selection, durable preview, and confirmation
- Six institution-specific layouts: UBS Brokerage, LifeSight Retirement, Atomic Banking, Robinhood Brokerage, Robinhood Bank, and Chase Banking
- Source headers and original source values are retained in the import record and copied to posted ledger transactions; UBS account numbers are masked to the last four digits
- Distinct activity, processing, posting, effective, and settlement dates are retained independently where supplied
- Signed source amounts are preserved separately from the positive ledger amount used with the normalized transaction type
- Security and retirement details include symbol, CUSIP, instrument, quantity, price, fund, contribution source, unit count, and unit value
- Banking details include provider transaction IDs, posting status, check/reference numbers, categories, memo, extended description, and running balance
- Duplicate fingerprints are scoped to the selected internal account; Atomic transaction IDs are preferred when available
- Atomic pending, void, rejected, and reversed rows are held for review
- Robinhood Brokerage rows are held for review until transaction-code mappings are configured; the importer does not guess investment action semantics
- Robinhood Bank's generic Date/Description/Amount layout requires explicit profile selection
- Import history records row-level validation, duplicate, and review status

Imports are persisted as unposted preview batches. Only valid, non-duplicate rows not flagged for review are posted when the user confirms. OFX/QFX, configurable Robinhood transaction-code mappings, and a workflow to resolve held-for-review rows remain future work.

## Run locally

    dotnet restore
    dotnet run --project src/BasicFinance.Web/BasicFinance.Web.csproj

Open http://localhost:7002. SQLite is stored at data/basicfinance.db.

## Docker

    docker compose up --build -d

The SQLite database is persisted in the basicfinance-data Docker volume.

## Health

/health provides the application health endpoint.
