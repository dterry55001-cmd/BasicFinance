# BasicFinance

A clean, modular personal finance application.

## Baseline

- ASP.NET Core MVC / .NET 10
- Entity Framework Core with SQLite
- Razor Views and responsive Bootstrap-based layout
- Docker-ready
- Application port: 7002

This repository intentionally starts with an empty application foundation. No legacy finance application code, imported data, or business modules are included.

## Run locally

```bash
dotnet restore
dotnet run --project src/BasicFinance.Web/BasicFinance.Web.csproj
```

The application uses SQLite at `data/basicfinance.db`. The database directory is created automatically. The initial dashboard is intentionally empty.

## Health

`/health` returns a simple application health response.
