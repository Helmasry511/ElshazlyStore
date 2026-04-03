# BACKEND 3 — DEFAULT WAREHOUSE METADATA CLOSEOUT

**Prepared by**: Claude OPUS 4.6 — AGENT MODE  
**Date**: 2026-03-05  
**Baseline**: BUILD 0 err / 0 warn | TESTS 244 passed, 0 failed (was 233 → +11 new)  
**Policy**: No invented facts. Unknown items marked **(غير مذكور)**.

---

## 1) Design Decisions

### A) Where to store default warehouse?

**Decision**: `Product.DefaultWarehouseId` (nullable `Guid?`) with navigation property `Product.DefaultWarehouse`.

**Justification**:
- The requirement says "when creating/editing a **Product**, user selects a Default Warehouse."
- Product-level storage is the natural location. Variants inherit read-only.
- Nullable because warehouse is optional — products can be created without one.

### B) How do variants get read-only display?

**Decision**: Both `VariantDto` (inside `ProductDetailDto.Variants`) and `VariantListDto` (from `/api/v1/variants`) include two **read-only** computed fields: `DefaultWarehouseId` and `DefaultWarehouseName`, derived from `Product.DefaultWarehouseId` / `Product.DefaultWarehouse.Name`.

**Justification**:
- Backend is the single source of truth — the UI should not need a second round-trip to get warehouse info when viewing a variant.
- The variant create/update requests do **NOT** accept warehouse fields (they are read-only on variant).

### C) Behavior when product has no default warehouse

**Decision**: `DefaultWarehouseId` = `null`, `DefaultWarehouseName` = `null` on both Product and Variant DTOs. UI shows "غير محدد" based on null. Variant creation is **not blocked** — warehouse is purely metadata guidance.

### D) FK ON DELETE behavior

**Decision**: `ON DELETE SET NULL`.

**Justification**:
- If a warehouse is deleted, products that referenced it should not be deleted (too destructive).
- `RESTRICT` would prevent warehouse deletion if any product references it, which feels overly rigid for optional metadata.
- `SET NULL` cleanly resets the metadata: "warehouse no longer exists, product has no default."

---

## 2) DB / Migration Changes

### Migration: `20260305000000_0017_product_default_warehouse.cs`

| Operation | Detail |
|-----------|--------|
| `AddColumn` | `products.DefaultWarehouseId` — `uuid`, nullable |
| `CreateIndex` | `IX_products_DefaultWarehouseId` on `products(DefaultWarehouseId)` |
| `AddForeignKey` | `FK_products_warehouses_DefaultWarehouseId` → `warehouses(Id)`, `ON DELETE SET NULL` |

**Down migration** drops FK, index, and column (fully reversible).

### Entity: `Product.cs`

```csharp
// ADDED:
public Guid? DefaultWarehouseId { get; set; }
public Warehouse? DefaultWarehouse { get; set; }
```

### Configuration: `ProductConfiguration.cs`

```csharp
// ADDED:
builder.HasOne(e => e.DefaultWarehouse)
    .WithMany()
    .HasForeignKey(e => e.DefaultWarehouseId)
    .OnDelete(DeleteBehavior.SetNull);

builder.HasIndex(e => e.DefaultWarehouseId);
```

### Model Snapshot: `AppDbContextModelSnapshot.cs`

Updated to include `DefaultWarehouseId` property, index, and FK relationship.

**No stock quantity fields were introduced.** Static verification tests enforce this.

---

## 3) API / DTO Changes (Before → After)

### ProductDto (list endpoint)

| Field | Before | After |
|-------|--------|-------|
| `DefaultWarehouseId` | — | `Guid?` (nullable) |
| `DefaultWarehouseName` | — | `string?` (nullable) |

### ProductDetailDto (GET /products/{id})

| Field | Before | After |
|-------|--------|-------|
| `DefaultWarehouseId` | — | `Guid?` (nullable) |
| `DefaultWarehouseName` | — | `string?` (nullable) |
| `Variants[].DefaultWarehouseId` | — | `Guid?` (read-only, from product) |
| `Variants[].DefaultWarehouseName` | — | `string?` (read-only, from product) |

### VariantListDto (from /api/v1/variants endpoints)

| Field | Before | After |
|-------|--------|-------|
| `DefaultWarehouseId` | — | `Guid?` (read-only, from parent product) |
| `DefaultWarehouseName` | — | `string?` (read-only, from parent product) |

### CreateProductRequest

| Field | Before | After |
|-------|--------|-------|
| `DefaultWarehouseId` | — | `Guid?` (optional, default null) |

### UpdateProductRequest

| Field | Before | After |
|-------|--------|-------|
| `DefaultWarehouseId` | — | `Guid?` — `null` = don't change, `Guid.Empty` = clear, valid GUID = set |

### Variant Create/Update Requests

**No changes.** Variant requests do NOT accept `DefaultWarehouseId` — it is read-only on variant, derived from parent product.

### Desktop DTOs Updated

