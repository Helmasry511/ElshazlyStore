# OpenAPI Contract Artifact & Drift Detection

## What Is This?

The file **`docs/openapi.json`** is the **versioned OpenAPI 3.0.1 specification** for the
ElshazlyStore API. It is generated directly from the running API's Swagger output
(`/swagger/v1/swagger.json`) and committed to the repository as the single source
of truth for the API contract.

This artifact enables:

- **Contract reproducibility** — any consumer can reference the committed spec
  without starting the API.
- **Drift detection** — automated comparison between the live API and the
  committed spec catches unintended contract changes.
- **Change tracking** — `git diff docs/openapi.json` shows exactly what changed
  in any PR that modifies the API surface.

---

## How to Regenerate the Artifact

> **Prerequisites:** .NET 8 SDK, PostgreSQL running with the dev database
> (see `src/ElshazlyStore.Api/appsettings.Development.json`).

### Option A — Using the export script (recommended)

```powershell
.\scripts\export-openapi.ps1
```

The script will:
1. Free port 5238 if occupied
2. Start the API in Development mode
3. Wait for the health check to pass
4. Fetch `/swagger/v1/swagger.json`
5. Normalise and write to `docs/openapi.json`
6. Stop the API

### Option B — Manually (if API is already running)

```powershell
$raw = Invoke-RestMethod -Uri 'http://localhost:5238/swagger/v1/swagger.json'
$json = $raw | ConvertTo-Json -Depth 100
[System.IO.File]::WriteAllText('docs/openapi.json', $json, [System.Text.Encoding]::UTF8)
```

---

## How Drift Is Detected

```powershell
.\scripts\check-openapi-drift.ps1
```

The drift-check script:
1. Starts the API, waits for health
2. Fetches the live swagger.json
3. Normalises it identically to the export script
4. Compares with the committed `docs/openapi.json`
5. **Exit 0** — no drift, contract unchanged
6. **Exit 1** — drift detected; prints instructions to update intentionally

When drift is detected, the script also writes the live version to
`docs/openapi-live.tmp.json` (git-ignored) so you can diff it manually.

---

## Policy

> **Any API change that modifies the contract surface MUST update the artifact.**

1. Make your endpoint/schema changes in code.
2. Run `.\scripts\export-openapi.ps1` to regenerate the artifact.
3. Review `git diff docs/openapi.json` to confirm the change is intentional.
4. Commit the updated `docs/openapi.json` alongside the code change.
5. Include a brief note in your PR/commit describing the contract change.

---

## CI Integration (Optional)

A GitHub Actions workflow is provided at `.github/workflows/openapi-drift.yml`.
It runs the drift check on every PR that touches files under `src/`.

To run locally before pushing:

```powershell
.\scripts\check-openapi-drift.ps1
```

If the check fails, regenerate and commit the artifact as described above.

---

## File Inventory

| File | Purpose |
|------|---------|
| `docs/openapi.json` | Committed OpenAPI 3.0.1 artifact |
| `scripts/export-openapi.ps1` | Regenerates the artifact from the live API |
| `scripts/check-openapi-drift.ps1` | Detects drift between live API and artifact |
| `.github/workflows/openapi-drift.yml` | CI workflow for automated drift detection |
| `docs/openapi-contract.md` | This document |
