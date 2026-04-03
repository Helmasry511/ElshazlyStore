# BACKEND 6A — Inventory Consistency: Purchases / Returns Must Update Stock Balances

## Status: COMPLETE ✅

---

## 1 Problem Statement

Posting a **Purchase Receipt** or **Purchase Return** updated the **Stock Ledger** (movement lines) but silently left **Stock Balances** and Variant quantities stale.
The `/api/v1/stock/balances` endpoint could therefore return quantities that no longer matched the sum of movements in `/api/v1/stock/ledger`.

---

## 2 Root Cause

### Split-Transaction Pattern under NpgsqlRetryingExecutionStrategy

The original posting flow performed three independent auto-committed operations rather than a single atomic unit:

| Step | Actor | Commit Boundary |
|------|-------|-----------------|
| 1. Claim (`Draft → Posted`) | `PurchaseService.PostAsync` → `ExecuteUpdateAsync` | auto-commit ★ |
| 2. Stock movement + balance update | `StockService.PostAsync` (own `Serializable` tx + `strategy.ExecuteAsync`) | own transaction ★ |
| 3. Link movement ID + create `SupplierPayable` | `PurchaseService.PostAsync` → `SaveChangesAsync` | auto-commit ★ |

Under PostgreSQL's `NpgsqlRetryingExecutionStrategy` (`EnableRetryOnFailure(maxRetryCount: 3)`), a serialization conflict in Step 2 could trigger a retry.
Because all three steps shared the same `DbContext` instance, the **ChangeTracker** retained entities from the failed first attempt.
On retry, EF Core tried to insert a **duplicate `StockBalance`** row, violating the unique index `(VariantId, WarehouseId)` and causing the entire posting to fail — leaving Step 1's claim stuck in the `Posted` state with no movement.

The same three-step split existed in `PurchaseReturnService.PostAsync`.

---

## 3 Fix Design (Hybrid Option 1 + Option 2)

### Option 1 — Single Atomic Transaction (Primary Fix)

Wrap **all** side-effects — stock movement, balance update, movement linking, supplier payable — inside a **single** `strategy.ExecuteAsync` + Serializable transaction owned by the purchase/return service.

`StockService.PostAsync` was split into:

- **`PostAsync`** (public) — detects whether the `DbContext` already has an ambient transaction.
  - If `_db.Database.CurrentTransaction is not null` → delegates to `PostCoreAsync` directly (no nested tx/strategy).
  - If no ambient tx → creates its own Serializable transaction with execution strategy (standalone calls still work).
- **`PostCoreAsync`** (private) — all actual posting logic (validate IDs, create movement + lines, update `StockBalance`, `SaveChangesAsync`).

`PurchaseService.PostAsync` and `PurchaseReturnService.PostAsync` now:

1. **Claim** (`ExecuteUpdateAsync` Draft → Posted) — auto-commits outside the transaction as the idempotent concurrency gate.
2. **`strategy.ExecuteAsync`** → `BeginTransactionAsync(Serializable)` → load receipt/return → call `StockService.PostAsync` (detects ambient tx) → link movement → create payable → `SaveChangesAsync` → `CommitAsync`.
3. **On failure** — `RollbackPurchaseClaimAsync` / `RollbackReturnClaimAsync` resets status to Draft.
4. **Accounting** (`CreateInvoiceEntryAsync` / DebitNote ledger entry) — runs **after** commit as a separate concern.

### Option 2 — Movement-Derived Balances (Safety Net)

`GetBalancesAsync` in `StockService` was rewritten with a 3-step approach:

1. **Step 1**: Query `StockBalance` table for structure, pagination, filtering, and sorting (unchanged EF Core query).
2. **Step 2**: Fetch raw `StockMovementLines` for the page's `(VariantId, WarehouseId)` pairs; aggregate **client-side** via LINQ `.GroupBy().ToDictionary()`.
3. **Step 3**: Merge — the quantity returned to callers is the **movement-derived** sum, not the cached `StockBalance.Quantity`.

This guarantees the `/api/v1/stock/balances` endpoint **can never diverge** from the ledger, even if a future bug corrupts the cached balance column.

> **EF Core / SQLite limitation**: Server-side `GroupBy + Sum` and correlated subqueries inside projections failed under the SQLite test provider.
> The 2-step (DB query for structure + client-side aggregation) pattern solved this.

---

## 4 Files Changed

| File | Change Summary |
|------|---------------|
| `src/ElshazlyStore.Infrastructure/Services/StockService.cs` | Split `PostAsync` → `PostAsync` + `PostCoreAsync`; ambient-tx detection; rewrote `GetBalancesAsync` to movement-derived quantities |
| `src/ElshazlyStore.Infrastructure/Services/PurchaseService.cs` | Wrapped stock + payable creation in single `strategy.ExecuteAsync` + Serializable transaction; added `RollbackPurchaseClaimAsync` |
| `src/ElshazlyStore.Infrastructure/Services/PurchaseReturnService.cs` | Same pattern as PurchaseService; single transaction for validation + stock + linking |
| `tests/ElshazlyStore.Tests/Api/InventoryConsistencyTests.cs` | **New file** — 6 integration tests (see §5) |

---

## 5 Tests Added

| # | Test Name | What It Verifies |
|---|-----------|-----------------|
| 1 | `PostPurchase_UpdatesBalancesAndLedger` | Purchase 75 units → balance = 75, ledger has `PurchaseReceipt` entry |
| 2 | `PostPurchaseReturn_SubtractsBalancesAndAddsLedgerEntry` | Seed 200, return 35 → balance = 165, ledger has `PurchaseReturnIssue` = −35 |
| 3 | `VoidPurchaseReturn_PostedReturnCannotBeVoided_BalancesUnchanged` | Posted return → void gets 409, balance unchanged |
| 4 | `ConcurrentPurchasePost_BalanceUpdatedExactlyOnce` | Parallel post → balance = 60 exactly (no double-credit), single ledger entry |
| 5 | `PurchaseThenReturn_BalancesConsistentThroughout` | 0 → purchase 50 → return 15 → balance = 35, correct ledger entries |
| 6 | `LedgerSum_MatchesBalanceEndpoint` | Purchase 120, return 30 → `SUM(ledger)` = balance = 90 |

---

## 6 Build & Test Results

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Passed!  - Failed:     0, Passed:   250, Skipped:     0, Total:   250
```

All 250 tests pass (244 pre-existing + 6 new).

---

## 7 Key Invariant

> **At all times: `GET /api/v1/stock/balances?…` returns quantities equal to `SUM(QuantityDelta)` from `GET /api/v1/stock/ledger?…` for any `(VariantId, WarehouseId)` pair.**

This is enforced by:
- **Write path** (Option 1): Single atomic transaction guarantees movement + balance update succeed or fail together.
- **Read path** (Option 2): Balances are re-derived from movement lines on every query.