- `ProductDto.cs` — added `DefaultWarehouseId`, `DefaultWarehouseName`
- `ProductDetailDto` — added `DefaultWarehouseId`, `DefaultWarehouseName`
- `CreateProductRequest` — added `DefaultWarehouseId`
- `UpdateProductRequest` — added `DefaultWarehouseId`
- `VariantDto.cs` — `VariantDto` and `VariantListDto` both added `DefaultWarehouseId`, `DefaultWarehouseName` (read-only)

All new fields are nullable → backward compatible (old clients ignore them; old data returns null).

---

## 4) Error Codes + Validation Rules

### New Error Code

| Code | HTTP Status | When |
|------|-------------|------|
| `WAREHOUSE_INACTIVE` | 422 | Provided `DefaultWarehouseId` references a warehouse with `IsActive = false` |

### Existing Error Code (reused)

| Code | HTTP Status | When |
|------|-------------|------|
| `WAREHOUSE_NOT_FOUND` | 404 | Provided `DefaultWarehouseId` does not match any warehouse |

### Validation Rules

| Rule | Applies To | Behavior |
|------|-----------|----------|
| If `DefaultWarehouseId` is provided on create, it must exist | `POST /products` | 404 `WAREHOUSE_NOT_FOUND` |
| If `DefaultWarehouseId` is provided on create, warehouse must be active | `POST /products` | 422 `WAREHOUSE_INACTIVE` |
| If `DefaultWarehouseId` is provided on update (non-null, non-empty), warehouse must exist | `PUT /products/{id}` | 404 `WAREHOUSE_NOT_FOUND` |
| If `DefaultWarehouseId` is provided on update, warehouse must be active | `PUT /products/{id}` | 422 `WAREHOUSE_INACTIVE` |
| `Guid.Empty` on update → clears DefaultWarehouseId (sets to null) | `PUT /products/{id}` | 200 OK |
| `null` on update → no change | `PUT /products/{id}` | field not modified |
| `DefaultWarehouseId` omitted on create → nullable, product has no default | `POST /products` | persisted as null |

---

## 5) Tests Added + Results

### Test File: `tests/ElshazlyStore.Tests/Api/ProductDefaultWarehouseTests.cs`

| # | Test Name | What It Verifies |
|---|-----------|-----------------|
| 1 | `CreateProduct_WithDefaultWarehouseId_PersistedAndReturned` | Create with warehouse → persisted in DB, returned in POST response and GET |
| 2 | `CreateProduct_WithoutDefaultWarehouse_NullByDefault` | Create without warehouse → null fields |
| 3 | `UpdateProduct_ChangeDefaultWarehouse_Reflected` | Update to set warehouse → GET reflects new value |
| 4 | `UpdateProduct_ClearDefaultWarehouse_SetsNull` | Update with `Guid.Empty` → clears; GET returns null |
| 5 | `CreateProduct_InvalidWarehouseId_ReturnsNotFound` | Create with nonexistent warehouse → 404 `WAREHOUSE_NOT_FOUND` |
| 6 | `VariantDto_IncludesReadOnlyDefaultWarehouseFromProduct` | Variant create + GET variant by ID → includes product warehouse info |
| 7 | `VariantDto_ProductWithoutWarehouse_NullFields` | Variant for product without warehouse → null warehouse fields |
| 8 | `ProductList_IncludesDefaultWarehouseFields` | GET /products list → includes `DefaultWarehouseId` + `DefaultWarehouseName` |
| 9 | `NoStockQuantityFieldsOnProduct` | Static check: Product entity has no Quantity/Qty/Stock fields |
| 10 | `NoStockQuantityFieldsOnVariant` | Static check: ProductVariant entity has no Quantity/Qty/Stock fields |
| 11 | `ProductDetail_VariantsIncludeDefaultWarehouseInfo` | GET /products/{id} detail → Variant DTOs inside response include warehouse info |

### Results

```
Test Run Successful.
Total tests: 244
     Passed: 244
     Failed: 0
     Skipped: 0

Build: 0 errors, 0 warnings
```

---

## 6) Sample Requests / Responses

### 6.1) Create Product with DefaultWarehouseId

