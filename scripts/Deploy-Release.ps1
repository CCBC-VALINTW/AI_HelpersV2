<#
.SYNOPSIS
    Builds and applies a staged release of AI Helpers V2: generates a reviewable DB migration
    script, applies it, publishes the app, and (optionally) deploys it to an IIS site.

.DESCRIPTION
    Formalises the process in docs/DEPLOYMENT.md's "Redeploying / updating" section into one
    script with an explicit confirmation gate before anything destructive happens. Run it from
    a machine with the .NET SDK, this repo checked out, and network access to the target SQL
    Server (Windows/Entra integrated auth - no password needed, same as every other DB
    connection this project uses).

    Three phases:
      1. Generate an idempotent EF Core migration script, open it for review, and pause.
      2. Apply that script to the target database via sqlcmd (only if you confirm).
      3. Publish the app to an out-of-tree folder, verify Project Info/Design brief/Tools
         are not in the output, and - if -SitePath is given - copy it to the IIS site and
         recycle the app pool.

    Deliberately does NOT touch your local AI_Helpers_DEV connection string or working DB -
    this only ever targets -Server/-Database, which default to the shared tester-facing DB.

    Safe to re-run: an idempotent migration script skips whatever's already applied, and
    republishing just overwrites the previous output folder.

.PARAMETER Server
    SQL Server hostname. Defaults to the shared test server this project has always used.

.PARAMETER Database
    Target database name - the tester-facing DB, NOT your local AI_Helpers_DEV working copy.

.PARAMETER Configuration
    Build configuration for both the migration script and the publish step.

.PARAMETER OutputRoot
    Where the migration script and publish output land. Defaults to a timestamped folder next
    to (not inside) the repo - publishing in-tree causes BLAZOR106 errors on a second publish
    (see docs/DEPLOYMENT.md).

.PARAMETER SitePath
    Physical path of the IIS site to deploy to. If omitted, the script stops after publishing
    and prints the manual copy/recycle steps instead of guessing at your site layout.

.PARAMETER AppPoolName
    App pool to recycle around the file copy. Optional even when -SitePath is given - if
    omitted, files are still copied but you recycle the pool yourself.

.PARAMETER SkipMigration
    Skip phases 1-2 entirely - use for a code-only release with no pending schema changes.

.PARAMETER SkipPublish
    Skip phase 3's publish step - use to re-run just the DB migration on its own.

.EXAMPLE
    .\scripts\Deploy-Release.ps1
    Generates + (on confirmation) applies the migration, publishes, and prints manual deploy
    steps since no -SitePath was given.

.EXAMPLE
    .\scripts\Deploy-Release.ps1 -SitePath 'C:\inetpub\wwwroot\AiHelpers' -AppPoolName 'AiHelpersPool'
    Full end-to-end release - typically run directly on the IIS box.

.EXAMPLE
    .\scripts\Deploy-Release.ps1 -SkipPublish
    DB-only release: generate, review, and (on confirmation) apply the migration script, then stop.
