# UI 2.3 — INVENTORY & STOCK + QUANTITY COLUMN IN VARIANTS — CLOSEOUT REPORT

**Phase:** UI 2.3 — **Revision: R6** (Variants Quantity Mode: Net All Warehouses Default + Top Warehouse Filter + Double-Click Details)
**Date:** 2026-03-11
**Status:** ✅ COMPLETE
**Build:** 0 errors, 0 warnings
**Tests:** 250 passed, 0 failed, 0 skipped

---

## 0  R1 Revision Summary

R1 addresses three user-reported issues:

| Issue | Root Cause | Fix |
|-------|-----------|-----|
| Stock Balances shows warehouses but no product/variant data — user confused | No movements exist yet → grid is simply empty with generic "لا توجد بيانات" | Rich empty state with explanation + navigation button to Stock Movements |
| Variant dropdown in Stock Ledger opens upwards | Popup had no explicit `Placement` target anchored to the TextBox | Popup `Placement="Bottom"` with named `PlacementTarget`, `MaxHeight=280` |
| Pickers require pressing Enter — not discoverable | Search triggered only on `KeyBinding Key="Enter"` | Debounced typeahead (250ms, min 2 chars, 5–8 results, cancellation) |

---

## 1  Summary

Four scopes were delivered:

| Scope | Screen / Feature | Description |
|-------|-----------------|-------------|
| **A** | Stock Balances | Read-only paged grid of per-variant per-warehouse quantities with warehouse filter and search |
| **B** | Stock Ledger | Read-only paged grid of movement history with variant picker, warehouse filter, date-range |
| **C** | Stock Movements | Posting form for Opening Balance / Adjustment / Transfer with multi-line entry |
| **D** | Quantity Column | `الكمية` column injected into the existing Variants list, computed from stock balances on page load |

All UI is Arabic-only, RTL-first. Technical fields (SKU, Barcode, dates, numbers) render LTR.

---

## 2  API Endpoints Consumed

| Method | Endpoint | Usage |
|--------|----------|-------|
| `GET` | `/api/v1/stock/balances` | Scope A grid, Scope D quantity column |
| `GET` | `/api/v1/stock/ledger` | Scope B grid |
| `POST` | `/api/v1/stock-movements/post` | Scope C posting |
| `GET` | `/api/v1/warehouses` | Warehouse combo-boxes (all 3 screens) |
| `GET` | `/api/v1/variants` | Variant picker (Scope B, Scope C) |
| `GET` | `/api/v1/variants/{id}` | Batch-resolve variant display names in Ledger (R6) |

No new endpoints were introduced — implementation consumes only what exists in `docs/openapi.json`.

---

## 3  Files Created

| File | Purpose |
|------|---------|
| `Models/Dtos/StockBalanceDto.cs` | DTO mapping `/stock/balances` response items |
| `Models/Dtos/StockLedgerEntryDto.cs` | DTO mapping `/stock/ledger` response items + `TypeDisplay`, `InQuantity`, `OutQuantity` computed props |
| `Models/Dtos/StockMovementRequest.cs` | Request/response DTOs for `POST /stock-movements/post` |
| `ViewModels/StockBalancesViewModel.cs` | PagedListViewModelBase subclass — warehouse filter, search, paging |
| `ViewModels/StockLedgerViewModel.cs` | PagedListViewModelBase subclass — variant picker, warehouse filter, date range, paging |
| `ViewModels/StockMovementsViewModel.cs` | Form ViewModel — movement type selector, reference, notes, multi-line entry, post |
| `Views/Pages/StockBalancesPage.xaml` + `.cs` | RTL page — warehouse ComboBox, search, DataGrid, paging, empty/error/busy states |
| `Views/Pages/StockLedgerPage.xaml` + `.cs` | RTL page — variant picker, warehouse ComboBox, DatePickers, DataGrid (In/Out color-coded), paging |
| `Views/Pages/StockMovementsPage.xaml` + `.cs` | RTL scrollable form — movement type, reference, notes, lines ItemsControl, post button |

---

## 4  Files Modified

| File | Change |
|------|--------|
| `Models/Dtos/VariantDto.cs` | `VariantListDto` now implements `INotifyPropertyChanged`; added `QuantityDisplay` string property (default `"…"`, `JsonIgnore`, fires `PropertyChanged`) |
| `ViewModels/PagedListViewModelBase.cs` | Added `await OnPageLoadedAsync()` callback after items are loaded; added `protected virtual Task OnPageLoadedAsync()` |
| `ViewModels/VariantsViewModel.cs` | Overrides `OnPageLoadedAsync` to batch-fetch stock balances per distinct warehouse and update `QuantityDisplay` on each variant row |
| `ViewModels/MainViewModel.cs` | Added `CanViewStockPost` observable property; added 3 navigation cases (`StockBalances`, `StockLedger`, `StockMovements`) |
| `Views/Pages/VariantsPage.xaml` | Added `الكمية` DataGridTextColumn (Width=80, LTR) after Barcode |
| `Views/MainWindow.xaml` | Replaced single "المخزون" sidebar button with 3 sub-buttons; added 3 DataTemplates for new ViewModels |
| `App.xaml.cs` | Registered 3 new ViewModels as Transient services |
| `Localization/Strings.resx` | Added ~35 Arabic string resources for stock screens |
| `Localization/Strings.cs` | Added corresponding strongly-typed accessors |

---

## 5  Quantity Column — Computation Strategy

**Principle:** Stock = Movements only. Quantity is NEVER stored as a field on VariantListDto from the server.

**Flow:**
1. `VariantsViewModel.LoadPageAsync()` fetches the variants page as normal.
2. After items are populated, `OnPageLoadedAsync()` is invoked (new virtual hook in `PagedListViewModelBase`).
3. Variants are grouped by `DefaultWarehouseId`. Variants without a default warehouse show `"—"`.
4. For each distinct warehouse, a single `GET /api/v1/stock/balances?warehouseId={id}&pageSize=100` call fetches all balances — not one call per row.
5. If multiple pages exist, all pages are fetched sequentially.
6. Results are locally joined to variant rows by `VariantId`. Each variant's `QuantityDisplay` property is set (`N0` formatted or `"0"` if absent).
7. Since `VariantListDto` implements `INotifyPropertyChanged`, the DataGrid cell updates live without re-binding.

**Caching:**
- `ConcurrentDictionary<Guid, (DateTime FetchedAt, Dictionary<Guid, decimal> Balances)>` per warehouse.
- 30-second TTL — avoids redundant calls while paging forward/back quickly.
- Cache is per-ViewModel instance (cleared when navigating away and back).

---

## 6  Permissions

| Permission | Screens |
|------------|---------|
| `STOCK_READ` (`StockRead`) | Stock Balances, Stock Ledger (sidebar visibility via `CanViewStock`) |
| `STOCK_POST` (`StockPost`) | Stock Movements (sidebar visibility via `CanViewStockPost`) |

---

## 7  Movement Types Exposed in UI

Only 3 of the 10 backend movement types are exposed for manual posting (the rest are driven by business operations):

| Enum Value | Arabic Label | Use Case |
|------------|-------------|----------|
| 0 | رصيد افتتاحي | Opening Balance — initial stock setup |
| 4 | تسوية | Adjustment — manual correction |
| 3 | تحويل | Transfer — between warehouses |

---

## 8  Localization Keys Added

```
Nav_StockBalances          = أرصدة المخزون
Nav_StockLedger            = حركة المخزون
Nav_StockMovements         = حركات مخزنية
Stock_BalancesTitle        = أرصدة المخزون
Stock_LedgerTitle          = سجل حركة المخزون
Stock_MovementsTitle       = ترحيل حركة مخزنية
Stock_PostSuccess          = تم ترحيل الحركة بنجاح
Stock_NoDefaultWarehouse   = لا يوجد مخزن افتراضي
Field_Quantity             = الكمية
Field_Warehouse            = المخزن
Field_WarehouseCode        = كود المخزن
Field_Date                 = التاريخ
Field_MovementType         = نوع الحركة
Field_Reference            = المرجع
Field_InQty                = داخل
Field_OutQty               = خارج
Field_DateFrom             = من تاريخ
Field_DateTo               = إلى تاريخ
Field_UnitCost             = تكلفة الوحدة
Field_Reason               = السبب
Field_Notes                = ملاحظات
Action_Post                = ترحيل
Action_ApplyFilters        = تطبيق
Action_ClearFilters        = مسح الفلاتر
Action_AddLine             = إضافة سطر
Action_RemoveLine          = حذف
Validation_LinesRequired   = يجب إضافة سطر واحد على الأقل
Validation_VariantRequired = يجب اختيار صنف
Validation_WarehouseRequired = يجب اختيار مخزن
Validation_QuantityRequired = الكمية يجب أن تكون غير صفرية
Stock_MovementType_OpeningBalance = رصيد افتتاحي
Stock_MovementType_Adjustment     = تسوية
Stock_MovementType_Transfer       = تحويل
Placeholder_SearchVariant  = بحث عن صنف...
Label_SelectedVariant      = الصنف المختار
```

---

