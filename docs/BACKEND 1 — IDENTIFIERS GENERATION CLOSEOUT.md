# BACKEND 1 — IDENTIFIERS GENERATION CLOSEOUT

**Date:** 2026-03-05
**Status:** COMPLETE — awaiting user approval before Backend Phase 2

---

## 1. Design Decisions

### A) Primary Scanning Key in POS

**Decision:** Barcode scanning remains the primary POS key via `GET /api/v1/barcodes/{barcode}`.

**Reasoning:** The existing `BarcodeReservation` entity with its unique index, cache-backed lookup endpoint, and status lifecycle (Reserved → Assigned → Retired) is the correct scanning entry point. No changes needed.

### B) SKU Policy

**Decision:** 10-digit numeric, zero-padded, counter-based, server-generated when omitted.

| Property | Value |
|----------|-------|
| Format | `NNNNNNNNNN` (10 digits, zero-padded) |
| Example | `0000000001`, `0000000042` |
| Generation | Counter-based: query max existing numeric SKU, increment by 1 |
| When generated | Client sends `sku` as `null`, empty string, or omits the field |
| When preserved | Client sends a non-empty `sku` value — stored as-is |

**Justification:**
- 10 digits supports up to 10 billion variants — more than sufficient.
- Counter-based is predictable, monotonically increasing, and human-friendly for inventory labels.
- Numeric-only avoids encoding issues on barcode labels.
- Concurrency safety via DB unique index + retry loop (up to 5 attempts).

### C) Barcode Policy

**Decision:** 13-digit numeric, random (EAN-like format), server-generated when omitted.

| Property | Value |
|----------|-------|
| Format | `NNNNNNNNNNNNN` (13 digits) |
| Example | `4829103756284` |
| Generation | Random via `Random.Shared` |
| Uniqueness | Guaranteed by DB unique index on `barcode_reservations.Barcode` |
| When generated | Client sends `barcode` as `null`, empty string, or omits the field |
| When preserved | Client sends a non-empty `barcode` value — stored as-is |

**Justification:**
- 13-digit format is compatible with EAN-13 barcode scanners.
- Random generation avoids sequential information leakage.
- Collision probability for 13-digit random: ~1 in 10 trillion per pair — negligible; DB constraint provides hard guarantee.
- Matches existing `ImportService.GenerateBarcode()` format (now unified into `IdentifierGenerator`).

### D) Product-Level Global Barcode

**Decision:** NO — not implemented.

**Reasoning:**
- Barcodes are a variant-level concept. Products are containers; variants are scanned at POS.
- Adding a product-level barcode would confuse the scanning flow (which variant to resolve?).
- If needed later, it can be added as a separate entity without breaking anything.
- **Impact on UI:** None. UI continues to work with variant-level barcodes only.

---

## 2. DB / Migration Changes

**No migration needed.** No new columns, tables, or index changes.

Existing constraints that enforce uniqueness:
- `product_variants`: unique index on `Sku` (already exists)
- `barcode_reservations`: unique index on `Barcode` (already exists)

These constraints serve as the concurrency-safety net for generated identifiers. The retry loop in `CreateAsync` catches `DbUpdateException` on unique violation and regenerates.

---

## 3. API Contract Changes (Before / After)

### POST /api/v1/variants

#### BEFORE
```json
// Request — SKU and Barcode REQUIRED (400 if null/empty)
{
  "productId": "guid",
  "sku": "TSH-BLK-M",           // REQUIRED
  "barcode": "8901234567890",    // REQUIRED
  "color": "Black",
  "size": "M",
  "retailPrice": 49.99,
  "wholesalePrice": 35.00
}
```

#### AFTER
```json
// Request — SKU and Barcode OPTIONAL (server generates if omitted)
{
  "productId": "guid",           // REQUIRED
  "sku": null,                   // OPTIONAL — server generates 10-digit numeric if null/empty/omitted
  "barcode": null,               // OPTIONAL — server generates 13-digit numeric if null/empty/omitted
  "color": "Black",
  "size": "M",
  "retailPrice": 49.99,
  "wholesalePrice": 35.00
}
```

### DTO Change

```csharp
// BEFORE
public sealed record CreateVariantRequest(
    Guid ProductId, string Sku, string Barcode,
    string? Color, string? Size, decimal? RetailPrice, decimal? WholesalePrice,
    decimal? LowStockThreshold = null);

// AFTER
public sealed record CreateVariantRequest(
    Guid ProductId, string? Sku = null, string? Barcode = null,
    string? Color = null, string? Size = null, decimal? RetailPrice = null,
    decimal? WholesalePrice = null, decimal? LowStockThreshold = null);
```

### Backward Compatibility

- Clients that already send `sku` and `barcode` will continue to work unchanged.
- Clients that omit or send null for `sku`/`barcode` will get server-generated values in the response.
- Response shape is unchanged — `VariantListDto` always includes `Sku` and `Barcode`.

---

## 4. Files Changed

