# Rebuild BasicFinance migration history as one clean baseline.
# WARNING: This script deletes the Docker Compose volumes for this application.
# Existing financial records in the basicfinance-data volume will be permanently removed.

[CmdletBinding()]
param(
    [switch]$SkipDockerReset
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$project = Join-Path $repoRoot "src/BasicFinance.Web/BasicFinance.Web.csproj"
$migrations = Join-Path $repoRoot "src/BasicFinance.Web/Migrations"
$artifacts = Join-Path $repoRoot "artifacts"
$backup = Join-Path ([System.IO.Path]::GetTempPath()) ("BasicFinance-Migrations-" + (Get-Date -Format "yyyyMMdd-HHmmss"))

Push-Location $repoRoot
try {
    if (-not (Test-Path $project)) { throw "Project file not found: $project" }
    if (-not (Test-Path $migrations)) { New-Item -ItemType Directory -Path $migrations | Out-Null }

    dotnet ef --version
    if ($LASTEXITCODE -ne 0) {
        throw "Entity Framework CLI is unavailable. Install it with: dotnet tool install --global dotnet-ef"
    }

    New-Item -ItemType Directory -Path $backup | Out-Null
    $oldFiles = @(Get-ChildItem -Path $migrations -Filter "*.cs" -File)
    foreach ($file in $oldFiles) {
        Copy-Item -LiteralPath $file.FullName -Destination $backup
    }

    try {
        foreach ($file in $oldFiles) {
            Remove-Item -LiteralPath $file.FullName -Force
        }

        dotnet ef migrations add InitialCreate --project $project --startup-project $project --output-dir Migrations
        if ($LASTEXITCODE -ne 0) { throw "EF Core could not generate the InitialCreate migration." }

        $snapshot = Join-Path $migrations "ApplicationDbContextModelSnapshot.cs"
        $initialMigration = @(Get-ChildItem -Path $migrations -Filter "*_InitialCreate.cs" -File)
        if (-not (Test-Path $snapshot) -or $initialMigration.Count -ne 1) {
            throw "EF Core did not generate the expected migration and model snapshot."
        }

        New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
        dotnet ef migrations script 0 InitialCreate --project $project --startup-project $project --output (Join-Path $artifacts "basicfinance-initial.sql")
        if ($LASTEXITCODE -ne 0) { throw "EF Core could not generate the consolidated SQLite SQL script." }

        dotnet build $project --configuration Release
        if ($LASTEXITCODE -ne 0) { throw "BasicFinance build failed. Docker data has not been reset." }
    }
    catch {
        Get-ChildItem -Path $migrations -Filter "*.cs" -File | Remove-Item -Force
        foreach ($file in Get-ChildItem -Path $backup -Filter "*.cs" -File) {
            Copy-Item -LiteralPath $file.FullName -Destination $migrations -Force
        }
        throw
    }

    if (-not $SkipDockerReset) {
        docker compose down --volumes --remove-orphans
        if ($LASTEXITCODE -ne 0) { throw "Docker Compose could not stop/remove the old application volume." }

        docker compose up --build -d
        if ($LASTEXITCODE -ne 0) { throw "Docker Compose could not rebuild and start BasicFinance." }

        $healthy = $false
        for ($attempt = 1; $attempt -le 30; $attempt++) {
            try {
                $response = Invoke-WebRequest -Uri "http://localhost:7002/health" -TimeoutSec 3
                if ($response.StatusCode -eq 200) { $healthy = $true; break }
            }
            catch {
                Start-Sleep -Seconds 2
            }
        }

        if (-not $healthy) {
            docker logs --tail 100 basicfinance
            throw "BasicFinance did not pass its HTTP health check. Review the container logs above."
        }
    }

    Write-Host "Migration baseline generated: $migrations"
    Write-Host "Consolidated SQL script: $(Join-Path $artifacts 'basicfinance-initial.sql')"
    if ($SkipDockerReset) {
        Write-Host "Docker reset skipped. Run 'docker compose down --volumes' and 'docker compose up --build -d' when ready to reset the database."
    }
    else {
        Write-Host "BasicFinance is responding at http://localhost:7002"
    }
}
finally {
    Pop-Location
}
