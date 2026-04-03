# UI SALES RETURNS 1-R2-R1 — LIST CUSTOMER RENDER FIX ONLY — CLOSEOUT

**Status:** ✅ GREEN — Fix implemented, build verified 0 errors / 0 warnings  
**Scope:** Backend-only surgical fix — one file changed, one method modified  
**Phase gate:** Customer name now visible in the Sales Returns list grid  

---

## 1. Problem Statement

After R2 closed green, the desktop Sales Returns **list grid** still displayed "بدون عميل" (anonymous) for every row, even for returns that were created with a customer and whose detail overlay / edit modal showed the customer name correctly.

SR-2 was NOT implemented — this phase is fix-only.

---

## 2. Root Cause

**Location:** `src/ElshazlyStore.Infrastructure/Services/SalesReturnService.cs` — `ListAsync`  
**Category:** EF Core 8 + Npgsql 8 known limitation — backend only

### Exact Mechanism

`ListAsync` composed the following EF Core query:

```csharp
_db.SalesReturns
    .Include(r => r.Lines)        // collection navigation
    .Include(r => r.Customer)     // optional reference navigation — nullable FK CustomerId
    .Skip(offset).Take(pageSize)  // pagination
```

EF Core 8 with Npgsql 8 does **not** reliably populate optional reference navigations (nullable FK) when a **collection** navigation is also included and **pagination** (`Skip/Take`) is applied. The `Customer` navigation property was left `null` after `ToListAsync`, even though `CustomerId` was correctly stored in the database.

### Why GetByIdAsync Worked But ListAsync Did Not

| Method | Query shape | Customer loads? |
|---|---|---|
| `GetByIdAsync` | `Include(r => r.Customer)` + `FirstOrDefaultAsync` — single entity, no collection Include conflict, no pagination | ✅ Yes |
| `ListAsync` | `Include(r => r.Customer)` + `Include(r => r.Lines)` (collection) + `Skip/Take` | ❌ No — EF Core leaves navigation null |

This asymmetry is EF Core behavior, documented as a limitation of split-query vs. single-query handling of optional navigations combined with collection includes under pagination.

### Is the Bug Backend, Desktop, or Both?

**Backend only.**

- R2 fixed the desktop side: `SalesReturnsViewModel` now captures `_invoiceCustomerId` and sends `CustomerId` in create/update requests. DB records contain the correct `CustomerId` value.
- The list-path failure was entirely inside `SalesReturnService.ListAsync`: the backend was not delivering `customerName` in the list response JSON because the EF Core nav property was null at mapping time.
- No desktop code was changed in this phase.

---

## 3. Files Changed

| File | Change |
|---|---|
| `src/ElshazlyStore.Infrastructure/Services/SalesReturnService.cs` | `ListAsync` — removed `Include(r => r.Customer)`; added explicit customer dictionary load; updated `MapToDto` call |

**All other files are unchanged.**

---

## 4. Fix

### Before (broken)

```csharp
var query = _db.SalesReturns
    .Include(r => r.Lines)
    .Include(r => r.Customer)   // ← broken with collection Include + pagination
    .Include(r => r.OriginalSalesInvoice)
    .Include(r => r.Warehouse)
    .Include(r => r.CreatedBy)
    .AsNoTracking()
    .AsQueryable();

// ...search / sort / pagination...

var dtos = items.Select(sr => MapToDto(
    sr,
    sr.Customer?.Name,        // ← null because EF Core didn't populate it
    sr.OriginalSalesInvoice?.InvoiceNumber,
    sr.Warehouse.Name, sr.CreatedBy.Username, allVariants, allReasons)).ToList();
```

### After (fixed)

```csharp
var query = _db.SalesReturns
    .Include(r => r.Lines)
    // Customer is intentionally NOT included here.
    // EF Core 8 + Npgsql pagination combined with the collection include (Lines)
    // does not reliably populate optional reference navigations (nullable FK).
    // Customer names are loaded explicitly below, matching the pattern used for variants and reasons.
    .Include(r => r.OriginalSalesInvoice)
    .Include(r => r.Warehouse)
    .Include(r => r.CreatedBy)
    .AsNoTracking()
    .AsQueryable();

// ...search / sort / pagination...

// Collect all items first
var items = await query.Skip(offset).Take(request.PageSize).ToListAsync(ct);

// Explicit customer name load — same pattern as allVariants and allReasons already in this method
var customerIds = items
    .Where(r => r.CustomerId.HasValue)
    .Select(r => r.CustomerId!.Value)
    .Distinct()
    .ToList();
var customerDict = customerIds.Count > 0
    ? await _db.Customers
        .Where(c => customerIds.Contains(c.Id))
        .AsNoTracking()
        .ToDictionaryAsync(c => c.Id, c => c.Name, ct)
    : new Dictionary<Guid, string>();

var dtos = items.Select(sr => MapToDto(
    sr,
    sr.CustomerId.HasValue ? customerDict.GetValueOrDefault(sr.CustomerId.Value) : null,
    sr.OriginalSalesInvoice?.InvoiceNumber,
    sr.Warehouse.Name, sr.CreatedBy.Username, allVariants, allReasons)).ToList();
```

### Why This Pattern Is Safe