| File | Change |
|------|--------|
| `src/ElshazlyStore.Api/Endpoints/VariantEndpoints.cs` | Made `Sku`/`Barcode` optional in `CreateVariantRequest`; replaced hard validation with auto-generation + retry loop |
| `src/ElshazlyStore.Infrastructure/Services/IdentifierGenerator.cs` | **NEW** — shared static utility for SKU (counter-based) and Barcode (random) generation |
| `src/ElshazlyStore.Infrastructure/Services/ImportService.cs` | Removed local `GenerateBarcode()`; delegated to `IdentifierGenerator`; added SKU auto-generation for import when omitted |
| `docs/api.md` | Updated `POST /variants` documentation to reflect optional SKU/Barcode |
| `tests/ElshazlyStore.Tests/Api/IdentifierGenerationTests.cs` | **NEW** — 7 integration tests for identifier generation |

---

## 5. Test Results

```
Build succeeded.    0 Warning(s)    0 Error(s)

Passed!  - Failed: 0, Passed: 221, Skipped: 0, Total: 221, Duration: 16 s
```

### New Tests (7/7 pass)

| Test | Description |
|------|-------------|
| `CreateVariant_OmitSkuAndBarcode_ServerGeneratesBoth` | Both omitted → server generates 10-digit SKU + 13-digit barcode |
| `CreateVariant_ExplicitSkuAndBarcode_PreservesValues` | Both provided → values preserved as-is |
| `CreateVariant_OmitSkuOnly_GeneratesSkuKeepsBarcode` | Only SKU omitted → generated SKU, explicit barcode kept |
| `CreateVariant_OmitBarcodeOnly_GeneratesBarcodeKeepsSku` | Only barcode omitted → generated barcode, explicit SKU kept |
| `CreateVariant_MultipleOmitted_GeneratesUniqueIdentifiers` | 5 variants created with both omitted → all unique |
| `CreateVariant_DuplicateExplicitSku_Returns409` | Explicit duplicate SKU → 409 Conflict |
| `CreateVariant_GeneratedSkuAppearsInBarcodeLookup` | Generated SKU/barcode appear correctly in barcode lookup |

### Existing Tests (214 pass — no regressions)

All existing barcode tests and other integration tests continue to pass without modification.

---

## 6. Sample Requests / Responses

### Sample 1: Create variant with SKU/Barcode omitted

**Request:**
```http
POST /api/v1/variants
Authorization: Bearer <token>
Content-Type: application/json

{
  "productId": "a1b2c3d4-...",
  "color": "Red",
  "size": "L",
  "retailPrice": 29.99
}
```

**Response (201 Created):**
```json
{
  "id": "f5e6d7c8-...",
  "productId": "a1b2c3d4-...",
  "productName": "T-Shirt Basic",
  "sku": "0000000001",
  "color": "Red",
  "size": "L",
  "retailPrice": 29.99,
  "wholesalePrice": null,
  "isActive": true,
  "barcode": "4829103756284"
}
```

### Sample 2: Create variant with explicit SKU/Barcode

**Request:**
```http
POST /api/v1/variants
Authorization: Bearer <token>
Content-Type: application/json

{
  "productId": "a1b2c3d4-...",
  "sku": "CUSTOM-SKU-001",
  "barcode": "9876543210123",
  "color": "Blue",
  "size": "M",
  "retailPrice": 49.99,
  "wholesalePrice": 35.00
}
```

**Response (201 Created):**
```json
{
  "id": "b8c9d0e1-...",
  "productId": "a1b2c3d4-...",
  "productName": "T-Shirt Basic",
  "sku": "CUSTOM-SKU-001",
  "color": "Blue",
  "size": "M",
  "retailPrice": 49.99,
  "wholesalePrice": 35.00,
  "isActive": true,
  "barcode": "9876543210123"
}
```

---

## 7. UI Integration Notes

- **To use auto-generation:** Simply omit `sku` and `barcode` fields from the create request body, or send them as `null`/empty string.
- **To use explicit values:** Include `sku` and/or `barcode` in the request body with non-empty values.
- **Response always contains:** Both `sku` and `barcode` in the response DTO, whether generated or provided.
- **Fields to read back from response:** `sku`, `barcode` — these are the authoritative generated values.
- **Import flow:** CSV/XLSX import now also auto-generates both SKU and barcode for rows that omit them.

---

## 8. Concurrency Safety

- **Unique DB constraints** on both `product_variants.Sku` and `barcode_reservations.Barcode` prevent duplicates at the database level.
- **Retry loop** (max 5 attempts) in `CreateAsync` catches `DbUpdateException` on unique violation and regenerates identifiers.
- **Scope:** Retry only activates when identifiers were server-generated (not when client provided explicit values — those return 409 immediately).

---

## 9. Audit Log

- No changes needed. The existing `AuditInterceptor` automatically captures `INSERT` operations on `ProductVariant` and `BarcodeReservation` entities, including the generated `Sku` and `Barcode` values in `NewValues` JSON.

---

## 10. Rollback Notes

To revert this change:
1. Restore `CreateVariantRequest` to require `string Sku, string Barcode` (non-nullable).
2. Restore the `"SKU is required"` and `"Barcode is required"` validation in `VariantEndpoints.CreateAsync`.
3. Delete `IdentifierGenerator.cs`.
4. Restore `GenerateBarcode()` in `ImportService.cs` and revert SKU auto-generation for imports.
5. Delete `IdentifierGenerationTests.cs`.
6. No DB migration rollback needed — schema is unchanged.

---

## STOP

**Awaiting user approval before proceeding to Backend Phase 2.**
