<#
.SYNOPSIS
    Detects drift between the committed OpenAPI artifact (docs/openapi.json)
    and the live API's current Swagger output.

.DESCRIPTION
    1. Starts the API in the background (Development environment)
    2. Fetches the live swagger.json
    3. Normalises both live and committed JSON through the same pipeline
    4. Compares them semantically
    5. Exits 0 if identical (no drift), exits 1 if different (drift detected)

    Prerequisites:
      - .NET 8 SDK installed
      - PostgreSQL running with the dev database available
      - docs/openapi.json must already exist (run export-openapi.ps1 first)

.EXAMPLE
    .\scripts\check-openapi-drift.ps1
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot     = Split-Path -Parent $PSScriptRoot
$apiProject   = Join-Path $repoRoot 'src\ElshazlyStore.Api'
$artifactFile = Join-Path $repoRoot 'docs\openapi.json'
$healthUrl    = 'http://localhost:5238/api/v1/health'
$swaggerUrl   = 'http://localhost:5238/swagger/v1/swagger.json'
$maxWaitSecs  = 60

# -- Pre-flight: artifact must exist --
if (-not (Test-Path $artifactFile)) {
    Write-Host "[drift-check] ERROR: docs/openapi.json not found."
    Write-Host "  Run '.\scripts\export-openapi.ps1' first to create the baseline artifact."
    exit 1
}

# -- Free port 5238 if occupied --
Write-Host "[drift-check] Ensuring port 5238 is free..."
Get-NetTCPConnection -LocalPort 5238 -ErrorAction SilentlyContinue |
    ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
Start-Sleep -Seconds 1

# -- Start API in the background --
Write-Host "[drift-check] Starting API (Development)..."
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$apiProcess = Start-Process -FilePath 'dotnet' `
    -ArgumentList "run --project `"$apiProject`"" `
    -PassThru -WindowStyle Hidden

# -- Wait for health endpoint --
Write-Host "[drift-check] Waiting for API to be healthy (max ${maxWaitSecs}s)..."
$elapsed = 0
$healthy = $false
while ($elapsed -lt $maxWaitSecs) {
    Start-Sleep -Seconds 2
    $elapsed += 2
    try {
        $null = Invoke-RestMethod -Uri $healthUrl -TimeoutSec 3 -ErrorAction Stop
        $healthy = $true
        break
    } catch {
        Write-Host "  ... waiting ($elapsed s)"
    }
}

if (-not $healthy) {
    Write-Host "[drift-check] ERROR: API did not become healthy within ${maxWaitSecs}s. Aborting."
    Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
    exit 1
}

Write-Host "[drift-check] API is healthy."

# -- Fetch live swagger.json as raw text --
Write-Host "[drift-check] Fetching live swagger.json ..."
try {
    $response = Invoke-WebRequest -Uri $swaggerUrl -TimeoutSec 15 -UseBasicParsing -ErrorAction Stop
    $liveJsonText = $response.Content
} catch {
    Write-Host "[drift-check] ERROR: Failed to fetch swagger.json: $_"
    Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
    exit 1
}

# -- Stop API --
Write-Host "[drift-check] Stopping API process..."
Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
Get-NetTCPConnection -LocalPort 5238 -ErrorAction SilentlyContinue |
    ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }

# -- Normalise BOTH sides through the same pipeline --
# This ensures formatting differences do not cause false drift.
$liveObj = $liveJsonText | ConvertFrom-Json
$liveNormalised = $liveObj | ConvertTo-Json -Depth 100

$committedText = [System.IO.File]::ReadAllText($artifactFile, [System.Text.Encoding]::UTF8)
$committedObj = $committedText | ConvertFrom-Json
$committedNormalised = $committedObj | ConvertTo-Json -Depth 100

# -- Compare --
if ($liveNormalised -eq $committedNormalised) {
    Write-Host ""
    Write-Host "============================================================"
    Write-Host "  [drift-check] NO DRIFT DETECTED -- contract is unchanged"
    Write-Host "============================================================"
    Write-Host ""
    exit 0
}
else {
    Write-Host ""
    Write-Host "============================================================"
    Write-Host "  [drift-check] DRIFT DETECTED -- API contract has changed!"
    Write-Host "============================================================"
    Write-Host ""
    Write-Host "The live API swagger output differs from docs/openapi.json."
    Write-Host ""
    Write-Host "If this change is intentional:"
    Write-Host "  1. Run: .\scripts\export-openapi.ps1"
    Write-Host "  2. Review the diff in docs/openapi.json"
    Write-Host "  3. Commit the updated artifact with a closeout note"
    Write-Host ""

    # Write live version to temp for manual diff inspection
    $tempFile = Join-Path $repoRoot 'docs\openapi-live.tmp.json'
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($tempFile, $liveNormalised, $utf8NoBom)
    Write-Host "  Live version saved to: $tempFile"
    Write-Host "  Compare with:  diff docs/openapi.json docs/openapi-live.tmp.json"
    Write-Host ""
    exit 1
}
