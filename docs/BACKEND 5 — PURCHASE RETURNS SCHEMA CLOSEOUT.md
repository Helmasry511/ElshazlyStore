# BACKEND 5 — PURCHASE RETURNS SCHEMA CLOSEOUT

**Date:** 2026-03-09
**Status:** COMPLETE

---

## 1. Root Cause

`GET /api/v1/purchase-returns` returned **HTTP 500** with PostgreSQL error:

> `42P01: relation "purchase_returns" does not exist`

The EF Core migration that was supposed to create the `purchase_returns` and `purchase_return_lines` tables — file `20260302050000_0015_purchase_returns.cs` — was **missing its companion `.Designer.cs` file**. Without a Designer file, EF Core does not discover or apply the migration. The same problem affected `20260302100000_0016_ret4_perf_indexes.cs` (GIN trigram indexes and sequences for returns/dispositions).

Additionally, the `AppDbContextModelSnapshot.cs` was never updated to include `PurchaseReturn`, `PurchaseReturnLine`, `InventoryDisposition`, or `InventoryDispositionLine` entities, confirming these migrations were never recognized by the EF Core tooling.

**Summary:** Two orphaned `.cs` migration files (no `.Designer.cs`) meant EF Core silently skipped them. The tables were never created in PostgreSQL.

---

## 2. Migrations Added / Fixed

| Action | File(s) |
|--------|---------|
| **Deleted** (orphaned, no Designer) | `20260302050000_0015_purchase_returns.cs` |
| **Deleted** (orphaned, no Designer) | `20260302100000_0016_ret4_perf_indexes.cs` |
| **Created** (proper migration + Designer + snapshot update) | `20260309011937_0018_purchase_returns_dispositions.cs` + `.Designer.cs` |

The new migration `0018_purchase_returns_dispositions` creates:

- **`purchase_returns`** — header table with FKs to suppliers, purchase_receipts, stock_movements, warehouses, users
- **`purchase_return_lines`** — line items with FKs to product_variants, reason_codes, purchase_returns (CASCADE)
- **`inventory_dispositions`** — header for inventory dispositions with FKs to stock_movements, warehouses, users
- **`inventory_disposition_lines`** — line items with FKs to product_variants, reason_codes, inventory_dispositions (CASCADE)
- All B-tree indexes (unique, FK, status, date, composite)
- GIN trigram indexes on `ReturnNumber` / `DispositionNumber` (from former 0016)
- PostgreSQL sequences: `sales_return_number_seq`, `purchase_return_number_seq`, `disposition_number_seq` (from former 0016)

---

## 3. How to Apply Migrations

Migrations auto-apply on startup via `db.Database.MigrateAsync()` in `Program.cs`.

Manual apply if needed:

```powershell
cd src/ElshazlyStore.Api
dotnet ef database update --project ../ElshazlyStore.Infrastructure
```

---

## 4. Evidence — Successful API Calls

| Endpoint | Method | Status | Notes |
|----------|--------|--------|-------|
| `/api/v1/purchase-returns?page=1&pageSize=25` | GET | **200** | Empty `{"items":[],"totalCount":0,...}` initially |
| `/api/v1/purchase-returns` | POST | **201** | Created draft `PRET-000001`, `totalAmount: 20.00` |
| `/api/v1/purchase-returns/{id}` | GET | **200** | Returns full return with lines |
| `/api/v1/purchase-returns/{id}/post` | POST | **200** | Transitions Draft → Posted |
| `/api/v1/purchase-returns/{id}/void` | POST | **200** | Transitions Draft → Voided (separate draft) |
| `/api/v1/purchase-returns` (list after) | GET | **200** | `totalCount: 2` |

Void on a Posted return correctly returns **409** (`PURCHASE_RETURN_VOID_NOT_ALLOWED_AFTER_POST`) — this is by design.

---

## 5. Build / Test Results

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Test Run Successful.
Total tests: 244
     Passed: 244

OpenAPI drift check: NO DRIFT DETECTED — contract is unchanged
```

---

## 6. Files Changed

| File | Change |
|------|--------|
| `Persistence/Migrations/20260302050000_0015_purchase_returns.cs` | **Deleted** (orphaned) |
| `Persistence/Migrations/20260302100000_0016_ret4_perf_indexes.cs` | **Deleted** (orphaned) |
| `Persistence/Migrations/20260309011937_0018_purchase_returns_dispositions.cs` | **Added** (new proper migration) |
| `Persistence/Migrations/20260309011937_0018_purchase_returns_dispositions.Designer.cs` | **Added** (auto-generated) |
| `Persistence/Migrations/AppDbContextModelSnapshot.cs` | **Updated** (auto-generated, now includes PurchaseReturn + InventoryDisposition entities) |

---

## STOP

Phase BACKEND 5 is complete. Do not proceed to the next phase until user approval.