#>
[CmdletBinding()]
param(
    [string]$Server = 'cm-itsqltest01.corp.conwy.gov.uk',
    [string]$Database = 'AI_Helpers',
    [string]$Configuration = 'Release',
    [string]$OutputRoot,
    [string]$SitePath,
    [string]$AppPoolName,
    [switch]$SkipMigration,
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    if (-not $OutputRoot) {
        $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
        $OutputRoot = Join-Path (Split-Path $repoRoot -Parent) "AiHelpers-Release\$timestamp"
    }
    New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

    Write-Host ""
    Write-Host "=== AI Helpers V2 release ===" -ForegroundColor Cyan
    Write-Host "  Repo:           $repoRoot"
    Write-Host "  Target DB:      $Database on $Server"
    Write-Host "  Configuration:  $Configuration"
    Write-Host "  Output:         $OutputRoot"
    if ($SitePath) { Write-Host "  Site path:      $SitePath" }
    Write-Host ""

    if (-not $SkipMigration) {
        # --- Phase 1: generate the migration script ---------------------------------
        Write-Host "--- Phase 1: generating migration script ---" -ForegroundColor Cyan
        & dotnet tool restore | Out-Null

        $scriptPath = Join-Path $OutputRoot 'migrate.sql'
        & dotnet ef migrations script --idempotent --configuration $Configuration -o $scriptPath
        if ($LASTEXITCODE -ne 0) { throw "dotnet ef migrations script failed (exit $LASTEXITCODE)." }

        $sqlContent = Get-Content $scriptPath -Raw
        $flagged = @('DROP COLUMN', 'DROP TABLE', 'ALTER COLUMN', 'sp_rename') |
            Where-Object { $sqlContent -match [regex]::Escape($_) }
        if ($flagged) {
            Write-Host ""
            Write-Host "  Heads up: this script contains $($flagged -join ', ') - check whether" -ForegroundColor Yellow
            Write-Host "  $Database has real rows in the affected table(s) before applying (query it" -ForegroundColor Yellow
            Write-Host "  directly if unsure - a rename/drop that's harmless on an empty dev table" -ForegroundColor Yellow
            Write-Host "  can genuinely lose data on a table testers have been using)." -ForegroundColor Yellow
        }

        Write-Host ""
        Write-Host "  Migration script written to:" -ForegroundColor Green
        Write-Host "    $scriptPath"
        Write-Host ""
        Write-Host "  Review it before continuing." -ForegroundColor Yellow
        try { Start-Process notepad.exe $scriptPath } catch { Write-Host "  (Couldn't auto-open Notepad - open the file above yourself.)" -ForegroundColor DarkYellow }

        # --- Phase 2: apply it ----------------------------------------------------
        $answer = Read-Host "Type YES to apply this script to '$Database' on '$Server' now (anything else skips this phase)"
        if ($answer -eq 'YES') {
            $sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
            if (-not $sqlcmd) {
                Write-Host ""
                Write-Host "  sqlcmd isn't on PATH here - open $scriptPath in SSMS against" -ForegroundColor Yellow
                Write-Host "  $Server / $Database and run it manually instead." -ForegroundColor Yellow
            }
            else {
                Write-Host "--- Phase 2: applying migration ---" -ForegroundColor Cyan
                & sqlcmd -E -S $Server -d $Database -b -i $scriptPath
                if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed applying the migration (exit $LASTEXITCODE) - $Database may be partially migrated, check it before re-running." }
                Write-Host "  Migration applied." -ForegroundColor Green
            }
        }
        else {
            Write-Host "  Skipped applying the migration - re-run with -SkipMigration once you've applied it another way, or just re-run this script (safe/idempotent)." -ForegroundColor Yellow
        }
    }

    if (-not $SkipPublish) {
        # --- Phase 3: publish -------------------------------------------------------
        Write-Host ""
        Write-Host "--- Phase 3: publishing ---" -ForegroundColor Cyan
        $publishPath = Join-Path $OutputRoot 'publish'
        & dotnet publish -c $Configuration -o $publishPath
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)." }

        # Real incident (see docs/DEPLOYMENT.md): these gitignored folders aren't excluded from
        # git, but .gitignore means nothing to dotnet publish's own MSBuild globbing - they
        # shipped to a live server once already before an explicit <Content Remove /> was added.
        $forbidden = @('Project Info', 'Design brief', 'Tools') |
            ForEach-Object { Join-Path $publishPath $_ } |
            Where-Object { Test-Path $_ }
        if ($forbidden) {
            throw "Publish output contains folder(s) that must never ship: $($forbidden -join ', '). Check AiHelpers.csproj's <Content Remove /> exclusions before deploying this build."
        }
        Write-Host "  Published to $publishPath (verified Project Info/Design brief/Tools are absent)." -ForegroundColor Green

        if ($SitePath) {
            $answer = Read-Host "Type YES to stop the app pool, copy the new build to '$SitePath', and restart it"
            if ($answer -eq 'YES') {
                Write-Host "--- Deploying to $SitePath ---" -ForegroundColor Cyan
                if ($AppPoolName) {
                    Import-Module WebAdministration -ErrorAction SilentlyContinue
                    if (Get-Command Stop-WebAppPool -ErrorAction SilentlyContinue) {
                        Stop-WebAppPool -Name $AppPoolName
                        Write-Host "  App pool '$AppPoolName' stopped."
                    }
                    else {
                        Write-Host "  WebAdministration module not available here - stop the app pool manually in IIS Manager, then press Enter." -ForegroundColor Yellow
                        Read-Host | Out-Null
                    }
                }
                else {
                    Write-Host "  No -AppPoolName given - copying files without stopping the pool first. Recycle it yourself after this finishes." -ForegroundColor Yellow
                }

                Copy-Item -Path (Join-Path $publishPath '*') -Destination $SitePath -Recurse -Force
                Write-Host "  Files copied to $SitePath."

                if ($AppPoolName) {
                    if (Get-Command Start-WebAppPool -ErrorAction SilentlyContinue) {
                        Start-WebAppPool -Name $AppPoolName
                        Write-Host "  App pool '$AppPoolName' started." -ForegroundColor Green
                    }
                    else {
                        Write-Host "  Start the app pool manually in IIS Manager." -ForegroundColor Yellow
                    }
                }
            }
            else {
                Write-Host "  Skipped deployment - publish output is sitting in $publishPath, copy it across whenever you're ready." -ForegroundColor Yellow
            }
        }
        else {
            Write-Host ""
            Write-Host "  No -SitePath given - manual next steps:" -ForegroundColor Yellow
            Write-Host "    1. Copy the contents of $publishPath to the IIS site's physical path."
            Write-Host "    2. Recycle the app pool (or iisreset) to pick up the new binaries."
        }
    }

    Write-Host ""
    Write-Host "=== Done. Smoke test (see docs/DEPLOYMENT.md): sign in, re-check a Helper run end-to-end. ===" -ForegroundColor Cyan
}
finally {
    Pop-Location
}
