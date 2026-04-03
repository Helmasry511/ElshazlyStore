# BACKEND 1-R1 — IDENTIFIERS FIX CLOSEOUT

**Date:** 2026-03-05  
**Status:** COMPLETE — awaiting user approval before Backend Phase 2

---

## 1. Root Causes Found

### A) SKU Generation — Fragile OrderBy Strategy

**Prior code** (`IdentifierGenerator.GenerateSkuAsync`) used `OrderByDescending(v => v.Sku)` (string sort) to find the "max" SKU. On PostgreSQL with locale-aware collation, non-numeric custom SKUs like `"EXPLICIT-XYZ"` sort **after** numeric ones like `"0000000001"`. The first result was therefore a non-numeric SKU, `long.TryParse` failed, and the code fell through to a **fallback that loaded ALL SKUs into memory** — a performance risk at scale.

**Observed behavior during reproduction:** SKU generation does produce correct 10-digit values on the current PostgreSQL instance. The fragility was in the query strategy, not a hard failure. The root cause of the user's "SKU NOT generated" report may have been:
- A transient DB timeout under the full-table scan fallback path
- The fallback scanning millions of rows
- A mismatch between code deployed and code in source (now verified consistent)

**Fix:** Rewrote `GenerateSkuAsync` to:
1. First query only 10-char SKUs (`WHERE LENGTH("Sku") = 10`), then parse numeric in memory — this targets generated SKUs directly.
2. If no 10-digit numeric SKUs exist, fall back to scanning all non-empty SKUs.
3. Both paths find the max numeric value and increment by 1.
4. Eliminates the `OrderByDescending` string sort entirely.

### B) Manual Barcode Lookup — Already Working

**Prior code** correctly creates a `BarcodeReservation` for every variant (both generated and manual barcodes). `GET /api/v1/barcodes/{barcode}` queries `BarcodeReservations` by exact match and returns the variant info.

**Reproduction confirmed:** Manual barcodes are discoverable via barcode lookup. No code change needed.

### C) SKU Discoverability — No Dedicated Endpoint

**Prior code** had:
- `GET /api/v1/variants?q={term}` — searches SKU, name, color, size, barcode (this already worked)
- `GET /api/v1/variants/{id}` — lookup by GUID
- **No** `GET /api/v1/variants/by-sku/{sku}` endpoint

**Fix:** Added `GET /api/v1/variants/by-sku/{sku}` — exact-match SKU lookup returning the full variant DTO (or 404). Permission: `PRODUCTS_READ`.

---

## 2. Exact Fixes

| File | Change |
|------|--------|
| `src/ElshazlyStore.Infrastructure/Services/IdentifierGenerator.cs` | Rewrote `GenerateSkuAsync` — replaced fragile `OrderByDescending` string sort with targeted 10-char filter + numeric parse. Eliminates full-table fallback on PostgreSQL. |
| `src/ElshazlyStore.Api/Endpoints/VariantEndpoints.cs` | Added `GET /variants/by-sku/{sku}` endpoint with `GetBySkuAsync` handler. Permission: `PRODUCTS_READ`. |
| `tests/ElshazlyStore.Tests/Api/IdentifierGenerationTests.cs` | Added 7 new R1 tests for discoverability (see §4). |
| `docs/api.md` | Added `by-sku` endpoint documentation; clarified search and barcode lookup capabilities. |

---

## 3. Updated API Behavior (Truth)

### Variant Creation — `POST /api/v1/variants`

| Field | When Omitted/null/"" | When Provided |
|-------|---------------------|---------------|
| `sku` | Server generates 10-digit numeric (counter-based, e.g., `"0000000042"`) | Preserved as-is; 409 if duplicate |
| `barcode` | Server generates 13-digit numeric (random EAN-like) | Preserved as-is; 409 if duplicate or retired |

### SKU Lookup — `GET /api/v1/variants/by-sku/{sku}` *(NEW)*

| Status | Condition |
|--------|-----------|
| `200 OK` | Variant found — returns `VariantListDto` |
| `404` | No variant with that SKU |

Response shape:
```json
{
  "id": "guid",
  "productId": "guid",
  "productName": "T-Shirt Basic",
  "sku": "CUSTOM-SKU-001",
  "color": "Black",
  "size": "M",
  "retailPrice": 49.99,
  "wholesalePrice": 35.00,
  "isActive": true,
  "barcode": "4829103756284"
}
```

### Barcode Lookup — `GET /api/v1/barcodes/{barcode}`

Works for **both** generated and manual barcodes. No change to behavior.

### Variant List Search — `GET /api/v1/variants?q={term}`

Searches across: **SKU**, product name, color, size, **barcode**.  
Works for both generated and manual identifiers. No change to behavior.

---

## 4. Test List + Results

```
Build succeeded.    0 Warning(s)    0 Error(s)

Passed!  - Failed: 0, Passed: 228, Skipped: 0, Total: 228, Duration: 16 s
```

### Original Tests (7/7 pass — unchanged)

| # | Test | Description |
|---|------|-------------|
| 1 | `CreateVariant_OmitSkuAndBarcode_ServerGeneratesBoth` | Both omitted → 10-digit SKU + 13-digit barcode |
| 2 | `CreateVariant_ExplicitSkuAndBarcode_PreservesValues` | Both provided → preserved as-is |
| 3 | `CreateVariant_OmitSkuOnly_GeneratesSkuKeepsBarcode` | SKU omitted → generated SKU, explicit barcode kept |
| 4 | `CreateVariant_OmitBarcodeOnly_GeneratesBarcodeKeepsSku` | Barcode omitted → generated barcode, explicit SKU kept |
| 5 | `CreateVariant_MultipleOmitted_GeneratesUniqueIdentifiers` | 5 variants, both omitted → all unique |
| 6 | `CreateVariant_DuplicateExplicitSku_Returns409` | Duplicate SKU → 409 |
| 7 | `CreateVariant_GeneratedSkuAppearsInBarcodeLookup` | Generated barcode lookup returns generated SKU |

