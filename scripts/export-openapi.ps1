<#
.SYNOPSIS
    Exports the live OpenAPI (Swagger) specification from the running ElshazlyStore API
    and writes it to docs/openapi.json as the versioned contract artifact.

.DESCRIPTION
    1. Starts the API in the background (Development environment)
    2. Waits for the health endpoint to respond
    3. Fetches /swagger/v1/swagger.json
    4. Normalises the JSON for stable diffs
    5. Writes to docs/openapi.json
    6. Stops the API process

    Prerequisites:
      - .NET 8 SDK installed
      - PostgreSQL running with the dev database available
        (see appsettings.Development.json for connection string)

.EXAMPLE
    .\scripts\export-openapi.ps1
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot    = Split-Path -Parent $PSScriptRoot
$apiProject  = Join-Path $repoRoot 'src\ElshazlyStore.Api'
$outputFile  = Join-Path $repoRoot 'docs\openapi.json'
$healthUrl   = 'http://localhost:5238/api/v1/health'
$swaggerUrl  = 'http://localhost:5238/swagger/v1/swagger.json'
$maxWaitSecs = 60

# -- Ensure docs/ directory exists --
$docsDir = Join-Path $repoRoot 'docs'
if (-not (Test-Path $docsDir)) { New-Item -ItemType Directory -Path $docsDir | Out-Null }

# -- Free port 5238 if occupied --
Write-Host "[export-openapi] Ensuring port 5238 is free..."
Get-NetTCPConnection -LocalPort 5238 -ErrorAction SilentlyContinue |
    ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
Start-Sleep -Seconds 1

# -- Start API in the background --
Write-Host "[export-openapi] Starting API (Development)..."
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$apiProcess = Start-Process -FilePath 'dotnet' `
    -ArgumentList "run --project `"$apiProject`"" `
    -PassThru -WindowStyle Hidden

# -- Wait for health endpoint --
Write-Host "[export-openapi] Waiting for API to be healthy (max ${maxWaitSecs}s)..."
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
    Write-Error "[export-openapi] API did not become healthy within ${maxWaitSecs}s. Aborting."
    Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
    exit 1
}

Write-Host "[export-openapi] API is healthy."

# -- Fetch swagger.json as raw text --
Write-Host "[export-openapi] Fetching $swaggerUrl ..."
try {
    $response = Invoke-WebRequest -Uri $swaggerUrl -TimeoutSec 15 -UseBasicParsing -ErrorAction Stop
    $rawJsonText = $response.Content
} catch {
    Write-Error "[export-openapi] Failed to fetch swagger.json: $_"
    Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
    exit 1
}

# -- Normalise JSON (round-trip for consistent formatting) --
$parsed = $rawJsonText | ConvertFrom-Json
$normalised = $parsed | ConvertTo-Json -Depth 100

# -- Write artifact (UTF-8 no BOM) --
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($outputFile, $normalised, $utf8NoBom)
Write-Host "[export-openapi] Artifact written to: $outputFile"

# -- Stop API --
Write-Host "[export-openapi] Stopping API process..."
Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
Get-NetTCPConnection -LocalPort 5238 -ErrorAction SilentlyContinue |
    ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }

Write-Host "[export-openapi] Done."
exit 0
