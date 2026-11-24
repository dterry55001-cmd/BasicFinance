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

- CSV upload with a 10 MB size limit
- Preview and validation before posting to the ledger
- Required Date, Description/Source, and Amount columns
- Optional transaction type, category, and reference columns
- Common statement aliases for contributions, employer matches, purchases, payments, dividends, and interest
- Duplicate detection against existing transactions and within each uploaded file
- Import history with row-level validation status
- LifeSight-style contribution metadata: source, fund name, unit count, and unit value
- Confirmed imports preserve fund and unit details on the ledger transaction

Imports remain staged until the user confirms them. CSV import currently supports a configurable common header set; OFX/QFX and bank-specific mapping templates are future work.

## Run locally

    dotnet restore
    dotnet run --project src/BasicFinance.Web/BasicFinance.Web.csproj

Open http://localhost:7002. SQLite is stored at data/basicfinance.db.

## Docker

    docker compose up --build -d

The SQLite database is persisted in the basicfinance-data Docker volume.

## Health

/health provides the application health endpoint.