## 9  Build & Test Results

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Passed!  - Failed:     0, Passed:   244, Skipped:     0, Total:   244
```

No regressions introduced. All 244 tests pass.

---

## 10  Manual Human Test Script

### Pre-requisites
- At least one active warehouse exists.
- At least one product/variant exists.
- Logged-in user has both `STOCK_READ` and `STOCK_POST` permissions.

### Test A — Stock Balances Screen
1. Click **أرصدة المخزون** in sidebar → screen loads, grid shows all balances (or empty state if no movements posted yet).
2. Select a warehouse from the filter ComboBox → grid reloads showing only that warehouse's balances.
3. Type a SKU fragment in the search box → grid filters by search text.
4. Clear search → grid resets.
5. Verify paging controls work (forward/back/page size).
6. Verify RTL layout: Arabic labels right-aligned, SKU/Barcode/numbers left-aligned.

### Test B — Stock Ledger Screen
1. Click **حركة المخزون** in sidebar → screen loads (empty until a variant is selected).
2. Type in the variant search box → dropdown shows matching variants.
3. Select a variant → `الصنف المختار` label appears with variant name.
4. Select a warehouse → click **تطبيق** → grid shows ledger entries filtered by variant + warehouse.
5. Set date range → click **تطبيق** → results narrow to date range.
6. Click **مسح الفلاتر** → all filters clear, grid reloads.
7. Verify In (green) and Out (red) quantity columns display correctly.
8. Verify paging.

### Test C — Stock Movements Posting
1. Click **حركات مخزنية** in sidebar → posting form appears.
2. Select movement type: **رصيد افتتاحي**.
3. Type a reference string (e.g., `INV-001`).
4. Click **إضافة سطر** → a new line card appears.
5. In the line: search and select a variant, select a warehouse, enter quantity `100`, optionally enter unit cost.
6. Add a second line with a different variant.
7. Click **ترحيل** → success message appears, form resets.
8. Go to Stock Balances screen → verify quantities appear for the posted variants.
9. Go to Stock Ledger screen → verify ledger entries appear for the posted variants.
10. Test validation: try posting with no movement type / no lines / zero quantity → error messages appear.

### Test D — Quantity Column in Variants List
1. Navigate to **الأصناف** (Variants list).
2. **الكمية** column should appear after Barcode.
3. Variants with a default warehouse show a loading indicator (`…`) then a number (e.g., `100`).
4. Variants without a default warehouse show `—`.
5. Navigate to page 2 and back → quantities load correctly (cache invalidates after 30 seconds).
6. Post a movement via the Stock Movements screen, wait 30+ seconds, return to Variants list → quantity reflects the new movement.

### Test E — Permissions
1. Log in with a user that has `STOCK_READ` but NOT `STOCK_POST` → sidebar shows Balances and Ledger buttons but NOT Movements.
2. Log in with a user that has neither → none of the 3 stock buttons appear.

---

## 11  Architecture Decisions

| Decision | Rationale |
|----------|-----------|
| `INotifyPropertyChanged` on VariantListDto instead of wrapping in a ViewModel | Minimal change — the DTO is only used in one place (Variants grid). Wrapping would require a new class + adapter layer for no added benefit. |
| Virtual `OnPageLoadedAsync` hook vs. making `LoadPageAsync` virtual | `LoadPageAsync` has complex logic (error handling, paging state). A single hook point at the end is safer and doesn't risk subclass mistakes. |
| Per-warehouse batch fetch vs. per-variant fetch | N warehouse calls (usually 1–3) instead of N variant calls (up to 50 per page). Order of magnitude fewer HTTP roundtrips. |
| 30-second cache TTL | Short enough to reflect new movements quickly, long enough to avoid hammering API when user pages back and forth. |
| Only 3 movement types in UI | Other types (PurchaseReceipt, SaleIssue, etc.) are driven by business operations (purchase orders, sales). Manual posting is only for Opening Balance, Adjustment, and Transfer. |

---

## R1.1  Empty-State UX Rules

### Stock Balances Page

| Condition | What Shows |
|-----------|-----------|
| `Items.Count == 0` AND `SearchText` is empty AND no warehouse filter | **Rich empty state:** Arabic message "لا توجد حركات مخزنية بعد. قم بإضافة حركة مخزنية لتظهر الأرصدة هنا." + accent button "فتح حركات مخزنية" → navigates to StockMovements page |
| `Items.Count == 0` AND a search/filter is active | **Generic empty state:** standard EmptyState control ("لا توجد بيانات") |
| `Items.Count > 0` | Normal DataGrid |

**Implementation:**
- New `IsEmptyNoSearch` observable property in `StockBalancesViewModel`, computed in `OnPageLoadedAsync()`.
- `NavigateToMovementsCommand` uses `INavigationService.NavigateTo<StockMovementsViewModel>()`.
- Constructor now accepts `INavigationService` (DI auto-resolves).

### Stock Ledger Page

| Condition | What Shows |
|-----------|-----------|
| No variant selected (`IsVariantSelected == false`) | **Hint panel:** info icon + "اختر صنفًا لعرض الحركة" — DataGrid hidden, paging hidden |
| Variant selected | DataGrid + paging visible (even if results are empty — standard empty state applies) |

---

## R1.2  Typeahead Design

### Debounce Architecture (shared pattern)

```
User types → OnVariantSearchTextChanged (partial method)
  ├─ Cancel previous CancellationTokenSource
  ├─ If text < 2 chars → clear results, return
  ├─ Create new CancellationTokenSource
  └─ Fire-and-forget: DebounceSearchVariantsAsync(query, token)
       ├─ await Task.Delay(250ms, token)  ← debounce
       ├─ If cancelled → return
       └─ SearchVariantsCoreAsync(query, token)
            ├─ GET /api/v1/variants?page=1&pageSize=8&q={query}
            ├─ If cancelled → return (don't update UI)
            └─ Populate VariantSearchResults (5–8 items max)
```

| Parameter | Value |
|-----------|-------|
| Debounce delay | 250ms |
| Minimum characters | 2 |
| Max results | 8 (`pageSize=8`) |
| Cancellation | Previous request cancelled on each new keystroke via `CancellationTokenSource` |
| Trigger | `PropertyChanged` on `VariantSearchText` (no Enter required) |
| Enter key | Still supported as fallback via `SearchVariantsCommand` |

### Applied To

| Screen | Picker | Notes |
|--------|--------|-------|
| Stock Ledger | Variant picker (filter bar) | Debounced typeahead, Popup below TextBox |
| Stock Movements | Variant picker (per-line) | Same debounce, shared search state, `GotFocus` tracks which line is being edited |
| Warehouses | Warehouse picker | Client-side only (fetched once, ≤500 items) — no typeahead needed |

---

## R1.3  OpenAPI-Backed Query Parameters

### Variant Search Endpoint

| Endpoint | `GET /api/v1/variants` |
|----------|------------------------|
| Search param | `q` (string — searches across SKU, ProductName, Barcode) |
| Pagination | `page=1`, `pageSize=8` |
| Sort | `sort=sku` (default, not overridden) |

**Confirmed from `docs/openapi.json`:** The `/api/v1/variants` endpoint accepts `q`, `page`, `pageSize`, `sort` query parameters. The `q` parameter performs a server-side text search.

### Warehouse Endpoint

| Endpoint | `GET /api/v1/warehouses` |
|----------|--------------------------|
| Approach | Fetch all once (`pageSize=500`), client-filter for `IsActive` |
| No typeahead needed | Warehouse count is small enough for a ComboBox |

---

## R1.4  Dropdown Placement Approach

| Property | Value | Rationale |
|----------|-------|-----------|
| `Placement` | `Bottom` | Always opens downward from the TextBox |
| `PlacementTarget` | Named `x:Name` TextBox element | Anchors popup precisely to the search TextBox, not relative to parent |
| `MaxHeight` | `280` (popup), `270` (inner ListBox) | Prevents covering entire screen; shows ~5–7 items before scrolling |
| `MinWidth` | `300` | Ensures suggestions are readable |
| `StaysOpen` | `False` | Closes on click outside |
| `CornerRadius` | `SmallCornerRadius` | Matches app theme |

### RTL/LTR in Suggestion Rows

Each suggestion row displays:
- **Product name** (Arabic, RTL) — top line
- **SKU** (LTR, `FlowDirection="LeftToRight"`) — bottom line, secondary text color
- **Barcode** (LTR, `FlowDirection="LeftToRight"`) — bottom line, shown only if not empty

---

## R1.5  Files Modified in R1

| File | Change |
|------|--------|
| `ViewModels/StockBalancesViewModel.cs` | Added `INavigationService` dependency; added `IsEmptyNoSearch` property + `OnPageLoadedAsync` override + `NavigateToMovementsCommand` |
| `ViewModels/StockLedgerViewModel.cs` | Added `CancellationTokenSource` field; added `OnVariantSearchTextChanged` partial method for debounce; `SearchVariantsCoreAsync` with cancellation; `pageSize` reduced from 10 to 8 |
| `ViewModels/StockMovementsViewModel.cs` | Added `CancellationTokenSource` field; added `OnVariantSearchTextChanged` partial method for debounce; `SearchVariantsCoreAsync` with cancellation; `pageSize` reduced from 10 to 8 |
| `Views/Pages/StockBalancesPage.xaml` | Replaced generic EmptyState with rich empty-state panel (Arabic guidance + nav button) + conditional generic empty state for search-no-results |
| `Views/Pages/StockLedgerPage.xaml` | Removed old standalone Popup; added inline variant typeahead with Popup `Placement="Bottom"`, placeholder hint, RTL/LTR suggestion rows; added hint panel when no variant selected; DataGrid visibility gated on `IsVariantSelected` |
| `Views/Pages/StockMovementsPage.xaml` | Added inline variant typeahead TextBox + Popup with suggestions in each line card; placeholder hint; RTL/LTR suggestion rows |
| `Views/Pages/StockMovementsPage.xaml.cs` | Added `OnLineVariantSearchFocused` handler to track which line is being edited |
| `Localization/Strings.resx` | Added 4 new keys: `Stock_BalancesEmpty`, `Stock_OpenMovements`, `Stock_LedgerSelectHint`, `Stock_VariantSearchHint` |
| `Localization/Strings.cs` | Added 4 corresponding strongly-typed accessors |

---

## R1.6  New Localization Keys (R1)

```
Stock_BalancesEmpty      = لا توجد حركات مخزنية بعد. قم بإضافة حركة مخزنية لتظهر الأرصدة هنا.
Stock_OpenMovements      = فتح حركات مخزنية
Stock_LedgerSelectHint   = اختر صنفًا لعرض الحركة
Stock_VariantSearchHint  = ابدأ الكتابة للبحث عن صنف...
```

---

## R1.7  Build & Test Results

```
dotnet build ElshazlyStore.sln

Build succeeded.
    0 Warning(s)
    0 Error(s)

dotnet test ElshazlyStore.sln

Passed!  - Failed:     0, Passed:   244, Skipped:     0, Total:   244
```

---

## R1.8  Manual Human Test Script

### Pre-requisites
- Logged-in user has `STOCK_READ` and `STOCK_POST` permissions.
- At least one warehouse and one variant exist.

### Test 1 — Stock Balances: Empty State
1. Navigate to **أرصدة المخزون**.
2. If no stock movements have been posted yet:
   - ✅ See empty-state panel with message "لا توجد حركات مخزنية بعد..."
   - ✅ See accent button "فتح حركات مخزنية"
   - ✅ Click button → navigates to Stock Movements page
3. If movements DO exist:
   - ✅ DataGrid shows balances normally.
   - ✅ Search with a non-matching term → generic empty state ("لا توجد بيانات").

### Test 2 — Stock Ledger: Hint + Typeahead
1. Navigate to **حركة المخزون**.
2. ✅ See hint panel: "اختر صنفًا لعرض الحركة" (info icon).
3. ✅ DataGrid is hidden (no confusing empty grid).
4. Click into variant search box → placeholder text "ابدأ الكتابة للبحث عن صنف..." visible.
5. Type 2 characters → after ~250ms, dropdown appears **below** the TextBox with up to 8 suggestions.
6. ✅ Each suggestion shows: **Product name** (Arabic, RTL) on top line, **SKU** (LTR) + **Barcode** (LTR, if present) on bottom line.
7. Click a suggestion → variant is selected, hint panel disappears, DataGrid appears.
8. Ledger entries (if any) are visible. If no entries, standard empty state shows.
9. Click ✕ to clear variant → hint panel reappears.

### Test 3 — Stock Movements: Line-Level Typeahead
1. Navigate to **حركات مخزنية**.
2. Select a movement type, click **إضافة سطر**.
3. In the new line card, click the variant search TextBox.
4. ✅ Placeholder hint visible: "ابدأ الكتابة للبحث عن صنف..."
5. Type first 2 characters of a known variant SKU or name.
6. ✅ After ~250ms, suggestion dropdown appears below the TextBox with up to 8 results.
7. Click a suggestion → variant display text appears, search box hides.
8. Select warehouse, enter quantity, click **ترحيل** → success.

### Test 4 — Dropdown Placement
1. In Stock Ledger, scroll down so that the variant search box is near the bottom of the window.
2. Type to trigger search → ✅ dropdown still opens **downward** (does not flip upward).
3. ✅ Dropdown has a maximum height (does not cover the entire screen).

### Test 5 — Typeahead Cancellation
1. In any variant search box, type 3 characters quickly, then delete and type 3 different characters.
2. ✅ Only the results for the final text appear (no stale/flickering results from the first query).

---

**End of R1**

---

## R2.0  Revision R2 Summary

**Date:** 2026-03-07
**Scope:** Warehouse "الكل" option + Product-Detail variant quantities + Variant picker Color/Size meta + Ledger scope unlock + UI empty-state overlap fix

| Scope | Feature | Description |
|-------|---------|-------------|
| **A** | Warehouse "الكل" | First ComboBox option "جميع المخازن" in Stock Balances and Stock Ledger. Selecting it sends no `warehouseId` → API returns data across all warehouses. |
| **B** | Product Detail quantities | "الكمية" column added to the product details modal's Variants DataGrid. Quantities fetched from `GET /api/v1/stock/balances` (batched, summed per variant across all warehouses). |
| **C** | Variant picker Color/Size | Suggestion rows in Stock Ledger and Stock Movements now show: ProductName + (Color) + [Size] + SKU/Barcode. Selected variant display includes Color/Size. |
| **D** | Ledger scope unlock | `variantId` is **optional** in `/api/v1/stock/ledger` (confirmed in openapi.json). UI now allows warehouse-only queries, date-range-only queries, or any combination. Variant selection is no longer required. |
| **E** | Empty-state overlap fix | Ledger empty state only shows when `FiltersApplied=true AND IsEmpty=true`. Hint panel shows when `FiltersApplied=false`. No more overlapping text. |

**Note — Stock Movements excluded from Scope A:** Stock Movements is a posting form where warehouse is per-line and required — adding "الكل" would be nonsensical. Scope A applies only to the filter-based screens (Balances, Ledger).

---

## R2.1  OpenAPI Contract Evidence — Ledger Scope Decision

**Endpoint:** `GET /api/v1/stock/ledger` (from `docs/openapi.json`, line 2878)

| Parameter | Type | Required | Evidence |
|-----------|------|----------|----------|
| `variantId` | uuid (query) | ❌ **Optional** | No `"required": true` marker in schema |
| `warehouseId` | uuid (query) | ❌ **Optional** | No `"required": true` marker in schema |
| `from` | date-time (query) | ❌ Optional | — |
| `to` | date-time (query) | ❌ Optional | — |
| `page` | int32 (query) | ❌ Optional | Default: 1 |
| `pageSize` | int32 (query) | ❌ Optional | Default: 25 |
| `includeTotal` | bool (query) | ❌ Optional | Default: true |

**Decision:** ✅ **SUPPORTED** — Warehouse-only and date-range-only ledger queries are allowed by the contract. The previous requirement to select a variant was a UI-side restriction, not a contract limitation. R2 removes this restriction.

---

## R2.2  API Endpoints Used (R2 additions)

| Method | Endpoint | Parameters Used | Scope |
|--------|----------|----------------|-------|
| `GET` | `/api/v1/stock/balances` | `q={productName}`, `page`, `pageSize` (no `warehouseId` — sums all) | Scope B: Product detail variant quantities |
| `GET` | `/api/v1/stock/balances` | `warehouseId` omitted when sentinel selected | Scope A: "الكل" in Balances |
| `GET` | `/api/v1/stock/ledger` | `variantId` optional, `warehouseId` optional | Scope D: Ledger scope unlock |
| `GET` | `/api/v1/variants` | `q`, `page=1`, `pageSize=8` (response includes `Color`, `Size`) | Scope C: Variant picker meta |

---

## R2.3  Files Modified in R2

| File | Change |
|------|--------|
| `Models/Dtos/VariantDto.cs` | `VariantDto` now implements `INotifyPropertyChanged`; added `QuantityDisplay` property (same pattern as `VariantListDto`) for product detail modal |
| `ViewModels/StockBalancesViewModel.cs` | Added `AllWarehousesSentinel` (Id=Guid.Empty, Name="جميع المخازن"); inserted as first item in `Warehouses`; `FetchPageAsync` skips filter when Id=Empty; `ClearWarehouseFilter` resets to sentinel; `IsEmptyNoSearch` updated |
| `ViewModels/StockLedgerViewModel.cs` | Added `AllWarehousesSentinel`; added `FiltersApplied` property; `FetchPageAsync` skips warehouse filter for sentinel; `SelectVariant` now includes Color/Size in display; `ApplyFilters` sets `FiltersApplied=true`; `ClearFilters` resets to hint state; `InitializeAsync` no longer calls `LoadPageAsync` (waits for user to apply); added `BuildColorSizeMeta` helper |
| `ViewModels/StockMovementsViewModel.cs` | `SelectVariantForLine` now includes Color/Size in `VariantDisplay`; added `BuildColorSizeMeta` helper |
| `ViewModels/ProductsViewModel.cs` | `ViewDetailsAsync` now calls `LoadVariantQuantitiesAsync` after loading detail; added `LoadVariantQuantitiesAsync` method (batched fetch from `/stock/balances` using product name as `q`, sums by variant, updates `QuantityDisplay`) |
| `Views/Pages/StockLedgerPage.xaml` | Hint panel visibility gated on `FiltersApplied` (not `IsVariantSelected`); DataGrid visibility gated on `FiltersApplied`; EmptyState uses `MultiDataTrigger` (FiltersApplied+IsEmpty+!HasError); variant suggestion template updated with Color/Size TextBlocks; hint text now wraps with MaxWidth |
| `Views/Pages/StockMovementsPage.xaml` | Variant suggestion template updated with Color/Size TextBlocks (same pattern as Ledger) |
| `Views/Pages/ProductsPage.xaml` | Product detail modal: added "الكمية" DataGridTextColumn (QuantityDisplay, Width=80, LTR) between Size and RetailPrice |
| `Localization/Strings.resx` | Updated `Stock_LedgerSelectHint` to "اختر صنفًا أو مخزنًا ثم اضغط بحث"; added `Stock_AllVariants` = "الكل" |
| `Localization/Strings.cs` | Added `Stock_AllVariants` accessor |

---

## R2.4  New/Updated Localization Keys (R2)

```
Stock_LedgerSelectHint   = اختر صنفًا أو مخزنًا ثم اضغط بحث   (updated from R1)
Stock_AllVariants        = الكل                                   (new)
Field_AllWarehouses      = جميع المخازن                          (existing, now used in ComboBox)
```

---

## R2.5  Product Detail Quantity — Computation Strategy

**Principle:** Same as Variants list — Stock = Movements only.

**Flow:**
1. User clicks "تفاصيل" on a product → `ViewDetailsAsync` loads `ProductDetailDto` (includes Variants).
2. After modal opens, `LoadVariantQuantitiesAsync` fires.
3. Fetches `GET /api/v1/stock/balances?q={productName}&page=1&pageSize=200` (narrows results to this product).
4. Groups balance items by `VariantId`, sums `Quantity` across all warehouses.
5. Updates each `VariantDto.QuantityDisplay` via `INotifyPropertyChanged` — DataGrid cells update live.
6. If a variant has no balance entries, displays `"0"`.
7. Handles multiple pages if necessary.

---

## R2.6  Build & Test Results

```
dotnet build ElshazlyStore.sln

Build succeeded.
    0 Warning(s)
    0 Error(s)

dotnet test ElshazlyStore.sln

Passed!  - Failed:     0, Passed:   244, Skipped:     0, Total:   244
```

---

## R2.7  Manual Human Test Script (R2)

### Pre-requisites
- Logged-in user has `STOCK_READ` and `STOCK_POST` permissions.
- At least one warehouse, product, and variant exist.
- Some stock movements posted (for quantity verification).

### Test 1 — Balances: Warehouse "الكل"
1. Navigate to **أرصدة المخزون**.
2. ✅ ComboBox shows "جميع المخازن" as first/default option.
3. ✅ DataGrid shows balances across all warehouses.
4. Select a specific warehouse → click بحث → grid filters to that warehouse only.
5. Click مسح → ComboBox resets to "جميع المخازن", grid shows all warehouses again.

### Test 2 — Product Details: Variant Quantities
1. Navigate to **المنتجات**, click "تفاصيل" on any product.
2. ✅ Modal opens showing product variants in a DataGrid.
3. ✅ "الكمية" column appears between المقاس and سعر التجزئة.
4. ✅ Quantities show "…" briefly (loading) then resolve to numbers (or "0").
5. Close modal, re-open → quantities load again correctly.

### Test 3 — Variant Picker: Color/Size Meta
1. Navigate to **حركة المخزون** (Stock Ledger).
2. Type in the variant search box → dropdown appears.
3. ✅ Suggestion rows show: **ProductName** + **(Color)** + **[Size]** on top line, SKU + Barcode on bottom line.
4. Select a variant → ✅ selected label shows "ProductName (Color / Size) — SKU".
5. Repeat in **حركات مخزنية** (Stock Movements) → same behavior in line-level variant picker.

### Test 4 — Ledger: Warehouse-Only Query
1. Navigate to **حركة المخزون** (Stock Ledger).
2. ✅ Hint panel shown: "اختر صنفًا أو مخزنًا ثم اضغط بحث".
3. Select a specific warehouse (no variant selected).
4. Click بحث → ✅ DataGrid shows ledger entries for that warehouse across all variants.
5. Select "جميع المخازن" + set date range → click بحث → ✅ shows all entries in date range.
6. Click مسح → ✅ hint panel reappears, DataGrid hides.

### Test 5 — Empty State: No Overlap
1. In Stock Ledger, without applying filters:
   - ✅ Only hint panel visible — no empty state or DataGrid shown.
2. Apply filters that return no results:
   - ✅ Empty state shows ("لا توجد بيانات") — no overlap with hint.
3. Apply filters that return results:
   - ✅ DataGrid shows — no hint visible.

---

**⛔ STOP — Do not proceed to UI 2.4 until user approval.**

---

## R3.0  Revision R3 Summary

**Date:** 2026-03-07
**Scope:** Warehouse Live Refresh in Stock Movements + Transfer 400 Fix + Arabic Error Mapping + Warehouse Display Format + Variant Default Warehouse Display

| Scope | Feature | Priority | Description |
|-------|---------|----------|-------------|
| **A** | Warehouse Live Refresh | P0 | "تحديث المخازن" button on Stock Movements page. Warehouses always fetched fresh on page open; manual refresh available. No permanent cache. Newly created active warehouses appear immediately. |
| **B** | Transfer Failure Fix | P0 | Root cause: stale warehouse list + no transfer-specific validation. Fix: client-side validation preventing same-warehouse transfer; `Guid.Empty` blocked from posting; Arabic error messages for warehouse errors with code/name context. |
| **C** | Warehouse Display Format | P1 | All warehouse ComboBoxes (Stock Movements, Stock Ledger, Stock Balances) now show `{Code} — {Name}`. The "جميع المخازن" sentinel (which has no code) shows name only. |
| **D** | Variant Default Warehouse Display | P1 | When selecting a variant in Stock Movements, shows read-only "المخزن الافتراضي للصنف: {name}" hint. If user hasn't picked a warehouse yet, auto-selects the variant's default warehouse as a suggestion. Display-only — not sent in request payload. |

---

## R3.1  Root Cause Analysis — Transfer 400 Error

### Symptom
`POST /api/v1/stock-movements/post` returned HTTP 400 with `WAREHOUSE_NOT_FOUND` ("Warehouse(s) not found or inactive") when posting a Transfer movement.

### Root Cause
Two co-contributing factors:

1. **Stale warehouse list:** The Stock Movements page fetched warehouses **once** on initialization (`IsInitialized` guard). If a user created a new warehouse and then navigated to Stock Movements without a full page reload, the new warehouse would not appear in the dropdown. The user would be forced to select from the stale list.

2. **No transfer-specific client-side validation:** The Transfer movement type (value=3) requires lines with at least two **different** warehouses (source and destination). There was no validation preventing the user from posting a transfer where all lines had the same warehouse. While the backend validated this, the error message was the English string from `StockService.PostAsync` which then went through `ErrorCodeMapper` and produced a generic Arabic message without warehouse context.

3. **`Guid.Empty` not blocked:** If a user somehow had a line with `SelectedWarehouse is null` but bypassed the existing validation, a `Guid.Empty` value could be sent as `warehouseId`, which the backend would reject as not found.

### Fix Applied

| Layer | Change |
|-------|--------|
| **Warehouse loading** | Extracted `LoadWarehousesAsync()` from `InitializeAsync`, called on init AND via new `RefreshWarehousesCommand`. Preserves per-line selections after refresh. |
| **Client validation** | Added `Guid.Empty` check on `SelectedWarehouse.Id`. Added transfer-specific check: at least 2 distinct warehouses required. |
| **Error display** | `EnhanceWarehouseError()` method in ViewModel detects warehouse error messages from `ErrorCodeMapper` and enriches them with the actual warehouse code/name from the posted lines. |
| **Error mapping** | `WAREHOUSE_NOT_FOUND` and `WAREHOUSE_INACTIVE` messages in `ErrorCodeMapper` updated to include guidance: "افحص حالة المخزن من شاشة المخازن." |

### Failing Request Payload (Before Fix)

```json
{
  "type": 3,
  "reference": "TXF-001",
  "notes": null,
  "lines": [
    { "variantId": "aaa...", "warehouseId": "00000000-0000-0000-0000-000000000000", "quantityDelta": -10, "unitCost": null, "reason": null },
    { "variantId": "aaa...", "warehouseId": "bbb...", "quantityDelta": 10, "unitCost": null, "reason": null }
  ]
}
```

The `warehouseId` of `Guid.Empty` (all zeros) was the AllWarehousesSentinel ID that could leak if the dropdown source was stale.

### Fixed Request Payload (After Fix)

```json
{
  "type": 3,
  "reference": "TXF-001",
  "notes": null,
  "lines": [
    { "variantId": "aaa...", "warehouseId": "ccc...", "quantityDelta": -10, "unitCost": null, "reason": null },
    { "variantId": "aaa...", "warehouseId": "ddd...", "quantityDelta": 10, "unitCost": null, "reason": null }
  ]
}
```

Both warehouse IDs are now real, active warehouse GUIDs from the freshly-loaded list.

---

## R3.2  Warehouse Refresh Strategy in Stock Movements

### Anti-Stale Pattern (same approach as Products page in UI 2.2b-R7)

| Aspect | Implementation |
|--------|---------------|
| **On page open** | `InitializeAsync()` → calls `LoadWarehousesAsync()` (first load only, via `IsInitialized` guard) |
| **Manual refresh** | `RefreshWarehousesCommand` → calls `LoadWarehousesAsync()` (always, no guard) |
| **Button** | "تحديث المخازن" in the lines header toolbar, uses `Action_RefreshWarehouses` localization key |
| **Loading indicator** | `IsRefreshingWarehouses` property disables the refresh button during fetch |
| **Selection preservation** | After refresh, per-line `SelectedWarehouse` is restored by matching `Id` against the new list. If a warehouse was deactivated, the line's selection clears (forces user to re-select). |
| **Inactive warehouses** | Filtered out: `Where(w => w.IsActive)`. Inactive warehouses are hidden from the dropdown. |
| **Cache** | None. Always fetches fresh from `/api/v1/warehouses?page=1&pageSize=500`. |

---

## R3.3  Arabic Error Mapping Approach

### Error Flow

```
Backend ProblemDetails (JSON) 
  → ApiClient.TryParseProblemDetails() 
    → ProblemDetails.ToUserMessage() 
      → ErrorCodeMapper.ToArabicMessage(errorCode, detail)
        → ViewModel.EnhanceWarehouseError(arabicMsg)
          → FormError (displayed to user)
```

### Key Error Codes & Arabic Messages

| Error Code | Arabic Message | When |
|------------|---------------|------|
| `WAREHOUSE_NOT_FOUND` | المخزن غير موجود أو غير نشط. افحص حالة المخزن من شاشة المخازن. | Backend can't find warehouseId or it's inactive |
| `WAREHOUSE_INACTIVE` | المخزن غير نشط. افحص حالة المخزن من شاشة المخازن. | Warehouse found but IsActive=false |
| `STOCK_NEGATIVE_NOT_ALLOWED` | الرصيد غير كافٍ — لا يُسمح برصيد سالب | Transfer would make source balance negative |
| `TRANSFER_UNBALANCED` | عملية التحويل غير متوازنة | Transfer lines don't balance (sum ≠ 0) |
| `VARIANT_NOT_FOUND` | الصنف غير موجود | Invalid variant ID |
| `MOVEMENT_EMPTY` | الحركة لا تحتوي على أصناف | No lines in request |

### Enhanced Warehouse Error (ViewModel-level)

When `ErrorCodeMapper` returns the generic warehouse message, `EnhanceWarehouseError()` injects the actual warehouse code/name from the posted lines:

**Example output:** `المخزن WH-01 — مخزن القاهرة غير نشط أو غير موجود. افحص حالة المخزن من شاشة المخازن.`

### Client-Side Validation Messages (prevent server round-trip)

| Condition | Arabic Message |
|-----------|---------------|
| Transfer with all lines on same warehouse | في التحويل، يجب أن يكون المخزن المصدر مختلفاً عن المخزن الوجهة |
| Warehouse is null or Guid.Empty | يجب اختيار المخزن |

---

## R3.4  Warehouse Display Formatting

### Format: `{Code} — {Name}`

| Screen | Component | Before | After |
|--------|-----------|--------|-------|
| Stock Movements | Per-line warehouse ComboBox | `Name` only | `Code — Name` |
| Stock Ledger | Warehouse filter ComboBox | `Name` only | `Code — Name` |
| Stock Balances | Warehouse filter ComboBox | `Name` only | `Code — Name` |

### Implementation

- Added `DisplayText` computed property to `WarehouseDto`: returns `"{Code} — {Name}"` for normal warehouses, or `Name` only when `Code` is empty (i.e., the AllWarehousesSentinel).
- All three XAML ComboBoxes changed from `DisplayMemberPath="Name"` to `DisplayMemberPath="DisplayText"`.
- The "جميع المخازن" sentinel has `Code = ""`, so `DisplayText` returns `"جميع المخازن"` (unchanged).

---

## R3.5  Variant Default Warehouse Display

### Behavior

When user selects a variant in a Stock Movement line:

1. `DefaultWarehouseDisplay` is set to `"المخزن الافتراضي للصنف: {DefaultWarehouseName}"` if the variant has a default warehouse name from the backend.
2. If no default warehouse, the hint line is hidden.
3. If the user hasn't selected a warehouse for the line yet, the variant's `DefaultWarehouseId` is matched against the active Warehouses list and auto-selected (as a suggestion — user can change).
4. The default warehouse info is **display-only** — it is NOT sent in the POST request payload. The `WarehouseId` in the request comes exclusively from `SelectedWarehouse.Id`.

### Data Source

`VariantListDto` already has `DefaultWarehouseId` and `DefaultWarehouseName` properties (added in UI 2.2b-R6), populated by the `GET /api/v1/variants` endpoint.

---

## R3.6  Files Modified in R3

| File | Change |
|------|--------|
| `Models/Dtos/WarehouseDto.cs` | Added `DisplayText` computed property — `"{Code} — {Name}"` or `Name` only if Code is empty |
| `Models/ErrorCodeMapper.cs` | Updated `WAREHOUSE_NOT_FOUND` and `WAREHOUSE_INACTIVE` Arabic messages to include guidance about checking warehouse status |
| `ViewModels/StockMovementsViewModel.cs` | Extracted `LoadWarehousesAsync()` with selection preservation; added `RefreshWarehousesCommand`; added `IsRefreshingWarehouses`; added `DefaultWarehouseDisplay` on `MovementLineVm`; added `Guid.Empty` check on WarehouseId; added transfer same-warehouse validation; added `EnhanceWarehouseError()` method; `SelectVariantForLine` now sets default warehouse display + auto-selects default warehouse |
| `Views/Pages/StockMovementsPage.xaml` | Added "تحديث المخازن" button in lines header; warehouse ComboBox `DisplayMemberPath` changed to `DisplayText`; added default warehouse hint TextBlock after warehouse ComboBox |
| `Views/Pages/StockLedgerPage.xaml` | Warehouse ComboBox `DisplayMemberPath` changed to `DisplayText` |
| `Views/Pages/StockBalancesPage.xaml` | Warehouse ComboBox `DisplayMemberPath` changed to `DisplayText` |
| `Localization/Strings.resx` | Added 3 new keys: `Validation_TransferSameWarehouse`, `Stock_VariantDefaultWarehouse`, `Stock_WarehouseInactiveOrMissing` |
| `Localization/Strings.cs` | Added 3 corresponding strongly-typed accessors |

---

## R3.7  New Localization Keys (R3)

```
Validation_TransferSameWarehouse = في التحويل، يجب أن يكون المخزن المصدر مختلفاً عن المخزن الوجهة
Stock_VariantDefaultWarehouse    = المخزن الافتراضي للصنف: {0}
Stock_WarehouseInactiveOrMissing = المخزن {0} — {1} غير نشط أو غير موجود. افحص حالة المخزن من شاشة المخازن.
```

---

## R3.8  Build & Test Results

```
dotnet build ElshazlyStore.sln

Build succeeded.
    0 Warning(s)
    0 Error(s)

dotnet test ElshazlyStore.sln

Passed!  - Failed:     0, Passed:   244, Skipped:     0, Total:   244
```

No regressions introduced.

---

## R3.9  Manual Human Test Script (R3)

### Pre-requisites
- Logged-in user has `STOCK_READ` and `STOCK_POST` permissions.
- At least two active warehouses exist.
- At least one product/variant exists with a default warehouse set.
- Some stock movements posted (for transfer testing).

### Test 1 — Warehouse Live Refresh
1. Navigate to **المخازن** (Warehouses) → create a new warehouse (e.g., Code=`WH-NEW`, Name=`مخزن جديد`, Active=✅).
2. Navigate to **حركات مخزنية** (Stock Movements).
3. Click **إضافة سطر** → in the warehouse dropdown:
   - ✅ If the page was already open: the new warehouse may NOT appear yet.
4. Click **تحديث المخازن** button in the toolbar.
5. ✅ The new warehouse `WH-NEW — مخزن جديد` appears in the warehouse dropdown.
6. ✅ Any previously selected warehouses on other lines are preserved.

### Test 2 — Transfer Between Two Active Warehouses
1. In **حركات مخزنية**, select movement type **تحويل** (Transfer).
2. Add 2 lines:
   - Line 1: Select a variant, select **Warehouse A**, quantity = `-10`
   - Line 2: Same variant, select **Warehouse B** (different from A), quantity = `+10`
3. Click **ترحيل**.
4. ✅ POST succeeds — success message "تم ترحيل الحركة بنجاح" appears.
5. Navigate to **أرصدة المخزون** → verify balances reflect the transfer.
6. Navigate to **حركة المخزون** (Ledger) → verify ledger entries show the transfer.

### Test 3 — Transfer Same-Warehouse Validation
1. Select movement type **تحويل**.
2. Add 2 lines — both with **same warehouse**.
3. Click **ترحيل**.
4. ✅ Error: "في التحويل، يجب أن يكون المخزن المصدر مختلفاً عن المخزن الوجهة" — no server request sent.

### Test 4 — Deactivated Warehouse Error
1. Deactivate a warehouse (e.g., `WH-OLD`).
2. In Stock Movements, if `WH-OLD` is still in a line's dropdown (from before refresh):
   - Click **ترحيل** with that warehouse.
3. ✅ Error message in Arabic referencing the warehouse code/name — NOT raw English.
4. Click **تحديث المخازن** → `WH-OLD` disappears from the dropdown (filtered out).

### Test 5 — Warehouse Display Format (Code — Name)
1. Navigate to **حركات مخزنية** → add a line → open warehouse dropdown.
   - ✅ Display: `WH-01 — مخزن القاهرة` (Code — Name format).
2. Navigate to **أرصدة المخزون** → warehouse filter dropdown.
   - ✅ First option: `جميع المخازن` (no code prefix — sentinel).
   - ✅ Other options: `WH-01 — مخزن القاهرة` format.
3. Navigate to **حركة المخزون** (Ledger) → warehouse filter dropdown.
   - ✅ Same format as Stock Balances.

### Test 6 — Variant Default Warehouse Hint
1. In **حركات مخزنية**, add a line.
2. Search and select a variant that has a default warehouse.
3. ✅ Below the warehouse dropdown, a hint shows: "المخزن الافتراضي للصنف: مخزن القاهرة".
4. ✅ The default warehouse is auto-selected in the dropdown (if user hadn't chosen one yet).
5. User can change the warehouse — the hint remains but the selection changes.
6. Select a variant without a default warehouse → ✅ no hint displayed.

### Test 7 — Permissions (unchanged from R2)
1. Log in with a user that has `STOCK_READ` but NOT `STOCK_POST` → sidebar shows Balances and Ledger but NOT Movements.

---

**⛔ STOP — Do not proceed to UI 2.4 until user approval.**

---

## R4.0  Revision R4 Summary

**Date:** 2026-03-09
**Scope:** Transfer UX Model (From/To) + Picker Consistency + Table Auto-Sizing + Dark Row Contrast

| Scope | Feature | Priority | Description |
|-------|---------|----------|-------------|
| **A** | Transfer From/To UX Model | P0 | Complete Transfer UX redesign: each transfer line now shows explicit "المخزن المصدر" (From) and "المخزن الوجهة" (To) pickers. ViewModel auto-generates the 2 backend lines per UI line (negative from source, positive to destination). Eliminates the old confusing 2-manual-line model. |
| **B** | Warehouse Picker Consistency | P0 | All warehouse pickers (single, From, To) bind to the same refreshable `Warehouses` collection. `RefreshWarehousesCommand` preserves From/To selections. Newly created warehouses appear after page open or refresh click. |
| **C** | Ledger Filter Behavior | P1 | Hint text updated to "اختر مخزنًا أو صنفًا أو نطاق تاريخ ثم اضغط بحث — جميع الفلاتر اختيارية". All filters optional — warehouse-only, date-only, or combined query supported without requiring a variant. |
| **D** | DataGrid Column Auto-Sizing | P1 | SKU columns fixed at 130px, Barcode columns fixed at 140px, product name columns use `2*` star sizing. Across VariantsPage, StockBalancesPage, StockLedgerPage. Prevents excessively wide technical columns. |
| **E** | Dark Theme Row Contrast | P0 | Fixed trigger ordering in `ThemedDataGridRowStyle`: alternation first, then hover, then selected. Selection and hover now always override alternating row background. Added explicit `Foreground` on row and `TextElement.Foreground` propagation in cell template. In/Out quantity colors changed from hardcoded `Green`/`Red` to themed `SuccessBrush`/`ErrorBrush`. |

---

## R4.1  Transfer UX Model — Why From/To

### The Problem (R3 Model)

The R3 model required the user to **manually create 2 lines** for a transfer:
- Line 1: Variant A → Warehouse X → Quantity `-10`
- Line 2: Variant A → Warehouse Y → Quantity `+10`

**Issues:**
1. User confusion: must remember to create a matching pair with opposite signs.
2. Validation checked `distinct warehouses across ALL lines` — if user created 1 line only, got a generic error.
3. If user selected 2 different warehouses but same variant in both lines, and both happened to have the same warehouse selected, got "same warehouse" error even though they intended different warehouses (the bug in the user's screenshot).
4. "Variant default warehouse" was auto-selected as a convenience, but for transfers the default warehouse should be the **source** (where stock currently is), not the destination.

### The Fix (R4 Model)

Each transfer line now captures the full transfer intent in ONE card:

```
┌─────────────────────────────────────────────────┐
│ Variant: ProductName (Color/Size) — SKU         │
│ المخزن المصدر:  [Warehouse X ▾] (searchable)    │
│ المخزن الوجهة:  [Warehouse Y ▾] (searchable)    │
│ الكمية: 10    التكلفة: —    السبب: —            │
└─────────────────────────────────────────────────┘
```

**ViewModel auto-generates 2 backend lines:**

| Backend Line | WarehouseId | QuantityDelta |
|-------------|-------------|---------------|
| Outbound | FromWarehouse.Id | `-abs(qty)` |
| Inbound | ToWarehouse.Id | `+abs(qty)` |

User only enters a positive quantity — the sign convention is handled automatically.

### Validation

| Rule | Arabic Error |
|------|-------------|
| From or To is null/empty | يجب اختيار المخزن المصدر والمخزن الوجهة في كل سطر تحويل |
| From == To | في التحويل، يجب أن يكون المخزن المصدر مختلفاً عن المخزن الوجهة |
| From or To is Guid.Empty | يجب اختيار المخزن المصدر والمخزن الوجهة في كل سطر تحويل |

### Non-Transfer Mode

For Opening Balance and Adjustment, the UI continues to show a single "المخزن" picker per line (unchanged).

`IsTransferMode` property on the ViewModel drives XAML visibility toggling between single-warehouse and From/To layouts.

---

## R4.2  Successful Transfer Payload Example

**UI Input:**
- Movement Type: تحويل (3)
- Reference: TXF-001
- Line 1: Variant `aaa-111-222`, From=`WH-01 — مخزن القاهرة`, To=`WH-02 — مخزن الإسكندرية`, Qty=10

**Generated POST Payload:**

```json
{
  "type": 3,
  "reference": "TXF-001",
  "notes": null,
  "lines": [
    {
      "variantId": "aaa-111-222-...",
      "warehouseId": "ccc-from-warehouse-guid",
      "quantityDelta": -10,
      "unitCost": null,
      "reason": null
    },
    {
      "variantId": "aaa-111-222-...",
      "warehouseId": "ddd-to-warehouse-guid",
      "quantityDelta": 10,
      "unitCost": null,
      "reason": null
    }
  ]
}
```

**Note:** Debug logging (`Debug.WriteLine`) emits the full serialized JSON for every transfer POST, visible in Visual Studio Output window under Debug category.

---

## R4.3  Warehouse Refresh Consistency Proof

| Event | Warehouse list state | From/To preserved? |
|-------|---------------------|-------------------|
| Page open (first load) | Fetched fresh from `/api/v1/warehouses?page=1&pageSize=500` | N/A (no lines yet) |
| Click "تحديث المخازن" | Re-fetched. `Warehouses.Clear()` + repopulate with active only. | ✅ All 3 selection fields (`SelectedWarehouse`, `SelectedFromWarehouse`, `SelectedToWarehouse`) restored by matching `Id` |
| Warehouse deactivated between refreshes | Removed from list. Line selection clears → user forced to re-select. | N/A (deactivated, correct behavior) |
| New warehouse created between refreshes | Appears in list after refresh click. | ✅ Existing selections unaffected |

**All pickers bind to the same `Warehouses` ObservableCollection** — no hidden stale copy. Single source of truth.

---

## R4.4  DataGrid Sizing Defaults

### Column Width Rule

| Column Type | Width | Rationale |
|-------------|-------|-----------|
| SKU | `130` (fixed px) | ~14 digits @ 13px font. Prevents star expansion taking excessive space. |
| Barcode | `140` (fixed px) | ~14 digits + slight padding for longer EAN-13 barcodes. |
| Product Name | `2*` (star) | Takes priority over other star columns — name is the primary visual identifier. |
| Quantity / Status / Code | `80`–`100` (fixed px) | Small, numeric columns with bounded content. |
| Actions | `160`–`220` (fixed px) | Button columns, fixed to fit content. |
| Reference / Category | `*` (star) | Flexible text columns take remaining space. |

### Pages Updated

| Page | Column | Before | After |
|------|--------|--------|-------|
| VariantsPage | SKU | `*` | `130` (+ LTR ElementStyle) |
| VariantsPage | Product Name | `*` | `2*` |
| VariantsPage | Barcode | `*` | `140` (+ LTR ElementStyle) |
| StockBalancesPage | SKU | `*` | `130` |
| StockBalancesPage | Barcode | `*` | `140` |
| StockBalancesPage | Product Name | `*` | `2*` |
| StockLedgerPage | SKU | `120` | `130` |

---

## R4.5  Dark Theme Contrast Fix

### Root Cause

The `ThemedDataGridRowStyle` had trigger ordering: `IsMouseOver → IsSelected → AlternationIndex=1`. In WPF, when multiple triggers match simultaneously, the **last** matching trigger wins. This meant:

- On an alternating row (AlternationIndex=1) that was also selected, the alternation trigger (last) overrode the selection highlight.
- Result: Selected alternating rows kept the very dark `SidebarBackgroundBrush` (#181825) instead of the intended `NavItemActiveBrush` (#45475A).
- Same issue with hover: alternation overrode hover highlight.

### Fix

**Trigger reordering:** Alternation first → Hover → Selected (last wins = correct).

```xml
<!-- BEFORE (broken) -->
IsMouseOver → IsSelected → AlternationIndex=1

<!-- AFTER (fixed) -->
AlternationIndex=1 → IsMouseOver → IsSelected
```

### Additional Fixes

| Change | Rationale |
|--------|-----------|
| Row: Added `Foreground="{DynamicResource PrimaryTextBrush}"` | Explicit baseline ensures text doesn't inherit from framework default (black) |
| Row: IsSelected trigger adds `Foreground=PrimaryTextBrush` | Keeps text readable on selection background |
| Cell: `ContentPresenter TextElement.Foreground="{TemplateBinding Foreground}"` | Forces foreground propagation through the cell's ControlTemplate to the actual TextBlock content |
| Ledger In/Out: `Green` → `{DynamicResource SuccessBrush}`, `Red` → `{DynamicResource ErrorBrush}` | Theme-aware colors: light theme uses dark green/red, dark theme uses pastel green/pink |

### Theme Color Contract

| Element | Light Theme | Dark Theme | Contrast |
|---------|-------------|------------|----------|
| Normal row bg | Transparent → Card #FFFFFF | Transparent → Card #2A2A3D | ✅ |
| Alternating row bg | Sidebar #E6E9EF | Sidebar #181825 | ✅ |
| Hover row bg | NavHover #DCE0E8 | NavHover #313244 | ✅ |
| Selected row bg | NavActive #CCD0DA | NavActive #45475A | ✅ |
| Text (normal) | Primary #4C4F69 | Primary #CDD6F4 | ✅ |
| Text (selected) | Accent #1E66F5 | Accent #89B4FA | ✅ |
| In quantity | Success #40A02B | Success #A6E3A1 | ✅ |
| Out quantity | Error #D20F39 | Error #F38BA8 | ✅ |

---

## R4.6  Files Modified in R4

| File | Change |
|------|--------|
| `ViewModels/StockMovementsViewModel.cs` | Added `IsTransferMode` computed property; `OnSelectedMovementTypeChanged` partial method fires `IsTransferMode` change; `SelectVariantForLine` pre-selects From warehouse for transfer; `PostAsync` redesigned: generates 2 backend lines per UI transfer line with abs(qty) sign convention; debug logging via `Debug.WriteLine`; validation per-line From/To for transfers, single warehouse for non-transfers; `EnhanceWarehouseError` checks From/To warehouse info; `LoadWarehousesAsync` preserves From/To/single selections |
| `ViewModels/StockMovementsViewModel.cs` (MovementLineVm) | Added `SelectedFromWarehouse` and `SelectedToWarehouse` observable properties |
| `Views/Pages/StockMovementsPage.xaml` | Warehouse section replaced: single picker shown when `IsTransferMode=false`, From/To picker pair (searchable via `IsEditable+IsTextSearchEnabled`) shown when `IsTransferMode=true`. Both bind to `DataContext.Warehouses`. |
| `Views/Pages/StockLedgerPage.xaml` | Hint text changed to `Stock_LedgerHintAllFilters`; In qty Foreground → `SuccessBrush`; Out qty Foreground → `ErrorBrush`; SKU width 120→130 |
| `Views/Pages/StockBalancesPage.xaml` | SKU width `*`→`130`; Barcode width `*`→`140`; ProductName width `*`→`2*` |
| `Views/Pages/VariantsPage.xaml` | SKU width `*`→`130` + LTR ElementStyle; Barcode width `*`→`140` + LTR ElementStyle; ProductName width `*`→`2*` |
| `Resources/Themes/DataGridStyles.xaml` | Row style: reordered triggers (Alternation→Hover→Selected); added `Foreground` setter on row and IsSelected trigger; Cell style: added `TextElement.Foreground` on ContentPresenter |
| `Localization/Strings.resx` | Added 4 keys: `Field_FromWarehouse`, `Field_ToWarehouse`, `Validation_TransferFromToRequired`, `Stock_LedgerHintAllFilters` |
| `Localization/Strings.cs` | Added 4 corresponding strongly-typed accessors |

---

## R4.7  New Localization Keys (R4)

```
Field_FromWarehouse              = المخزن المصدر
Field_ToWarehouse                = المخزن الوجهة
Validation_TransferFromToRequired = يجب اختيار المخزن المصدر والمخزن الوجهة في كل سطر تحويل
Stock_LedgerHintAllFilters       = اختر مخزنًا أو صنفًا أو نطاق تاريخ ثم اضغط بحث — جميع الفلاتر اختيارية
```

---

## R4.8  Build & Test Results

```
dotnet build ElshazlyStore.sln

Build succeeded.
    0 Warning(s)
    0 Error(s)

dotnet test ElshazlyStore.sln

Passed!  - Failed:     0, Passed:   244, Skipped:     0, Total:   244
```

No regressions introduced.

---

## R4.9  Manual Human Test Script (R4)

### Pre-requisites
- Logged-in user has `STOCK_READ` and `STOCK_POST` permissions.
- At least two active warehouses exist.
- At least one product/variant exists with stock in at least one warehouse.
- Both light and dark themes available for contrast testing.

### Test 1 — Transfer: From/To Warehouse Pickers
1. Navigate to **حركات مخزنية** (Stock Movements).
2. Select movement type **تحويل** (Transfer).
3. Click **إضافة سطر**.
4. ✅ The line card shows TWO warehouse pickers: "المخزن المصدر" and "المخزن الوجهة".
5. ✅ Both pickers are searchable (type to filter).
6. Select a variant → ✅ the variant's default warehouse pre-selects as "المخزن المصدر".
7. Pick a **different** warehouse for "المخزن الوجهة".
8. Enter quantity (e.g., `5`).
9. Click **ترحيل** → ✅ POST succeeds. Success message appears.
10. Navigate to **أرصدة المخزون** → verify source warehouse decreased, destination increased.

### Test 2 — Transfer: Same Warehouse Validation
1. Select movement type **تحويل**, add a line.
2. Set both From and To to the **same** warehouse.
3. Click **ترحيل**.
4. ✅ Error: "في التحويل، يجب أن يكون المخزن المصدر مختلفاً عن المخزن الوجهة" — no server request.

### Test 3 — Transfer: Missing Warehouse Validation
1. Select movement type **تحويل**, add a line.
2. Select a variant, leave From warehouse empty.
3. Click **ترحيل**.
4. ✅ Error: "يجب اختيار المخزن المصدر والمخزن الوجهة في كل سطر تحويل".

### Test 4 — Non-Transfer: Single Warehouse Picker
1. Select movement type **رصيد افتتاحي** (Opening Balance).
2. Add a line.
3. ✅ Only ONE warehouse picker shows: "المخزن" (not From/To).
4. Switch to **تحويل** → ✅ From/To pickers appear.
5. Switch to **تسوية** (Adjustment) → ✅ single warehouse picker returns.

### Test 5 — Warehouse Refresh: Newly Created Warehouse Appears
1. Open a second window or tab, create a new warehouse (e.g., `WH-NEW`).
2. Return to Stock Movements page.
3. Click **تحديث المخازن**.
4. ✅ New warehouse `WH-NEW — مخزن جديد` appears in all warehouse pickers (single, From, To).
5. ✅ Any existing selections on lines are preserved.

### Test 6 — Dark Theme: Row Contrast
1. Toggle to **dark theme** via Settings or toggle button.
2. Navigate to **أرصدة المخزون** or **حركة المخزون** (any page with a DataGrid).
3. ✅ Product/variant names are readable on ALL rows (normal, alternating, hovered, selected).
4. ✅ Alternating rows use a slightly different background but text remains clearly visible.
5. ✅ Hovering over a row changes background — text stays readable.
6. ✅ Selecting a row highlights it differently from alternation — text uses accent color.

### Test 7 — Dark Theme: Ledger In/Out Colors
1. In dark theme, navigate to **حركة المخزون** (Stock Ledger).
2. Apply filters to see movements.
3. ✅ "داخل" (In) quantities show in pastel green (#A6E3A1), readable on dark background.
4. ✅ "خارج" (Out) quantities show in soft pink (#F38BA8), readable on dark background.
5. Switch to light theme → ✅ green/red colors still work (darker green #40A02B, darker red #D20F39).

### Test 8 — Table Auto-Sizing
1. Navigate to **الأصناف** (Variants) → ✅ SKU column is ~130px (not stretched). Barcode ~140px. Product Name takes most space.
2. Navigate to **أرصدة المخزون** → ✅ Same sizing behavior. SKU/Barcode are compact, Product Name is wide.
3. Navigate to **حركة المخزون** (Ledger) → ✅ SKU column is ~130px. Layout is professional on open.
4. ✅ No manual column resizing needed for a readable default layout.

### Test 9 — Ledger: All-Filters-Optional
1. Navigate to **حركة المخزون** (Stock Ledger).
2. ✅ Hint text: "اختر مخزنًا أو صنفًا أو نطاق تاريخ ثم اضغط بحث — جميع الفلاتر اختيارية".
3. Select **only** a warehouse (no variant, no date range). Click بحث.
4. ✅ DataGrid shows all movements for that warehouse across all variants.
5. Clear, select **only** a date range. Click بحث.
6. ✅ DataGrid shows all movements in that date range.
7. Clear, click بحث with "جميع المخازن" and no variant.
8. ✅ DataGrid shows all movements (all warehouses, all variants).

---

**⛔ STOP — UI 2.3 R4 complete. Superseded by R6 below.**

---

## R6.0  Revision R6 Summary

R6 implements three presentation-only improvements:

| Area | Change | Requirement |
|------|--------|-------------|
| **Ledger: Product Name column** | New "اسم المنتج/الصنف" column immediately after "التاريخ" — visible only when no specific variant selected | A.1, A.3 |
| **Ledger: Warehouse Code/Name split** | Single "المخزن" column replaced with "كود المخزن" + "اسم المخزن" | A.2 |
| **Movements: Transfer route text** | Per-line read-only "تحويل: من مخزن (...) إلى مخزن (...)" + enriched success message | B.1, B.2 |

No new endpoints. No business logic changes. UI presentation only.

---

## R6.1  Ledger Column Rules

### Product Name Column ("اسم المنتج/الصنف")

| Condition | Column Visible? | Why |
|-----------|----------------|-----|
| No variant filter (user browsing all movements) | ✅ Shown | User needs to identify which product each row belongs to |
| Specific variant selected in filter | ❌ Hidden | Redundant — user already knows the variant |

**Data source:** Ledger entries return only `VariantId` + `Sku` (no `ProductName`). Resolution:

1. After each page load (`OnPageLoadedAsync`), collect distinct `VariantId` values from the current page.
2. Check in-memory cache (`_variantNameCache`). Unknown IDs are batch-resolved via `GET /api/v1/variants/{id}`.
3. The endpoint returns `VariantListDto` with `ProductName`, `Color`, `Size`.
4. Display format: `ProductName (Color / Size)` if meta exists, else `ProductName` alone, else SKU fallback.
5. Cache persists for the lifecycle of the ViewModel — no repeated calls for the same variant.

### Warehouse Code/Name Columns

| Old Column | New Columns |
|-----------|-------------|
| "المخزن" → `WarehouseCode` | "كود المخزن" → `WarehouseCode` (LTR) |
| — | "اسم المخزن" → `WarehouseName` (RTL) |

**Data source:** Ledger entries return `WarehouseId` + `WarehouseCode` but NO `WarehouseName`.

**Mapping approach:** On `OnPageLoadedAsync`, build a lookup `Dictionary<Guid, string>` from `Warehouses` collection (already fetched via `GET /api/v1/warehouses?page=1&pageSize=500` during initialization). Map `entry.WarehouseId → warehouse.Name`. No additional API calls needed.

---

## R6.2  Contract Evidence from openapi.json

### Stock Ledger Response (`GET /api/v1/stock/ledger`)

Fields returned per entry: `MovementId`, `Type`, `Reference`, `PostedAtUtc`, `PostedByUsername`, `VariantId`, `Sku`, `WarehouseId`, `WarehouseCode`, `QuantityDelta`, `UnitCost`, `Reason`.

**Not returned:** `VariantName`, `ProductName`, `WarehouseName` — these are UI-only, resolved locally.

### Variant Resolution (`GET /api/v1/variants/{id}`)

Returns `VariantListDto`: `Id`, `ProductId`, `ProductName`, `Sku`, `Color`, `Size`, `RetailPrice`, `WholesalePrice`, `IsActive`, `Barcode`, `DefaultWarehouseId`, `DefaultWarehouseName`.

**Confirmed:** `ProductName` is available from this endpoint → product name column is fully supported by contract.

### Warehouses (`GET /api/v1/warehouses`)

Returns `WarehouseDto`: `Id`, `Code`, `Name`, `Address`, `IsDefault`, `IsActive`, `CreatedAtUtc`.

**Confirmed:** `Name` is available → warehouse name mapping is fully supported via local join.

---

## R6.3  Transfer Route Text Behavior

### Per-Line Display (Stock Movements form)

When movement type is **تحويل (Transfer)** and both From/To warehouses are selected on a line:

> تحويل: من مخزن ({FromWarehouseName}) إلى مخزن ({ToWarehouseName})

- Appears as read-only `TextBlock` below the From/To warehouse pickers.
- Hidden when either warehouse is not yet selected.
- Updates reactively via `OnSelectedFromWarehouseChanged` / `OnSelectedToWarehouseChanged` partial methods.

### Success Message After Post

On successful transfer post, the info message includes route from the first line:

> تم ترحيل التحويل: من مخزن ({FromWarehouseName}) إلى مخزن ({ToWarehouseName})

Non-transfer movements continue to show the generic "تم ترحيل الحركة بنجاح".

---

## R6.4  Files Modified in R6

| File | Change |
|------|--------|
| `Models/Dtos/StockLedgerEntryDto.cs` | Added `INotifyPropertyChanged`, UI-only `WarehouseName` + `ProductVariantName` properties |
| `ViewModels/StockLedgerViewModel.cs` | Added `ShowProductColumn`, `_variantNameCache`, `OnPageLoadedAsync` override for batch resolution |
| `Views/Pages/StockLedgerPage.xaml` | Added "اسم المنتج/الصنف" column with visibility binding; split warehouse into two columns |
| `ViewModels/StockMovementsViewModel.cs` | Added `TransferRouteDisplay` to `MovementLineVm`, `BuildTransferSuccessMessage`, enriched success msg |
| `Views/Pages/StockMovementsPage.xaml` | Added transfer route `TextBlock` per line card |
| `Localization/Strings.resx` | Added `Field_WarehouseName`, `Field_ProductVariantName`, `Stock_TransferRoute`, `Stock_TransferPostSuccess` |
| `Localization/Strings.cs` | Added corresponding static properties |

---

## R6.5  New Localization Keys (R6)

| Key | Arabic Value |
|-----|-------------|
| `Field_WarehouseName` | اسم المخزن |
| `Field_ProductVariantName` | اسم المنتج/الصنف |
| `Stock_TransferRoute` | تحويل: من مخزن ({0}) إلى مخزن ({1}) |
| `Stock_TransferPostSuccess` | تم ترحيل التحويل: من مخزن ({0}) إلى مخزن ({1}) |

---

## R6.6  Build & Test Results

```
dotnet build => Build succeeded. 0 Warning(s), 0 Error(s)
dotnet test  => Passed! Failed: 0, Passed: 244, Skipped: 0, Total: 244
```

---

## R6.7  Manual Human Test Script (R6)

### Test 1 — Ledger: Product Name Column (All Warehouses)
1. Navigate to **حركة المخزون** (Stock Ledger).
2. Ensure **جميع المخازن** is selected in warehouse filter.
3. Do NOT select a specific variant. Click **بحث**.
4. ✅ Column order after التاريخ: **"اسم المنتج/الصنف"** is visible.
5. ✅ Each row shows a readable product name (e.g., "قميص (أحمر / XL)").
6. ✅ If a variant's product name couldn't be resolved (API error), SKU is shown as fallback.

### Test 2 — Ledger: Product Name Column Hidden (Specific Variant)
1. Navigate to **حركة المخزون** (Stock Ledger).
2. Search and select a specific variant using the variant picker.
3. Click **بحث**.
4. ✅ "اسم المنتج/الصنف" column is **hidden** (not visible in the DataGrid).
5. ✅ All other columns (كود المخزن, اسم المخزن, etc.) are still visible.

### Test 3 — Ledger: Warehouse Code + Name Columns
1. Navigate to **حركة المخزون** and apply any filter. Click **بحث**.
2. ✅ Two warehouse columns visible: **"كود المخزن"** (LTR code) + **"اسم المخزن"** (Arabic name).
3. ✅ Old single "المخزن" column is gone.
4. ✅ Warehouse names match the values from **المخازن** screen.

### Test 4 — Transfer: Route Text Per Line
1. Navigate to **حركات مخزنية** (Stock Movements).
2. Select **تحويل** (Transfer) as movement type.
3. Add a line, select variant, then select **المخزن المصدر** and **المخزن الوجهة**.
4. ✅ Below the pickers, text appears: "تحويل: من مخزن (مخزن رئيسي) إلى مخزن (مخزن فرعي)".
5. ✅ If only one warehouse is selected, text is hidden.
6. Change the "المخزن الوجهة" → ✅ text updates reactively.

### Test 5 — Transfer: Success Message with Route
1. Complete a valid transfer (at least one line with From/To warehouses, variant, quantity).
2. Click **ترحيل**.
3. ✅ Info dialog shows: "تم ترحيل التحويل: من مخزن (...) إلى مخزن (...)".
4. ✅ Non-transfer movement types still show: "تم ترحيل الحركة بنجاح".

### Test 6 — Regression: Existing Ledger Columns
1. Open ledger with filters applied.
2. ✅ All existing columns still work: التاريخ, نوع الحركة, المرجع, SKU, داخل, خارج.
3. ✅ In/Out colors (green/red) still render correctly.
4. ✅ Paging, sort, refresh all work.

---

**⛔ STOP — After R6, UI 2.3 should be considered READY TO CLOSE pending user approval.**

---

## R4 Revision — Variants Quantity Always Up-to-Date (2026-03-11)

### Problem
After posting a Purchase Receipt or Purchase Return, the quantity column in the Variants list (الأصناف) did not refresh. Users had to wait for the 30-second cache TTL or navigate away and back. This made it appear that purchases/returns were not working correctly.

### Solution — 4 Scopes

#### Scope A — Global StockChanged Signal (IStockChangeNotifier)

| File | Change |
|------|--------|
| `Services/IStockChangeNotifier.cs` | **New** — Interface + singleton implementation with `event EventHandler StockChanged` and `void NotifyStockChanged()` |
| `App.xaml.cs` | Registered `IStockChangeNotifier` → `StockChangeNotifier` as singleton in DI |

The notifier contains zero UI logic — it is a pure in-app signal.

#### Scope B — Wiring Points (Operations That Trigger StockChanged)

| ViewModel | Method | Trigger Point |
|-----------|--------|---------------|
| `StockMovementsViewModel` | `PostAsync()` | After successful `POST /api/v1/stock-movements/post` |
| `PurchasesViewModel` | `PostPurchaseAsync()` | After successful `POST /api/v1/purchases/{id}/post` |
| `PurchaseReturnsViewModel` | `PostReturnAsync()` | After successful `POST /api/v1/purchase-returns/{id}/post` |

Each ViewModel received `IStockChangeNotifier` via constructor injection. `NotifyStockChanged()` is called **only after a successful** API response.

#### Scope C — VariantsViewModel Cache Invalidation

| Behavior | Detail |
|----------|--------|
| **Subscription** | `VariantsViewModel` subscribes to `_stockNotifier.StockChanged` in constructor |
| **On signal** | `_balanceCache.Clear()` → `OnPageLoadedAsync()` re-fetches quantities for current page |
| **No page reload** | Only quantities are refreshed; the variant list data is NOT reloaded |
| **TTL preserved** | Normal browsing still uses 30-second cache; StockChanged bypasses TTL immediately |

#### Scope C (Manual) — "تحديث الكميات" Button

| Element | Detail |
|---------|--------|
| **Button** | Added to VariantsPage.xaml toolbar, between "تحديث" (Refresh) and "بحث" (Search) |
| **Label** | `تحديث الكميات` (Refresh Quantities) — Arabic, localized via `Strings.resx` |
| **Command** | `RefreshQuantitiesCommand` → clears `_balanceCache` → calls `OnPageLoadedAsync()` |
| **Effect** | Identical to StockChanged handler — bypasses TTL, refreshes only quantities |

#### Scope D — UX Hint (Quantity = 0)

When a variant's DefaultWarehouse quantity displays "0", the quantity cell shows:
- **Tooltip:** `قد يكون الرصيد في مخزن آخر` ("Balance may be in another warehouse")
- **Visual:** Greyed-out text (`#999999`) to distinguish from loading state

Implemented as a `DataTrigger` on `QuantityDisplay == "0"` in the DataGrid column template.

### Files Changed

| File | Type | Summary |
|------|------|---------|
| `Services/IStockChangeNotifier.cs` | New | Interface + impl — event-based stock change signal |
| `App.xaml.cs` | Modified | Register `IStockChangeNotifier` as singleton |
| `ViewModels/VariantsViewModel.cs` | Modified | Subscribe to StockChanged, add `RefreshQuantitiesCommand`, clear cache on signal |
| `ViewModels/PurchasesViewModel.cs` | Modified | Inject `IStockChangeNotifier`, call `NotifyStockChanged()` after post |
| `ViewModels/PurchaseReturnsViewModel.cs` | Modified | Inject `IStockChangeNotifier`, call `NotifyStockChanged()` after post |
| `ViewModels/StockMovementsViewModel.cs` | Modified | Inject `IStockChangeNotifier`, call `NotifyStockChanged()` after post |
| `Views/Pages/VariantsPage.xaml` | Modified | Add "تحديث الكميات" button + quantity column template with 0-hint |
| `Localization/Strings.resx` | Modified | Add `Action_RefreshQuantities`, `Stock_QuantityOtherWarehouse` |
| `Localization/Strings.cs` | Modified | Add accessor properties for new strings |

### Build & Test Results

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Passed!  - Failed: 0, Passed: 250, Skipped: 0, Total: 250
```

### Manual Test Steps (Human Vision Gate)

#### Test 1 — Auto-refresh after Purchase Post
1. Open الأصناف (Variants) list — note quantity for a variant (e.g., `50`).
2. Navigate to المشتريات (Purchases) → create a purchase for that variant with qty `25`, same warehouse.
3. Click ترحيل (Post) → success message appears.
4. Navigate back to الأصناف:
   - ✅ Quantity shows `75` (50 + 25) **immediately** — no 30-second wait.

#### Test 2 — Auto-refresh after Purchase Return Post
1. Open الأصناف — note quantity (e.g., `75`).
2. Navigate to مرتجعات المشتريات (Purchase Returns) → create a return for `10` units.
3. Post it → success.
4. Navigate back to الأصناف:
   - ✅ Quantity shows `65` (75 - 10) immediately.

#### Test 3 — Auto-refresh after Stock Movement Post
1. Open الأصناف — note quantity.
2. Navigate to حركات المخزون (Stock Movements) → post an Adjustment for `+20`.
3. Navigate back to الأصناف:
   - ✅ Quantity updated immediately.

#### Test 4 — Manual Refresh Button
1. Open الأصناف — quantities displayed.
2. Click "تحديث الكميات" button in toolbar.
3. ✅ Quantities refresh (loading placeholder `…` briefly appears, then updated values).

#### Test 5 — Zero-Quantity Hint
1. Find a variant with quantity = 0 in the list.
2. Hover over the "0" in the الكمية column.
3. ✅ Tooltip shows: `قد يكون الرصيد في مخزن آخر`.
4. ✅ "0" displays in grey (#999999).

#### Test 6 — TTL Still Works for Normal Browsing
1. Open الأصناف — quantities load.
2. Navigate away and back within 30 seconds.
3. ✅ Quantities appear instantly (from cache, no API call).
4. Wait 30+ seconds, navigate away and back.
5. ✅ Quantities re-fetch from API (cache expired).

---

**⛔ STOP — R4 complete. Variants quantity is now always up-to-date after inventory operations.**

---

## R6.0  Revision R6 — Variants Quantity Mode (Net All Warehouses Default) + Top Warehouse Filter + Double-Click Details

**Date:** 2026-03-11
**Scope:** Rework Variants quantity column to show NET total across all warehouses by default, add top-level warehouse filter dropdown, add double-click per-warehouse balance details modal.

| Scope | Feature | Priority | Description |
|-------|---------|----------|-------------|
| **A** | Net All Warehouses Default | P0 | Default quantity = SUM(balance.qty) across ALL warehouses for each variant. Replaces old per-default-warehouse display. Variants without stock show `0` (not `—`). |
| **B** | Top Warehouse Filter | P0 | Dropdown above grid: "جميع المخازن" (default) + active warehouses. On selection, quantity column switches to show ONLY that warehouse's balance. Mode label: "عرض: إجمالي" / "عرض: مخزن محدد". |
| **C** | Double-Click Details Modal | P0 | Double-click a variant row → modal "تفاصيل أرصدة الصنف" shows per-warehouse breakdown (كود المخزن, اسم المخزن, الكمية). Includes "فتح حركة المخزون" navigation button. |
| **D** | StockChanged Invalidation | P0 | Reuses existing `IStockChangeNotifier`. On stock change: clears balance cache → refreshes quantity column for current page. |

---

## R6.1  Locked Design Decisions

| Decision | Detail |
|----------|--------|
| **Default quantity mode** | NET total across ALL warehouses. `QtyNet = SUM(balance.qty WHERE variantId = X)`. No longer shows per-default-warehouse quantity. |
| **Zero display** | Variants with no balance entries show `0` (not `—`). The old "no default warehouse → show dash" logic is removed. |
| **Warehouse filter** | Top-level dropdown, NOT per-row. Switching warehouse refreshes ONLY the quantity column values — does NOT reload the variants list from the API. |
| **Mode label** | Small text showing "عرض: إجمالي" (all warehouses) or "عرض: مخزن محدد" (specific warehouse selected). |
| **Details modal** | Double-click row (not a button). Shows per-warehouse breakdown from cached data. Includes negative quantities if present. |
| **Navigation** | Details modal has "فتح حركة المخزون" button → navigates to Stock Ledger page. |
| **Cache strategy** | Single fetch of ALL balances (no `warehouseId` filter), paginated to completion. Cache serves net totals, per-warehouse filtering, AND details modal — ONE data source for all three. |

---

## R6.2  API Endpoints Used

| Method | Endpoint | Parameters | Usage |
|--------|----------|------------|-------|
| `GET` | `/api/v1/stock/balances` | `page=1&pageSize=200` (no `warehouseId`) | Fetch ALL balances across all warehouses. Paginated if >200 rows. |
| `GET` | `/api/v1/warehouses` | `page=1&pageSize=500` | Load warehouse dropdown (active only, fetched once per ViewModel lifecycle). |

**Contract evidence from `docs/openapi.json`:**

- `/api/v1/stock/balances`: `warehouseId` is optional (no `required: true`). Omitting it returns balances for ALL warehouses. Each row includes: `VariantId`, `Sku`, `ProductName`, `WarehouseId`, `WarehouseCode`, `WarehouseName`, `Quantity`.
- `/api/v1/warehouses`: Returns `WarehouseDto` with `Id`, `Code`, `Name`, `IsActive`.

**No per-row HTTP calls.** A single paginated fetch of `/stock/balances` (without `warehouseId`) provides:
1. Net totals per variant (sum by `VariantId`)
2. Per-warehouse quantity (filter by `WarehouseId`)
3. Per-warehouse breakdown for details modal (filter by `VariantId`)

---

## R6.3  Batching & Caching Approach

### Data Flow

```
VariantsViewModel.OnPageLoadedAsync()
  ├─ LoadWarehousesIfNeededAsync()       → GET /api/v1/warehouses (once)
  └─ HydrateQuantitiesAsync()
       ├─ GetAllBalancesAsync()           → GET /api/v1/stock/balances (paginated, cached)
       └─ For each variant in Items:
            ├─ If "جميع المخازن": QtyNet = SUM(balances WHERE VariantId=X)
            └─ If specific warehouse: Qty = SUM(balances WHERE VariantId=X AND WarehouseId=Y)
```

### Cache Design

| Aspect | Detail |
|--------|--------|
| **Key** | Single nullable tuple: `(DateTime FetchedAt, List<StockBalanceDto> Balances)?` |
| **TTL** | 30 seconds (unchanged from R4) |
| **Scope** | Per-ViewModel instance (cleared when navigating away) |
| **Invalidation** | `StockChanged` event clears cache instantly + re-hydrates quantities |
| **Serves** | Net total display, per-warehouse display, AND details modal — zero extra API calls |
| **Page size** | 200 per page (larger than old 100 — fewer roundtrips for larger datasets) |

### Warehouse Filter Flow

```
User selects warehouse in dropdown → clicks بحث
  └─ FilterByWarehouseAsync()
       ├─ Set all QuantityDisplay = "…" (placeholder)
       └─ HydrateQuantitiesAsync()  ← reuses cached data, no API call
```

### Details Modal Flow

```
User double-clicks variant row
  └─ ShowBalanceDetailsAsync(variant)
       ├─ GetAllBalancesAsync()    ← reuses cache
       ├─ Filter by VariantId
       └─ Populate BalanceDetailItems → modal opens
```

---

## R6.4  StockChanged Invalidation Wiring

**Unchanged from R4.** The `IStockChangeNotifier` pattern remains identical:

| Trigger Point | ViewModel |
|---------------|-----------|
| `POST /api/v1/stock-movements/post` success | `StockMovementsViewModel` |
| `POST /api/v1/purchases/{id}/post` success | `PurchasesViewModel` |
| `POST /api/v1/purchase-returns/{id}/post` success | `PurchaseReturnsViewModel` |

**Handler in `VariantsViewModel`:**
1. `_allBalancesCache = null` (clear cache)
2. `HydrateQuantitiesAsync()` (re-fetch and recompute for current page)

Manual refresh: "تحديث الكميات" button → same behavior (clears cache + re-hydrates).

---

## R6.5  Files Modified

| File | Change |
|------|--------|
| `ViewModels/VariantsViewModel.cs` | **Major rework:** Replaced per-warehouse `ConcurrentDictionary` cache with single `List<StockBalanceDto>` cache. Added `INavigationService` dependency. Added warehouse filter properties (`Warehouses`, `SelectedWarehouse`, `WarehouseFilterModeLabel`). Added balance details modal properties (`IsShowingBalanceDetails`, `BalanceDetailVariantName`, `BalanceDetailItems`). Added commands: `FilterByWarehouseCommand`, `ShowBalanceDetailsCommand`, `CloseBalanceDetailsCommand`, `NavigateToStockLedgerCommand`. Rewrote `OnPageLoadedAsync` → `HydrateQuantitiesAsync` for net/per-warehouse logic. |
| `Views/Pages/VariantsPage.xaml` | Added Grid.Row 2 (warehouse filter row with ComboBox + mode label). Added DataGrid RowStyle with `MouseDoubleClick` EventSetter (BasedOn ThemedDataGridRowStyle). Added balance details modal (dim backdrop + card with per-warehouse DataGrid + navigation buttons). Bumped subsequent Grid.Row indices. |
| `Views/Pages/VariantsPage.xaml.cs` | Added `OnRowDoubleClick` handler (routes to `ShowBalanceDetailsCommand`). Added `using` for `MouseButtonEventArgs` and `VariantListDto`. |
| `Localization/Strings.resx` | Added 4 keys: `Stock_ViewTotal`, `Stock_ViewWarehouse`, `Stock_BalanceDetailsTitle`, `Stock_OpenStockLedger` |
| `Localization/Strings.cs` | Added 4 corresponding strongly-typed accessors |

---

## R6.6  New Localization Keys

| Key | Arabic Value |
|-----|-------------|
| `Stock_ViewTotal` | عرض: إجمالي |
| `Stock_ViewWarehouse` | عرض: مخزن محدد |
| `Stock_BalanceDetailsTitle` | تفاصيل أرصدة الصنف |
| `Stock_OpenStockLedger` | فتح حركة المخزون |

---

## R6.7  Build & Test Results

```
dotnet build ElshazlyStore.sln

Build succeeded.
    0 Warning(s)
    0 Error(s)

dotnet test ElshazlyStore.sln

Passed!  - Failed:     0, Passed:   250, Skipped:     0, Total:   250
```

No regressions introduced.

---

## R6.8  Manual Human Test Script

### Pre-requisites
- Logged-in user has `STOCK_READ` permission.
- At least two active warehouses exist.
- At least one variant has stock in at least one warehouse.
- Some variant has stock in multiple warehouses (for net total verification).

### Test 1 — Default: Net Total Across All Warehouses
1. Navigate to **الأصناف** (Variants list).
2. ✅ Warehouse filter dropdown visible above grid, defaulting to "جميع المخازن".
3. ✅ Mode label shows: "عرض: إجمالي".
4. ✅ Quantity column shows `…` briefly, then NET total (sum across all warehouses) for each variant.
5. ✅ Variants with no stock show `0` (not `—`).
6. ✅ A variant with stock in 2 warehouses (e.g., 30 in WH-01 + 20 in WH-02) shows `50`.

### Test 2 — Select Specific Warehouse
1. From the warehouse dropdown, select a specific warehouse (e.g., `WH-01 — مخزن القاهرة`).
2. Click **بحث** (filter button).
3. ✅ Mode label changes to: "عرض: مخزن محدد".
4. ✅ Quantity column now shows ONLY WH-01's balance for each variant.
5. ✅ A variant with 30 in WH-01 now shows `30` (not the total `50`).
6. ✅ A variant with no stock in WH-01 shows `0`.
7. ✅ The variants list itself did NOT reload (no page reload, no flicker in other columns).

### Test 3 — Switch Back to All Warehouses
1. Select "جميع المخازن" from the dropdown. Click **بحث**.
2. ✅ Quantities revert to NET totals.
3. ✅ Mode label reverts to "عرض: إجمالي".

### Test 4 — Double-Click: Per-Warehouse Balance Details
1. Double-click on any variant row.
2. ✅ Modal opens with title "تفاصيل أرصدة الصنف".
3. ✅ Variant name displayed (e.g., "قميص أبيض (أحمر) [XL]").
4. ✅ Per-warehouse table visible:
   | كود المخزن | اسم المخزن | الكمية |
   |------------|-----------|--------|
   | WH-01 | مخزن القاهرة | 30 |
   | WH-02 | مخزن الإسكندرية | 20 |
5. ✅ If a warehouse has negative balance, it shows (e.g., `-5`).
6. ✅ "فتح حركة المخزون" button visible.

### Test 5 — Navigate to Stock Ledger from Details Modal
1. In the balance details modal, click "فتح حركة المخزون".
2. ✅ Modal closes, navigates to Stock Ledger page.

### Test 6 — Auto-Refresh After Purchase/Return/Movement Post
1. Open الأصناف — note quantity for a variant (e.g., `50`).
2. Navigate to المشتريات → create a purchase for that variant with qty `25`.
3. Post it → success.
4. Navigate back to الأصناف:
   - ✅ Quantity shows `75` (50 + 25) **immediately** — no 30-second wait.
5. ✅ Works for purchase returns and stock movements as well.

### Test 7 — Manual Refresh Button
1. Click "تحديث الكميات" button in toolbar.
2. ✅ Quantities show `…` briefly then update.

### Test 8 — Cache Behavior
1. Open الأصناف — quantities load.
2. Page forward and back within 30 seconds → ✅ quantities appear instantly (from cache).
3. Switch warehouse filter and back → ✅ no extra API call (cache reused).
4. Wait 30+ seconds, page → ✅ quantities re-fetch from API.

### Test 9 — RTL/LTR Correctness
1. ✅ Warehouse dropdown label "المخزن" is right-aligned (RTL).
2. ✅ Warehouse display format: `WH-01 — مخزن القاهرة` (Code — Name).
3. ✅ Mode label "عرض: إجمالي" is right-aligned.
4. ✅ In details modal, warehouse codes (كود المخزن) are LTR, warehouse names are RTL, quantities are LTR.

---

**⛔ STOP — R6 complete. Do not proceed until user approval.**