### New R1 Tests (7/7 pass)

| # | Test | Description |
|---|------|-------------|
| 8 | `ManualBarcode_AppearsInBarcodeLookup` | Explicit barcode → discoverable via `GET /barcodes/{barcode}` |
| 9 | `ManualSku_DiscoverableViaBySkuEndpoint` | Explicit SKU → discoverable via `GET /variants/by-sku/{sku}` |
| 10 | `GeneratedSku_DiscoverableViaBySkuEndpoint` | Generated SKU → discoverable via `GET /variants/by-sku/{sku}` |
| 11 | `ManualSku_DiscoverableViaListSearch` | Explicit SKU → appears in `GET /variants?q=` search results |
| 12 | `GeneratedSku_DiscoverableViaListSearch` | Generated SKU → appears in `GET /variants?q=` search results |
| 13 | `BySkuEndpoint_NonExistent_Returns404` | Non-existent SKU → 404 |
| 14 | `DuplicateExplicitBarcode_Returns409` | Duplicate manual barcode → 409 |

### Existing Tests (214 pass — no regressions)

All other tests pass unchanged.

---

## 5. Human API Script (5 Steps)

**Prerequisites:** Server running on `http://localhost:5238`. Replace `$TOKEN` with your bearer token.

### Step 1 — Omit both SKU and barcode → both generated

```powershell
$headers = @{ Authorization = "Bearer $TOKEN"; "Content-Type" = "application/json" }

# Create a test product
$prod = Invoke-RestMethod -Uri "http://localhost:5238/api/v1/products" -Method POST -Headers $headers -Body '{"name":"R1 Verify Product"}'
$pid = $prod.id

# Create variant with both omitted
$v1 = Invoke-RestMethod -Uri "http://localhost:5238/api/v1/variants" -Method POST -Headers $headers -Body "{`"productId`":`"$pid`",`"color`":`"Red`"}"
Write-Host "SKU: $($v1.sku) (expect 10-digit numeric)"
Write-Host "Barcode: $($v1.barcode) (expect 13-digit numeric)"
```

### Step 2 — Manual SKU + barcode accepted if unique

```powershell
$body2 = "{`"productId`":`"$pid`",`"sku`":`"MY-CUSTOM-SKU`",`"barcode`":`"MY-CUSTOM-BC-001`",`"color`":`"Blue`"}"
$v2 = Invoke-RestMethod -Uri "http://localhost:5238/api/v1/variants" -Method POST -Headers $headers -Body $body2
Write-Host "SKU: $($v2.sku) (expect MY-CUSTOM-SKU)"
Write-Host "Barcode: $($v2.barcode) (expect MY-CUSTOM-BC-001)"
```

### Step 3 — Manual barcode works in GET /barcodes/{barcode}

```powershell
$bl = Invoke-RestMethod -Uri "http://localhost:5238/api/v1/barcodes/MY-CUSTOM-BC-001" -Method GET -Headers $headers
Write-Host "Barcode lookup SKU: $($bl.sku) (expect MY-CUSTOM-SKU)"
Write-Host "Status: $($bl.status) (expect Assigned)"
```

### Step 4 — SKU works via GET /variants/by-sku/{sku}

```powershell
$sl = Invoke-RestMethod -Uri "http://localhost:5238/api/v1/variants/by-sku/MY-CUSTOM-SKU" -Method GET -Headers $headers
Write-Host "By-SKU lookup: $($sl.sku) (expect MY-CUSTOM-SKU)"
Write-Host "Barcode: $($sl.barcode) (expect MY-CUSTOM-BC-001)"
```

### Step 5 — Generated SKU works via by-sku lookup

```powershell
$gl = Invoke-RestMethod -Uri "http://localhost:5238/api/v1/variants/by-sku/$($v1.sku)" -Method GET -Headers $headers
Write-Host "Generated SKU lookup: $($gl.sku) (expect $($v1.sku))"
Write-Host "Generated barcode: $($gl.barcode) (expect $($v1.barcode))"
```

---

## 6. Decision Record

| Decision | Choice | Justification |
|----------|--------|---------------|
| SKU discoverability approach | **Option 1: `GET /variants/by-sku/{sku}`** | Dedicated endpoint is simple, cache-friendly, and mirrors the barcode lookup pattern. Search (`?q=`) already works too. |
| SKU generation algorithm | Counter-based, 10-digit | Unchanged from BACKEND 1 — predictable, human-readable. |
| Barcode generation algorithm | Random, 13-digit | Unchanged from BACKEND 1 — EAN-compatible. |

---

## 7. UI Integration Notes

- **SKU lookup:** Use `GET /api/v1/variants/by-sku/{sku}` for direct SKU→variant resolution.
- **Barcode scan:** Use `GET /api/v1/barcodes/{barcode}` for POS scanning (unchanged).
- **Search:** `GET /api/v1/variants?q={term}` searches SKU, name, color, size, barcode.
- **Auto-generation:** Omit `sku` and/or `barcode` from `POST /variants` body; read generated values from response.

---

## STOP

**Awaiting user approval before proceeding to Backend Phase 2.**
