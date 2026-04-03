# BACKEND 4 -- OPENAPI ARTIFACT + CONTRACT FREEZE PIPELINE -- CLOSEOUT

**Phase:** BACKEND 4  
**Date:** 2026-03-06  
**Status:** COMPLETE  

---

## 1. Summary

This phase establishes a versioned **OpenAPI 3.0.1 artifact** (`docs/openapi.json`) generated directly from the running API's Swagger output, along with a **drift detection pipeline** that fails when the API contract changes without updating the artifact. No business logic was modified; this phase is purely about contract reproducibility and drift prevention.

### Key outcomes:
- **Artifact committed:** `docs/openapi.json` (452 KB, 4723 lines) -- the full OpenAPI 3.0.1 spec
- **Export script:** `scripts/export-openapi.ps1` -- regenerates artifact from the live API
- **Drift check script:** `scripts/check-openapi-drift.ps1` -- compares live vs committed, exits non-zero on drift
- **CI workflow:** `.github/workflows/openapi-drift.yml` -- automated drift check on PRs
- **Documentation:** `docs/openapi-contract.md` -- policy and usage guide

---

## 2. Artifact Location

| File | Description |
|------|-------------|
| `docs/openapi.json` | OpenAPI 3.0.1 specification exported from `/swagger/v1/swagger.json` at `http://localhost:5238`. Contains all endpoints, schemas, and security definitions. |

The artifact is the **canonical contract representation** of the ElshazlyStore API. It is generated from the API running in `Development` mode with Swashbuckle (v6.9.0).

---

## 3. Scripts Added

### `scripts/export-openapi.ps1`

**Purpose:** Regenerate the OpenAPI artifact from the running API.

**How to run:**
```powershell
.\scripts\export-openapi.ps1
```

**What it does:**
1. Frees port 5238 if occupied
2. Starts the API in Development mode (background process)
3. Waits up to 60s for the health endpoint (`/api/v1/health`)
4. Fetches `/swagger/v1/swagger.json` via `Invoke-WebRequest`
5. Normalises JSON via `ConvertFrom-Json | ConvertTo-Json -Depth 100`
6. Writes `docs/openapi.json` (UTF-8 no BOM)
7. Stops the API process

**Prerequisites:** .NET 8 SDK, PostgreSQL with dev database running.

---

### `scripts/check-openapi-drift.ps1`

**Purpose:** Detect whether the live API contract differs from the committed artifact.

**How to run:**
```powershell
.\scripts\check-openapi-drift.ps1
```

**Exit codes:**
- `0` -- No drift detected, contract unchanged
- `1` -- Drift detected (or pre-flight failure: artifact missing, API won't start)

**What it does:**
1. Verifies `docs/openapi.json` exists
2. Starts API, waits for health
3. Fetches live swagger.json
4. Stops API
5. Normalises **both** live and committed JSON through the same `ConvertFrom-Json | ConvertTo-Json -Depth 100` pipeline (eliminates formatting-only differences)
6. Compares normalised strings
7. On drift: saves live version to `docs/openapi-live.tmp.json` (git-ignored) for manual diffing

---

## 4. Drift Detection Behavior

### What fails:
When any code change modifies the API surface (new/changed/removed endpoints, altered request/response schemas, modified security requirements), the drift check script exits with code 1 and prints:

```
============================================================
  [drift-check] DRIFT DETECTED -- API contract has changed!
============================================================

The live API swagger output differs from docs/openapi.json.

If this change is intentional:
  1. Run: .\scripts\export-openapi.ps1
  2. Review the diff in docs/openapi.json
  3. Commit the updated artifact with a closeout note
```

### How to update intentionally:
1. Make your endpoint/schema changes in code
2. Run `.\scripts\export-openapi.ps1`
3. Verify `git diff docs/openapi.json` shows only expected changes
4. Commit `docs/openapi.json` alongside the code change
5. Include a brief note describing the contract change

---

## 5. CI Notes

A GitHub Actions workflow is provided at `.github/workflows/openapi-drift.yml`:

- **Trigger:** PRs that touch `src/**` or `docs/openapi.json`
- **Runs:** Ubuntu-latest with PostgreSQL 16 service container
- **Steps:** Checkout, .NET 8 setup, restore, start API, health wait, fetch + compare (via Python `json.dumps` with `sort_keys` for deterministic comparison)
- **Failure:** Exits non-zero if drift detected, with instructions to regenerate

If GitHub Actions is not yet configured for this repo, the workflow file is ready to activate. For local pre-push verification, run `.\scripts\check-openapi-drift.ps1`.

---

## 6. Build & Test Results

### Build
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:01.85
```

### Tests
```
Failed:     1, Passed:   243, Skipped:     0, Total:   244
```

The single failure is **pre-existing** and unrelated to this phase:
- `ProductionBatchTests.PostProductionBatch_ConcurrentDoublePost_OnlyOneStockMovement` -- a concurrency edge-case test that returns HTTP 500 instead of the expected 409. This test was already failing before BACKEND 4 (visible in terminal history from prior phases). **No new test failures introduced.**

---

## 7. Files Added/Modified

| File | Action | Description |
|------|--------|-------------|
| `docs/openapi.json` | **Added** | Versioned OpenAPI 3.0.1 artifact (452 KB) |
| `docs/openapi-contract.md` | **Added** | Documentation: what it is, how to regenerate, drift policy |
| `scripts/export-openapi.ps1` | **Added** | Artifact export script |
| `scripts/check-openapi-drift.ps1` | **Added** | Drift detection script |
| `.github/workflows/openapi-drift.yml` | **Added** | CI workflow for automated drift checks |
| `.gitignore` | **Modified** | Added `docs/openapi-live.tmp.json` to ignore list |

---

## 8. Manual Verification Steps

1. **Build:** `dotnet build ElshazlyStore.sln` -- 0 errors, 0 warnings
2. **Tests:** `dotnet test ElshazlyStore.sln` -- 243 pass, 1 pre-existing failure
3. **Export:** `.\scripts\export-openapi.ps1` -- generates `docs/openapi.json` successfully
4. **Drift check:** `.\scripts\check-openapi-drift.ps1` -- exits 0, prints "NO DRIFT DETECTED"
5. **Artifact valid:** `docs/openapi.json` contains valid OpenAPI 3.0.1 with correct title "ElshazlyStore API" and version "v1"

---

## STOP

Do not proceed to the next phase until user approval.