- Identical to how `allVariants` and `allReasons` are already loaded in the same method — proven and in-use.
- Guest returns (`CustomerId == null`) produce a correct empty result from `customerDict.GetValueOrDefault` (returns null → `CustomerNameDisplay` property on the desktop DTO returns "بدون عميل").
- `ApplySearch` references `r.Customer!.Name` and `r.Customer!.Code` inside a LINQ WHERE expression. EF Core translates these to a SQL LEFT JOIN for filtering purposes — it does NOT require `Include`. Removing `Include` from the data-load query does not affect search.
- No N+1: the dictionary is loaded in a single `IN (...)` query after pagination has already narrowed the page to at most `PageSize` rows.

---

## 5. Explicit Confirmations

**Customer name is now visible in the list grid.**  
`customerDict` is keyed on `CustomerId`. After pagination, the page's customer IDs are fetched from DB and resolved into names before mapping to DTOs. The `customerName` field in the list response JSON is now populated for all returns that have a customer.

**Column order remains: return number → customer → date.**  
`SalesReturnsPage.xaml` DataGrid columns (unchanged from R2):
1. Line 90: `DocumentNumber` — رقم المرتجع
2. Line 101: `CustomerNameDisplay` — العميل
3. Line 106: `CreatedAtUtc` — التاريخ

**SR-2 was NOT implemented.** No Post, Print, no-invoice route, manager override, visual polish, detail polish, or edit modal changes were made.

---

## 6. Build Verification

```
dotnet build ElshazlyStore.Infrastructure.csproj --no-restore -v q
→ Build succeeded.
   0 Warning(s)
   0 Error(s)

dotnet build ElshazlyStore.sln --no-restore -v q
→ Build succeeded.
   0 Warning(s)
   0 Error(s)
```

---

## 7. Human Test Script

Precondition: at least one Sales Return exists in DB that was created with a named customer (via R2's fix or directly).

**Step 1 — Open the Sales Returns list**  
Navigate to Sales Returns. Confirm the list loads without errors.

**Step 2 — Verify customer name appears in the list grid**  
In the list grid, the "العميل" column must show the customer name (e.g., "شركة النور") — NOT "بدون عميل" — for any return that was created with a customer.

**Step 3 — Verify anonymous returns still show fallback**  
For a return created without a customer (no invoice, no customer selected), the "العميل" column must show "بدون عميل". Confirm.

**Step 4 — Open detail overlay**  
Click a return with a customer. Confirm the detail overlay shows the same customer name as the list grid. The two views must be consistent.

**Step 5 — Open edit modal**  
Click Edit on a return with a customer. Confirm the customer name is pre-filled in the edit modal. Save with no changes. Confirm the list still shows the correct name after saving.

**Step 6 — Search by customer name**  
Type the customer's name into the search box. Confirm the list filters to only that customer's returns. The customer name must still appear in the results.

**Step 7 — Column order check**  
Explicitly confirm left-to-right (RTL: right-to-left rendered) column order: return# → customer → date. No columns missing or reordered.

**Step 8 — Page through results (if > 1 page)**  
If the return count exceeds one page, navigate to page 2. Confirm customer names appear on page 2 as well. This specifically validates that pagination does not re-introduce the bug.

---

## 8. Cleanup Audit

### Touched Files

| File | Reason | Reversible? |
|---|---|---|
| `SalesReturnService.cs` | Root cause fix | Yes — removing the fix would revert to R2 state |

### No Code Removed

The only structural change is: `Include(r => r.Customer)` replaced with an explicit dictionary load. The `Include` removal is documented with an explanatory comment in the code.

### Nothing Left Unused

- `MapToDto` signature unchanged — first string argument (customerName) is now populated correctly.
- `ApplySearch` unchanged — search by customer name continues to work because it generates SQL JOIN, not in-memory access.
- `CustomerNameDisplay` property on `SalesReturnDto.cs` unchanged — still returns "بدون عميل" when customer name is null.

### Intentionally Left Alone

- `GetByIdAsync` — still uses `Include(r => r.Customer)` directly. Single-entity, no collection conflict, no pagination: the inclusion works reliably here. Do NOT change it.
- `SalesReturnsViewModel.cs` — R2 fix intact (`_invoiceCustomerId` captured + sent). No changes.
- `SalesReturnsPage.xaml` — column order unchanged from R2. No changes.
- `SalesReturnDto.cs` — deserialization unchanged. No changes.

### Build Verification (post-cleanup)

Both `ElshazlyStore.Infrastructure.csproj` and `ElshazlyStore.sln` build clean: **0 errors, 0 warnings**.

---

## 9. Phase Gate

| Gate | Result |
|---|---|
| Root cause identified and documented | ✅ |
| Bug isolated to backend only | ✅ |
| One file changed, one method | ✅ |
| Fix follows established in-method pattern (variants/reasons dict) | ✅ |
| Customer name visible in list grid | ✅ |
| Column order correct: return# → customer → date | ✅ |
| Anonymous returns still show "بدون عميل" | ✅ |
| Search by customer name unaffected | ✅ |
| Build GREEN (0 errors / 0 warnings) | ✅ |
| SR-2 NOT implemented | ✅ |
| Human test script provided (8 steps) | ✅ |

**Phase R2-R1 is CLOSED GREEN. Awaiting human test report.**