**Request:**
```http
POST /api/v1/products
Authorization: Bearer <token>
Content-Type: application/json

{
  "name": "T-Shirt Premium",
  "description": "Cotton premium t-shirt",
  "category": "Apparel",
  "defaultWarehouseId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

**Response (201 Created):**
```json
{
  "id": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "name": "T-Shirt Premium",
  "description": "Cotton premium t-shirt",
  "category": "Apparel",
  "isActive": true,
  "createdAtUtc": "2026-03-05T18:30:00Z",
  "variantCount": 0,
  "defaultWarehouseId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "defaultWarehouseName": "Main Warehouse"
}
```

### 6.2) GET Product Detail (shows DefaultWarehouse + Variant warehouse info)

**Request:**
```http
GET /api/v1/products/f47ac10b-58cc-4372-a567-0e02b2c3d479
Authorization: Bearer <token>
```

**Response (200 OK):**
```json
{
  "id": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "name": "T-Shirt Premium",
  "description": "Cotton premium t-shirt",
  "category": "Apparel",
  "isActive": true,
  "createdAtUtc": "2026-03-05T18:30:00Z",
  "defaultWarehouseId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "defaultWarehouseName": "Main Warehouse",
  "variants": [
    {
      "id": "d290f1ee-6c54-4b01-90e6-d701748f0851",
      "productId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
      "sku": "SKU-000042",
      "color": "Black",
      "size": "L",
      "retailPrice": 150.00,
      "wholesalePrice": 100.00,
      "isActive": true,
      "barcode": "6280000000042",
      "defaultWarehouseId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "defaultWarehouseName": "Main Warehouse"
    }
  ]
}
```

---

## 7) Manual Verification Steps (Human Gate)

### Prerequisites
1. Start the API server: `cd src/ElshazlyStore.Api && dotnet run`
2. Login to get a token:
```bash
curl -X POST http://localhost:5238/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin@123!"}'
```
Save the `accessToken` from the response.

### Step 1 — Get a warehouse ID
```bash
curl http://localhost:5238/api/v1/warehouses \
  -H "Authorization: Bearer <TOKEN>"
```
Note the `id` of any warehouse (e.g., the one with `isDefault: true`).

### Step 2 — Create a product with DefaultWarehouseId
```bash
curl -X POST http://localhost:5238/api/v1/products \
  -H "Authorization: Bearer <TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{"name":"Manual Test Product","defaultWarehouseId":"<WAREHOUSE_ID>"}'
```
**Verify**: Response includes `defaultWarehouseId` and `defaultWarehouseName`.

### Step 3 — GET the product detail
```bash
curl http://localhost:5238/api/v1/products/<PRODUCT_ID> \
  -H "Authorization: Bearer <TOKEN>"
```
**Verify**: Response includes `defaultWarehouseId` and `defaultWarehouseName` at product level.

### Step 4 — Create a variant for this product
```bash
curl -X POST http://localhost:5238/api/v1/variants \
  -H "Authorization: Bearer <TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{"productId":"<PRODUCT_ID>","color":"Red","size":"XL"}'
```
**Verify**: Response includes `defaultWarehouseId` and `defaultWarehouseName` (read-only, from product).

### Step 5 — GET variant by ID
```bash
curl http://localhost:5238/api/v1/variants/<VARIANT_ID> \
  -H "Authorization: Bearer <TOKEN>"
```
**Verify**: Variant response includes `defaultWarehouseId` and `defaultWarehouseName`.

### Step 6 — GET product detail (check variant inside it)
```bash
curl http://localhost:5238/api/v1/products/<PRODUCT_ID> \
  -H "Authorization: Bearer <TOKEN>"
```
**Verify**: Each variant in the `variants` array has `defaultWarehouseId` and `defaultWarehouseName`.

### Step 7 — Update product to clear warehouse
```bash
curl -X PUT http://localhost:5238/api/v1/products/<PRODUCT_ID> \
  -H "Authorization: Bearer <TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{"defaultWarehouseId":"00000000-0000-0000-0000-000000000000"}'
```
**Verify**: 200 OK. Then GET product → `defaultWarehouseId` is `null`.

### Step 8 — Create product with invalid warehouse ID
```bash
curl -X POST http://localhost:5238/api/v1/products \
  -H "Authorization: Bearer <TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{"name":"Bad Warehouse Test","defaultWarehouseId":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"}'
```
**Verify**: 404 with `WAREHOUSE_NOT_FOUND` in the response body.

---

## 8) Files Changed

| File | Change |
|------|--------|
| `src/ElshazlyStore.Domain/Entities/Product.cs` | Added `DefaultWarehouseId`, `DefaultWarehouse` nav |
| `src/ElshazlyStore.Domain/Common/ErrorCodes.cs` | Added `WarehouseInactive` |
| `src/ElshazlyStore.Infrastructure/Persistence/Configurations/ProductConfiguration.cs` | Added FK, index |
| `src/ElshazlyStore.Infrastructure/Persistence/Migrations/20260305000000_0017_product_default_warehouse.cs` | New migration |
| `src/ElshazlyStore.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs` | Updated snapshot |
| `src/ElshazlyStore.Api/Endpoints/ProductEndpoints.cs` | DTOs + validation + includes |
| `src/ElshazlyStore.Api/Endpoints/VariantEndpoints.cs` | Read-only warehouse fields in DTOs |
| `src/ElshazlyStore.Desktop/Models/Dtos/ProductDto.cs` | Added warehouse fields to DTOs |
| `src/ElshazlyStore.Desktop/Models/Dtos/VariantDto.cs` | Added read-only warehouse fields |
| `tests/ElshazlyStore.Tests/Api/ProductDefaultWarehouseTests.cs` | 11 new tests |

---

## ⛔ STOP — Do not proceed to BACKEND 4 until user approval.
