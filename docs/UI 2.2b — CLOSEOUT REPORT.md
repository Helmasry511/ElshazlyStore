# UI 2.2b-R7 — WAREHOUSE DROPDOWN LIVE REFRESH (NO STALE CACHE) — CLOSEOUT REPORT

**Phase:** UI 2.2b-R7  
**Scope:** Fix stale warehouse cache in Product create/edit dropdown; always reload from backend; add in-modal refresh button  
**Status:** ✅ Complete — 0 errors, 0 warnings, 244/244 tests pass  
**Date:** 2026-03-05  
**Revision:** R7 (supersedes R6)  

---

## R7 Change Summary

### Root Cause

In R6, `LoadWarehousesAsync()` used a `WarehousesLoaded` boolean gate — once warehouses were fetched the first time, they were never reloaded. Creating a new warehouse on the المخازن page would not appear in the Product form dropdown because the cached list was stale.

### Fix Approach: Always Reload + Refresh Button

| Strategy | Implementation |
|----------|---------------|
| **Reload on every modal open** | `LoadWarehousesAsync()` no longer checks `WarehousesLoaded`. Every call to `OpenCreateAsync()` or `OpenEditAsync()` fetches fresh data from `GET /api/v1/warehouses`. |
| **"تحديث المخازن" refresh button** | A button inside the Product form modal calls `LoadWarehousesCommand` — user can manually refresh without closing the modal. |
| **Selection preservation** | After refresh, the previously selected warehouse is re-matched by ID. If the warehouse was deactivated or deleted, selection clears naturally. |
| **Active-only filter** | Kept from R6 — only `IsActive == true` warehouses appear in the dropdown. |

### Changes

| File | Change |
|------|--------|
| `ViewModels/ProductsViewModel.cs` | Removed `WarehousesLoaded` flag. `LoadWarehousesAsync()` now always fetches from backend. Changed from private method to `[RelayCommand]` for XAML binding. Added `IsLoadingWarehouses` property. Added selection-preservation after refresh. |
| `Views/Pages/ProductsPage.xaml` | Added "تحديث المخازن" button beside the ComboBox (disabled while loading). |
| `Localization/Strings.resx` | +1 key: `Action_RefreshWarehouses` |
| `Localization/Strings.cs` | +1 accessor property |

### New Localization Key (R7)

| Key | Arabic Value |
|-----|-------------|
| `Action_RefreshWarehouses` | تحديث المخازن |

---

## Build + Test Results

```
Build succeeded.  0 Warning(s)  0 Error(s)
Passed! - Failed: 0, Passed: 244, Skipped: 0, Total: 244
```

---

## Manual Human Test Script (R7)

### Prerequisites
- Backend running (`dotnet run` in `src/ElshazlyStore.Api`)
- Desktop app running (`dotnet run` in `src/ElshazlyStore.Desktop`)
- Logged in as admin with PRODUCTS_WRITE + WAREHOUSES_WRITE permissions

### Test 1: New Warehouse Appears in Dropdown on Next Open
| Step | Action | Expected |
|------|--------|----------|
| 1 | Navigate to المخازن → click إضافة | Warehouse form modal opens |
| 2 | Enter code + name, click حفظ | New warehouse created (active by default) |
| 3 | Navigate to المنتجات → click إضافة | Product form modal opens |
| 4 | Click "المخزن الافتراضي" ComboBox | ✅ Newly created warehouse is visible and selectable |

### Test 2: Refresh Button Updates List Without Closing Modal
| Step | Action | Expected |
|------|--------|----------|
| 1 | On المنتجات → click إضافة | Product form modal opens |
| 2 | Note the warehouses in ComboBox | Current list shown |
| 3 | **Without closing modal:** open المخازن in another way (or note count) | — |
| 4 | Click "تحديث المخازن" button | ComboBox refreshes with latest data from backend |

### Test 3: Selection Preserved After Refresh
| Step | Action | Expected |
|------|--------|----------|
| 1 | Click إضافة on المنتجات, select a warehouse from ComboBox | Warehouse selected |
| 2 | Click "تحديث المخازن" button | List refreshes |
| 3 | Check ComboBox | ✅ Same warehouse still selected |

### Test 4: Deactivated Warehouse Disappears After Refresh
| Step | Action | Expected |
|------|--------|----------|
| 1 | Go to المخازن, deactivate a warehouse | Warehouse set to inactive |
| 2 | Go to المنتجات → إضافة | Product form opens |
| 3 | Open dropdown | ✅ Deactivated warehouse is NOT in the list |

### Test 5: Edit Product — Pre-existing Tests Still Work
| Step | Action | Expected |
|------|--------|----------|
| 1 | Click تعديل on a product with a default warehouse | Edit form opens, warehouse pre-selected |
| 2 | Change warehouse, click حفظ | Reflected in grid |
| 3 | Click تعديل again, click "مسح", click حفظ | Warehouse cleared, shows "غير محدد" |

---

## ⛔ STOP — Do not proceed to BACKEND 4 until user approval.

---

## Prior Revision: R6

### Scope A — Product Create/Edit Modal: Default Warehouse ComboBox

**What:** Added a "المخزن الافتراضي" ComboBox to the Product create/edit modal dialog, populated from backend warehouses, with a "مسح" clear button.

**Changes:**

1. **ProductsViewModel.cs:**
   - Added `ObservableCollection<WarehouseDto> Warehouses` — populated once from `GET /api/v1/warehouses?page=1&pageSize=500`.
   - Added `SelectedWarehouse` property bound to the ComboBox.
   - `OpenCreateAsync()` loads warehouses, clears selection.
   - `OpenEditAsync()` loads warehouses, pre-selects the current warehouse from `ProductDto.DefaultWarehouseId`.
   - `SaveAsync()` — Create sends `DefaultWarehouseId = SelectedWarehouse?.Id`; Update computes delta vs original:
     - Same → `null` (no change)
     - Cleared → `Guid.Empty` (clear)
     - New → sends new GUID

2. **ProductsPage.xaml:**
   - ComboBox added after Category field, before IsActive checkbox.
   - Clear ("مسح") button beside ComboBox calls `ClearWarehouseSelectionCommand`.
   - Data grid: new "المخزن الافتراضي" column showing `DefaultWarehouseName` (or "غير محدد" when null).
   - Detail modal: new line showing "المخزن الافتراضي: {name}" (or "غير محدد").

3. **Inactive warehouses:** Hidden from ComboBox dropdown (filtered `w.IsActive == true`). If a product's current warehouse is now inactive, it will not appear in the dropdown on edit, effectively prompting the user to select a different one or clear it.

### Scope B — Variant Create/Edit Modal: Read-only Warehouse Display

**What:** Added a read-only "المخزن الافتراضي للمنتج" display in the Variant create/edit modal, sourced from the selected product's `DefaultWarehouseName`.

**Changes:**

1. **VariantsViewModel.cs:**
   - Added `FormDefaultWarehouseName` (read-only display string).
   - `SelectProduct()` populates from `ProductDto.DefaultWarehouseName` (already returned by the products list endpoint). If null → shows "غير محدد".
   - `OpenEdit()` populates from `VariantListDto.DefaultWarehouseName` (already returned by the variants endpoint).
   - `OpenCreate()` / `ClearProductSelection()` clears the field.
   - No warehouse fields sent in variant create/update requests (read-only only).

2. **VariantsPage.xaml:**
   - Read-only `Border` with label + value, visible when `IsProductSelected` is true.
   - Placed between the product picker and SKU field.

### Scope C — Error Handling

**What:** Added `WAREHOUSE_INACTIVE` mapping to `ErrorCodeMapper.cs`.

| Error Code | Arabic Message | Already Existed? |
|------------|---------------|-----------------|
| `WAREHOUSE_NOT_FOUND` | المخزن غير موجود | ✅ Yes |
| `WAREHOUSE_INACTIVE` | المخزن غير نشط | ❌ Added in R6 |

These errors flow through the existing ProblemDetails → ApiClient → ErrorCodeMapper → `FormError` display pipeline.

---

## Endpoints Used

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `GET /api/v1/warehouses?page=1&pageSize=500` | GET | Populate warehouse ComboBox (active only) |
| `GET /api/v1/products` | GET | Products list (includes `DefaultWarehouseId`, `DefaultWarehouseName`) |
| `GET /api/v1/products/{id}` | GET | Product detail (includes warehouse + variant warehouse fields) |
| `POST /api/v1/products` | POST | Create product with optional `DefaultWarehouseId` |
| `PUT /api/v1/products/{id}` | PUT | Update product (`DefaultWarehouseId`: null=no-change, Empty=clear, GUID=set) |
| `GET /api/v1/variants` | GET | Variants list (includes read-only `DefaultWarehouseId/Name`) |
| `POST /api/v1/variants` | POST | Create variant (no warehouse fields sent) |
| `PUT /api/v1/variants/{id}` | PUT | Update variant (no warehouse fields sent) |

---

## Request Payload Examples

### Create Product with DefaultWarehouseId
```json
POST /api/v1/products
{
  "name": "T-Shirt Premium",
  "description": "Cotton premium t-shirt",
  "category": "Apparel",
  "defaultWarehouseId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

### Create Product without DefaultWarehouseId
```json
POST /api/v1/products
{
  "name": "Basic Shirt",
  "category": "Apparel",
  "defaultWarehouseId": null
}
```

### Update Product — Change Warehouse
```json
PUT /api/v1/products/{id}
{
  "name": "T-Shirt Premium",
  "defaultWarehouseId": "b2c3d4e5-f6a7-8901-bcde-f12345678901"
}
```

### Update Product — Clear Warehouse (Guid.Empty)
```json
PUT /api/v1/products/{id}
{
  "name": "T-Shirt Premium",
  "defaultWarehouseId": "00000000-0000-0000-0000-000000000000"
}
```

### Update Product — No Change to Warehouse (null)
```json
PUT /api/v1/products/{id}
{
  "name": "Updated Name Only",
  "defaultWarehouseId": null
}
```

---

## Screens Changed Checklist

| Screen / File | Change | Done |
|---------------|--------|------|
| `ViewModels/ProductsViewModel.cs` | Warehouse loading, form fields, save logic | ✅ |
| `Views/Pages/ProductsPage.xaml` | ComboBox + clear button in form; grid column; detail line | ✅ |
| `ViewModels/VariantsViewModel.cs` | `FormDefaultWarehouseName` read-only field | ✅ |
| `Views/Pages/VariantsPage.xaml` | Read-only warehouse display in form modal | ✅ |
| `Models/ErrorCodeMapper.cs` | Added `WAREHOUSE_INACTIVE` mapping | ✅ |
| `Localization/Strings.resx` | +3 keys: `Field_DefaultWarehouse`, `Field_DefaultWarehouseNotSet`, `Variant_DefaultWarehouseLabel` | ✅ |
| `Localization/Strings.cs` | +3 accessor properties | ✅ |

---

## New Localization Keys (R6)

| Key | Arabic Value |
|-----|-------------|
| `Field_DefaultWarehouse` | المخزن الافتراضي |
| `Field_DefaultWarehouseNotSet` | غير محدد |
| `Variant_DefaultWarehouseLabel` | المخزن الافتراضي للمنتج |

---

## Build + Test Results

```
Build succeeded.  0 Warning(s)  0 Error(s)
Passed! - Failed: 0, Passed: 244, Skipped: 0, Total: 244
```

---

## Manual Human Test Script (R6)

### Prerequisites
- Backend running (`dotnet run` in `src/ElshazlyStore.Api`)
- Desktop app running (`dotnet run` in `src/ElshazlyStore.Desktop`)
- Logged in as admin with PRODUCTS_WRITE + WAREHOUSES_READ permissions
- At least one active warehouse exists in the system

### Test 1: Create Product with Default Warehouse
| Step | Action | Expected |
|------|--------|----------|
| 1 | Navigate to المنتجات, click إضافة | Product form modal opens |
| 2 | Enter product name, category | Fields populated |
| 3 | Click "المخزن الافتراضي" ComboBox | Dropdown shows active warehouses |
| 4 | Select a warehouse | Warehouse name displayed in ComboBox |
| 5 | Click حفظ | Save succeeds, modal closes |
| 6 | Find product in grid | "المخزن الافتراضي" column shows selected warehouse name |
| 7 | Click تفاصيل on the product | Detail modal shows "المخزن الافتراضي: {warehouse name}" |

### Test 2: Edit Product — Change Warehouse
| Step | Action | Expected |
|------|--------|----------|
| 1 | Click تعديل on product from Test 1 | Edit form opens, warehouse pre-selected |
| 2 | Change ComboBox to a different warehouse | New warehouse selected |
| 3 | Click حفظ | Save succeeds |
| 4 | Check grid / detail | New warehouse name reflected |

### Test 3: Edit Product — Clear Warehouse
| Step | Action | Expected |
|------|--------|----------|
| 1 | Click تعديل on product | Edit form opens with warehouse selected |
| 2 | Click "مسح" button beside ComboBox | ComboBox cleared (empty) |
| 3 | Click حفظ | Save succeeds, sends Guid.Empty to backend |
| 4 | Check grid | "المخزن الافتراضي" column shows "غير محدد" |
| 5 | Click تفاصيل | Detail shows "المخزن الافتراضي: غير محدد" |

### Test 4: Create Product without Warehouse
| Step | Action | Expected |
|------|--------|----------|
| 1 | Click إضافة, enter name | Form opens |
| 2 | Leave ComboBox empty (no selection) | No warehouse selected |
| 3 | Click حفظ | Save succeeds |
| 4 | Check grid | "المخزن الافتراضي" column shows "غير محدد" |

### Test 5: Variant Create — Read-only Warehouse Display
| Step | Action | Expected |
|------|--------|----------|
| 1 | Navigate to الأصناف, click إضافة | Variant form modal opens |
| 2 | Search for the product from Test 1 (with warehouse) | Product list appears |
| 3 | Click on the product to select it | Product selected, read-only "المخزن الافتراضي للمنتج: {warehouse name}" appears |
| 4 | Verify warehouse section is NOT editable | Read-only display only, no ComboBox |
| 5 | Fill variant fields, click حفظ | Save succeeds, no warehouse field in request |

### Test 6: Variant Edit — Read-only Warehouse Display
| Step | Action | Expected |
|------|--------|----------|
| 1 | Click تعديل on variant from Test 5 | Edit form opens |
| 2 | Read-only "المخزن الافتراضي للمنتج" is visible | Shows parent product's warehouse name |
| 3 | No way to modify warehouse from variant form | ✅ Read-only only |

### Test 7: Variant for Product without Warehouse
| Step | Action | Expected |
|------|--------|----------|
| 1 | Open Create Variant, select product from Test 4 (no warehouse) | Product selected |
| 2 | Read-only warehouse section shows | "المخزن الافتراضي للمنتج: غير محدد" |

### Test 8: Error — Invalid/Inactive Warehouse
| Step | Action | Expected |
|------|--------|----------|
| 1 | (API test) POST /api/v1/products with nonexistent warehouse ID | 404, "المخزن غير موجود" |
| 2 | (API test) POST /api/v1/products with inactive warehouse ID | 422, "المخزن غير نشط" |

---

## ⛔ STOP — Do not proceed to BACKEND 4 until user approval.

---

## Prior Revision: R5

### Scope A — SKU/Barcode Optional on Variant Create

**Problem:** UI enforced `Validation_SkuRequired` and blocked saving without SKU, preventing server-side generation (backend now supports null/empty SKU and barcode and generates them automatically).

**Changes:**

1. **Removed SKU required validation on CREATE.** The `if (string.IsNullOrWhiteSpace(FormSku))` guard now only applies in EDIT mode (`IsEditMode`). On CREATE, empty SKU is allowed.

2. **Request body sends null for empty fields:**
   - `CreateVariantRequest.Sku` and `.Barcode` changed from `string` to `string?` (nullable).
   - If user leaves SKU empty/whitespace → `Sku = null` in JSON body.
   - If user leaves Barcode empty/whitespace → `Barcode = null` in JSON body.
   - Server generates 10-digit numeric SKU and 13-digit barcode when null.

3. **Helper text displayed in CREATE mode:**
   - Below SKU field: `"اتركه فارغًا ليتم توليده تلقائيًا من السيرفر"`
   - Below Barcode field: same helper text.
   - Hidden in EDIT mode via `InverseBoolToVisConv` on `IsEditMode`.

4. **Success toast with generated values:**
   - On successful CREATE, shows MessageBox:
     `"تم إنشاء الصنف بنجاح — SKU: {sku} — الباركود: {barcode}"`
   - Values come from server response (`VariantListDto`), showing the actual generated identifiers.

5. **Manual SKU still works:** If user enters SKU manually, it's sent as-is. Server returns 409 on duplicate (displayed in Arabic via ProblemDetails).

### Scope B — SKU Search via by-sku Endpoint

**Problem:** SKU search mode on Variants page used `?q=` param (generic search) instead of the dedicated `GET /api/v1/variants/by-sku/{sku}` endpoint added in Backend 1-R1.

**Changes:**

1. **Search mode "SKU" now calls `GET /api/v1/variants/by-sku/{sku}`:**
   - Exact match lookup — returns single variant or 404.
   - On success: result displayed in lookup card (same card used for barcode results).
   - On 404: Arabic message `"لم يتم العثور"` displayed.

2. **Other search modes unchanged:**
   - "الكل" / "الاسم": still use `?q=` param on list endpoint.
   - "الباركود": still calls `GET /api/v1/barcodes/{barcode}`.

3. **Both manual and generated SKUs are discoverable** via the by-sku endpoint.

### Files Modified in R5

| File | Changes |
|------|---------|
| `Models/Dtos/VariantDto.cs` | `CreateVariantRequest.Sku` and `.Barcode` changed from `string` to `string?` |
| `ViewModels/VariantsViewModel.cs` | SKU validation only on EDIT; null for empty SKU/Barcode on CREATE; success toast with generated values; SKU search mode uses `by-sku` endpoint |
| `Views/Pages/VariantsPage.xaml` | Helper text below SKU and Barcode fields (visible in CREATE mode only) |
| `Localization/Strings.resx` | +4 keys: `Sku_HelperText`, `Barcode_HelperText`, `Variant_CreatedSuccess`, `Search_NotFound` |
| `Localization/Strings.cs` | +4 accessor properties |

### New Localization Keys (R5)

| Key | Arabic Value |
|-----|-------------|
| `Sku_HelperText` | اتركه فارغًا ليتم توليده تلقائيًا من السيرفر |
| `Barcode_HelperText` | اتركه فارغًا ليتم توليده تلقائيًا من السيرفر |
| `Variant_CreatedSuccess` | تم إنشاء الصنف بنجاح — SKU: {0} — الباركود: {1} |
| `Search_NotFound` | لم يتم العثور |

### Request Body Behavior (CREATE)

```json
// User leaves SKU + Barcode empty:
{
  "productId": "guid",
  "sku": null,
  "barcode": null,
  "color": "Black",
  "size": "M",
  "retailPrice": 49.99,
  "wholesalePrice": 35.00
}
// Server responds with generated values:
{
  "id": "guid",
  "productId": "guid",
  "productName": "...",
  "sku": "0000000042",
  "barcode": "4829103756284",
  ...
}
```

```json
// User enters SKU manually:
{
  "productId": "guid",
  "sku": "MY-CUSTOM-SKU",
  "barcode": null,
  ...
}
// Server preserves manual SKU, generates barcode.
```

### Search Behavior (by-sku)

| Mode | Endpoint | Behavior |
|------|----------|----------|
| SKU | `GET /api/v1/variants/by-sku/{sku}` | Exact match; result card or "لم يتم العثور" |
| الباركود | `GET /api/v1/barcodes/{barcode}` | Exact match; result card or "لم يتم العثور على الباركود" |
| الكل / الاسم | `GET /api/v1/variants?q={term}` | Fuzzy search across SKU/name/color/size/barcode |

### Build + Test

```
Build succeeded.  0 Warning(s)  0 Error(s)
Passed! - Failed: 0, Passed: 228, Skipped: 0, Total: 228
```

### Human Test Script (R5)

#### Prerequisites
- Backend running (`dotnet run` in `src/ElshazlyStore.Api`)
- Desktop app running (`dotnet run` in `src/ElshazlyStore.Desktop`)
- Logged in as admin with PRODUCTS_WRITE permission

#### Test A: Create Variant Without SKU (Server Generates)
| Step | Action | Expected |
|------|--------|----------|
| 1 | Navigate to الأصناف (Variants), click إضافة | Form opens |
| 2 | Select a product via product picker | Product name displayed |
| 3 | Leave SKU field empty | Helper text visible: "اتركه فارغًا ليتم توليده تلقائيًا من السيرفر" |
| 4 | Leave Barcode field empty | Helper text visible below barcode field |
| 5 | Fill Color, Size, Prices | Optional fields filled |
| 6 | Click حفظ (Save) | Save succeeds |
| 7 | Toast message shows | "تم إنشاء الصنف بنجاح — SKU: 00000000XX — الباركود: XXXXXXXXXXXXX" |
| 8 | Grid refreshes | New variant appears with generated SKU and Barcode |

#### Test B: Create Variant With Manual SKU
| Step | Action | Expected |
|------|--------|----------|
| 1 | Click إضافة, select product | Form opens |
| 2 | Enter SKU: `MY-CUSTOM-001` | Helper text still visible but user typed value |
| 3 | Leave Barcode empty | Server will generate barcode |
| 4 | Click حفظ | Save succeeds, toast shows manual SKU + generated barcode |
| 5 | Try creating another variant with same SKU | Server returns 409, Arabic error displayed |

#### Test C: SKU Search via by-sku Endpoint
| Step | Action | Expected |
|------|--------|----------|
| 1 | On Variants page, select search mode "SKU" | ComboBox shows "SKU" |
| 2 | Enter generated SKU from Test A (e.g., `0000000042`) | Text in search box |
| 3 | Click بحث or press Enter | Lookup card appears showing variant details (ProductName, SKU, Color, RetailPrice, Status, Barcode) |
| 4 | Enter non-existent SKU (e.g., `DOES-NOT-EXIST`) | Red error message: "لم يتم العثور" |
| 5 | Click مسح (Clear) | Card clears, search text clears, grid reloads |

#### Test D: Edit Mode Still Requires SKU
| Step | Action | Expected |
|------|--------|----------|
| 1 | Click تعديل on existing variant | Edit form opens with SKU pre-filled |
| 2 | Clear SKU field, click حفظ | Error: "رمز SKU مطلوب" (validation blocks empty SKU on edit) |
| 3 | Helper text NOT visible in edit mode | ✅ |

---

## Prior Revision: R3

## R3 Change Summary

### Scope A — Navigation Stability (P0 bug fix)

**Root cause:** `NavigationService.NavigateTo<T>()` called `GetRequiredService<T>()` on every invocation. Since all page ViewModels are registered as `Transient`, clicking the same sidebar item twice created a brand-new VM with empty `Items`, then `OnLoaded` fired `LoadCommand` → `Items.Clear()`, visually blanking the grid during reload.

**Fix (2 files + 5 code-behind):**

| File | Change |
|------|--------|
| `Services/NavigationService.cs` | Added `ConcurrentDictionary<Type, ViewModelBase> _cache`. `NavigateTo<T>()` now: (1) returns immediately if `CurrentPage` is already the same type (no-op on re-click), (2) uses `_cache.GetOrAdd()` to reuse VM instances. |
| 5× `*Page.xaml.cs` (code-behind) | `OnLoaded` now checks `vm.Items.Count == 0 && !vm.IsLoading` before triggering load — prevents redundant reload when returning to a cached page. |

### Scope B — Refresh Button + F5 Shortcut

| What | Detail |
|------|--------|
| Button | "تحديث" button added to search toolbar (after Clear) on all 5 pages. Bound to `RefreshCommand` in `PagedListViewModelBase`. |
| F5 shortcut | `<KeyBinding Key="F5" Command="{Binding RefreshCommand}" />` added as `UserControl.InputBindings` on all 5 pages. |
| ESC shortcut | `<KeyBinding Key="Escape" Command="{Binding CancelEditCommand}" />` added to close modals on all 5 pages. |
| Localization | `Strings.Action_Refresh` = "تحديث", `Strings.Toast_SaveSuccess` = "تم الحفظ بنجاح" added to RESX + accessor. |

### Scope C — Modal Form Overlays

All 5 commerce page forms converted from **full-page replacement** to **modal dialog overlays**:

| Pattern element | Implementation |
|----------------|---------------|
| List visibility | Always visible (removed `IsEditing → InverseBoolToVisConv` wrapper from list Grid) |
| Backdrop | `<Border Background="#80000000" />` (50% black dim — theme-safe) |
| Card | `<Border>` with `DynamicResource ContentBackgroundBrush`, `BorderBrush`, `CornerRadius="12"`, `MaxWidth=520` (560 for Variants), centered |
| Close button | `✕` in modal header, bound to `CancelEditCommand` |
| Scroll | `<ScrollViewer MaxHeight="500">` (560 for Variants) inside card |
| Products detail view | Also converted to modal overlay (MaxWidth=700, MaxHeight=600) |

### Scope D — Theme/RTL Regression Check

- All modal card colors use `DynamicResource` — verified 0 occurrences of `StaticResource` on theme-sensitive brushes in the 5 XAML pages.
- `#80000000` backdrop is theme-neutral (works on both light/dark).
- RTL `FlowDirection="RightToLeft"` set at `UserControl` level; all technical fields (Code, Phone, SKU, Barcode, Prices) keep explicit `FlowDirection="LeftToRight"`.

### Files Modified in R3

| File | Changes |
|------|---------|
| `Services/NavigationService.cs` | VM caching + same-page no-op guard |
| `Views/Pages/ProductsPage.xaml` | Modal overlays for detail + edit/create, F5/ESC bindings, refresh button |
| `Views/Pages/ProductsPage.xaml.cs` | Conditional OnLoaded |
| `Views/Pages/VariantsPage.xaml` | Modal overlay for edit/create (MaxWidth=560), F5/ESC bindings, refresh button |
| `Views/Pages/VariantsPage.xaml.cs` | Conditional OnLoaded |
| `Views/Pages/CustomersPage.xaml` | Modal overlay for edit/create, F5/ESC bindings, refresh button |
| `Views/Pages/CustomersPage.xaml.cs` | Conditional OnLoaded |
| `Views/Pages/SuppliersPage.xaml` | Modal overlay for edit/create, F5/ESC bindings, refresh button |
| `Views/Pages/SuppliersPage.xaml.cs` | Conditional OnLoaded |
| `Views/Pages/WarehousesPage.xaml` | Modal overlay for edit/create, F5/ESC bindings, refresh button |
| `Views/Pages/WarehousesPage.xaml.cs` | Conditional OnLoaded |
| `Localization/Strings.resx` | +Action_Refresh, +Toast_SaveSuccess |
| `Localization/Strings.cs` | +2 accessor properties |

### Build + Test

```
Build succeeded.  0 Warning(s)  0 Error(s)
Passed! - Failed: 0, Passed: 214, Skipped: 0, Total: 214
```

### Manual Test Script

1. Login → navigate to Products → grid loads
2. Click Products sidebar again → grid should NOT blank/reload (Scope A)
3. Click "تحديث" or press F5 → grid reloads in-place
4. Click "إنشاء" → modal overlay appears over the grid with dim backdrop
5. Press ESC → modal closes
6. Click Edit on a row → modal appears pre-filled
7. Toggle theme (dark/light) → modal card + backdrop correct
8. Repeat steps 2-7 for Variants, Customers, Suppliers, Warehouses
9. On Products, click a row to see Detail view → modal overlay with variant sub-list

---

## 1. Screens Delivered

| # | Screen | ViewModel | XAML Page | Perm (READ) | Perm (WRITE) |
|---|--------|-----------|-----------|-------------|--------------|
| 1 | Products | `ProductsViewModel` | `ProductsPage.xaml` | PRODUCTS_READ | PRODUCTS_WRITE |
| 2 | Variants + Barcode | `VariantsViewModel` | `VariantsPage.xaml` | PRODUCTS_READ | PRODUCTS_WRITE |
| 3 | Customers | `CustomersViewModel` | `CustomersPage.xaml` | CUSTOMERS_READ | CUSTOMERS_WRITE |
| 4 | Suppliers | `SuppliersViewModel` | `SuppliersPage.xaml` | SUPPLIERS_READ | SUPPLIERS_WRITE |
| 5 | Warehouses | `WarehousesViewModel` | `WarehousesPage.xaml` | WAREHOUSES_READ | WAREHOUSES_WRITE |

---

## 2. Feature Matrix

| Feature | Products | Variants | Customers | Suppliers | Warehouses |
|---------|----------|----------|-----------|-----------|------------|
| Paged list (DataGrid) | ✅ | ✅ | ✅ | ✅ | ✅ |
| Search / filter | ✅ | ✅ | ✅ | ✅ | ✅ |
| Search mode selector | ✅ (4 modes) | ✅ (4 modes) | — | — | — |
| Unified toolbar | ✅ | ✅ | ✅ | ✅ | ✅ |
| Sort by column | ✅ | ✅ | ✅ | ✅ | ✅ |
| Pagination controls | ✅ | ✅ | ✅ | ✅ | ✅ |
| Refresh (button + F5) | ✅ | ✅ | ✅ | ✅ | ✅ |
| Create (modal form) | ✅ | ✅ | ✅ | ✅ | ✅ |
| Edit (modal form) | ✅ | ✅ | ✅ | ✅ | ✅ |
| ESC to close modal | ✅ | ✅ | ✅ | ✅ | ✅ |
| Delete / Deactivate | DELETE | DELETE | Deactivate | Deactivate | Deactivate |
| Detail view (modal) | ✅ (variants) | — | — | — | — |
| Row double-click → details | ✅ | — | — | — | — |
| Barcode lookup (unified) | ✅ | ✅ | — | — | — |
| Navigation re-select safe | ✅ | ✅ | ✅ | ✅ | ✅ |
| Permission gate (READ) | ✅ | ✅ | ✅ | ✅ | ✅ |
| Permission gate (WRITE) | ✅ | ✅ | ✅ | ✅ | ✅ |
| Error handling (ProblemDetails) | ✅ | ✅ | ✅ | ✅ | ✅ |
| Arabic labels (RESX) | ✅ | ✅ | ✅ | ✅ | ✅ |
| RTL layout | ✅ | ✅ | ✅ | ✅ | ✅ |
| Technical fields LTR | — | SKU, Barcode, Prices | Phone, Code | Phone, Code | Code |
| Empty / Error / Busy states | ✅ | ✅ | ✅ | ✅ | ✅ |

---

## 3. Files Created

### Models / DTOs (6 files)
| File | Purpose |
|------|---------|
| `Models/Dtos/ProductDto.cs` | ProductDto, ProductDetailDto, CreateProductRequest, UpdateProductRequest |
| `Models/Dtos/VariantDto.cs` | VariantDto, VariantListDto, CreateVariantRequest, UpdateVariantRequest |
| `Models/Dtos/BarcodeLookupResult.cs` | BarcodeLookupResult for GET /barcodes/{barcode} |
| `Models/Dtos/CustomerDto.cs` | CustomerDto, CreateCustomerRequest, UpdateCustomerRequest |
| `Models/Dtos/SupplierDto.cs` | SupplierDto, CreateSupplierRequest, UpdateSupplierRequest |
| `Models/Dtos/WarehouseDto.cs` | WarehouseDto, CreateWarehouseRequest, UpdateWarehouseRequest |

### ViewModels (5 files)
| File | Base Class | API Endpoints |
|------|------------|---------------|
| `ViewModels/ProductsViewModel.cs` | PagedListViewModelBase\<ProductDto\> | GET/POST/PUT/DELETE /api/v1/products |
| `ViewModels/VariantsViewModel.cs` | PagedListViewModelBase\<VariantListDto\> | GET/POST/PUT/DELETE /api/v1/variants + GET /api/v1/barcodes/{b} |
| `ViewModels/CustomersViewModel.cs` | PagedListViewModelBase\<CustomerDto\> | GET/POST/PUT/DELETE /api/v1/customers |
| `ViewModels/SuppliersViewModel.cs` | PagedListViewModelBase\<SupplierDto\> | GET/POST/PUT/DELETE /api/v1/suppliers |
| `ViewModels/WarehousesViewModel.cs` | PagedListViewModelBase\<WarehouseDto\> | GET/POST/PUT/DELETE /api/v1/warehouses |

### Views (10 files — 5 XAML + 5 code-behind)
| File | Description |
|------|-------------|
| `Views/Pages/ProductsPage.xaml` | List + detail (with variants sub-grid) + create/edit form |
| `Views/Pages/ProductsPage.xaml.cs` | Code-behind — OnLoaded triggers LoadCommand |
| `Views/Pages/VariantsPage.xaml` | List + barcode lookup panel + create/edit form |
| `Views/Pages/VariantsPage.xaml.cs` | Code-behind |
| `Views/Pages/CustomersPage.xaml` | List + create/edit form + deactivate |
| `Views/Pages/CustomersPage.xaml.cs` | Code-behind |
| `Views/Pages/SuppliersPage.xaml` | List + create/edit form + deactivate |
| `Views/Pages/SuppliersPage.xaml.cs` | Code-behind |
| `Views/Pages/WarehousesPage.xaml` | List + create/edit form (Code=readonly on edit, IsDefault checkbox) |
| `Views/Pages/WarehousesPage.xaml.cs` | Code-behind |

### Helpers (1 file)
| File | Converters |
|------|------------|
| `Helpers/AdditionalConverters.cs` | BoolToActiveStatusConverter, StringNotEmptyToVisibilityConverter, InverseBoolConverter |

---

## 4. Files Modified

| File | Changes |
|------|---------|
| `Localization/Strings.resx` | +46 new keys: Nav_Variants, 20×Field_*, 4×Action_*, 2×Barcode_*, 6×Screen titles, 4×Validation_*, Dialog_ConfirmDeactivate |
| `Localization/Strings.cs` | +46 new static property accessors matching RESX keys |
| `Resources/Themes/SharedStyles.xaml` | +3 converter registrations (BoolToActiveStatus, StringNotEmptyToVis, InvBool) |
| `ViewModels/MainViewModel.cs` | +5 navigation switch cases (Products, Variants, Customers, Suppliers, Warehouses) |
| `Helpers/PageDataTemplateSelector.cs` | +5 template properties and switch arms |
| `Views/MainWindow.xaml` | +5 implicit DataTemplates in Window.Resources, +1 Variants nav button in Commerce sidebar |
| `App.xaml.cs` | +5 ViewModel DI registrations (AddTransient) |

---

## 5. API Endpoints Consumed

| Method | Endpoint | Screen |
|--------|----------|--------|
| GET | /api/v1/products?page=&pageSize=&search=&sort= | Products |
| GET | /api/v1/products/{id} | Products (detail) |
| POST | /api/v1/products | Products |
| PUT | /api/v1/products/{id} | Products |
| DELETE | /api/v1/products/{id} | Products |
| GET | /api/v1/variants?page=&pageSize=&search=&sort= | Variants |
| POST | /api/v1/variants | Variants |
| PUT | /api/v1/variants/{id} | Variants |
| DELETE | /api/v1/variants/{id} | Variants |
| GET | /api/v1/variants/by-sku/{sku} | Variants (SKU search) |
| GET | /api/v1/barcodes/{barcode} | Variants (lookup) |
| GET | /api/v1/customers?page=&pageSize=&search=&sort= | Customers |
| POST | /api/v1/customers | Customers |
| PUT | /api/v1/customers/{id} | Customers |
| DELETE | /api/v1/customers/{id} | Customers (deactivate) |
| GET | /api/v1/suppliers?page=&pageSize=&search=&sort= | Suppliers |
| POST | /api/v1/suppliers | Suppliers |
| PUT | /api/v1/suppliers/{id} | Suppliers |
| DELETE | /api/v1/suppliers/{id} | Suppliers (deactivate) |
| GET | /api/v1/warehouses?page=&pageSize=&search=&sort= | Warehouses |
| POST | /api/v1/warehouses | Warehouses |
| PUT | /api/v1/warehouses/{id} | Warehouses |
| DELETE | /api/v1/warehouses/{id} | Warehouses (deactivate) |

---

## 6. Localization Keys Added (Arabic)

### Field Labels
| Key | Value |
|-----|-------|
| Field_Name | الاسم |
| Field_Description | الوصف |
| Field_Category | الفئة |
| Field_VariantCount | عدد الأصناف |
| Field_Status | الحالة |
| Field_Actions | الإجراءات |
| Field_Code | الكود |
| Field_Phone | الهاتف |
| Field_Phone2 | هاتف ٢ |
| Field_Notes | ملاحظات |
| Field_Color | اللون |
| Field_Size | المقاس |
| Field_RetailPrice | سعر التجزئة |
| Field_WholesalePrice | سعر الجملة |
| Field_Barcode | الباركود |
| Field_ProductName | اسم المنتج |
| Field_ProductId | رقم المنتج |
| Field_Sku | SKU |
| Field_Address | العنوان |
| Field_IsDefault | المخزن الافتراضي |

### Actions / Navigation / Validation / Dialogs
| Key | Value |
|-----|-------|
| Nav_Variants | الأصناف |
| Action_Details | تفاصيل |
| Action_Back | رجوع |
| Action_Deactivate | تعطيل |
| Action_Clear | مسح |
| Barcode_LookupTitle | بحث بالباركود |
| Barcode_NotFound | لم يتم العثور على الباركود |
| Products_FormTitle | بيانات المنتج |
| Products_Variants | الأصناف |
| Variants_FormTitle | بيانات الصنف |
| Customers_FormTitle | بيانات العميل |
| Suppliers_FormTitle | بيانات المورد |
| Warehouses_FormTitle | بيانات المخزن |
| Validation_NameRequired | الاسم مطلوب |
| Validation_SkuRequired | رمز SKU مطلوب |
| Validation_ProductRequired | يجب اختيار المنتج |
| Validation_CodeRequired | الكود مطلوب |
| Dialog_ConfirmDeactivate | هل أنت متأكد من تعطيل هذا العنصر؟ |

---

## 7. Permission Gating

- **Sidebar visibility**: Each nav button is bound to `CanView*` (checks `*_READ` permission)
- **Variants** reuses `CanViewProducts` (variants are sub-entities of products)
- **Create/Edit/Delete buttons**: Each XAML page binds `Visibility` to `CanWrite` property on the ViewModel
- **CanWrite** in each ViewModel checks the corresponding `*_WRITE` permission via `IPermissionService`

---

## 8. Build & Test Verification

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Passed: 213, Failed: 1, Skipped: 0, Total: 214
```

**Failed test:** `PurchaseReceiptTests.PostPurchaseReceipt_ConcurrentDoublePost_OnlyOneStockMovement`  
**Cause:** Pre-existing backend concurrency race condition (both parallel requests return `Conflict`/`BadRequest` instead of at least one `OK`). This test was already failing before R1. Unrelated to any UI changes.

---

## 9. Architecture Notes

- All ViewModels extend `PagedListViewModelBase<T>` — shared paging, search, sort, loading, error states
- Error messages flow through `ProblemDetails` → `ErrorCodeMapper.ToArabicMessage()` → displayed in Arabic
- Deactivate (Customers/Suppliers/Warehouses) uses HTTP DELETE which performs a soft-delete on the backend
- Warehouses use Shape B paging (anonymous projection) but produce identical JSON — no special handling needed
- Barcode lookup on Variants page shows product details inline without navigation
- Product detail view loads `ProductDetailDto` which includes embedded `List<VariantDto>`
- Technical fields (SKU, Barcode, Phone, Code, Prices) use `FlowDirection="LeftToRight"` for correct display

---

## 10. Known Limitations / Future Work

1. **Bulk operations** — no bulk delete/deactivate. Can be added in a future phase.
2. **Form validation** — client-side validation is minimal (name required, SKU required on EDIT only — optional on CREATE for server generation). Server-side validation via ProblemDetails provides comprehensive coverage.
3. **No inline editing** — all edits go through a full form overlay. This is by design for consistency with the existing UI pattern.

---

## 11. Permanent Arabic / RTL Policy

> **Arabic is the ONLY UI language. RTL is the default FlowDirection.**
> - Every user-visible string MUST come from `Localization/Strings.resx` (Arabic).
> - `FlowDirection.RightToLeft` is set globally and per-UserControl.
> - `FlowDirection="LeftToRight"` is used ONLY for: SKU, Barcode, Code, Phone, Prices, Email, URLs, numeric IDs.
> - No English text may appear in the UI except technical identifiers.

---

## 12. R2 — Human Issues Fixed

### 12.1 Page Title Centering (Scope B)
**Issue:** Page titles were right-aligned (start-aligned in RTL) using DockPanel.
**Fix:** Replaced `DockPanel` headers with `Grid` overlay on all 5 screens + detail view. Title uses `HorizontalAlignment="Center"` with `Margin="0"` override. Create button overlays at one side.

**Affected pages:** ProductsPage, VariantsPage, CustomersPage, SuppliersPage, WarehousesPage.

### 12.2 RTL Button Content (Scope A)
**Issue:** Button ContentPresenters did not explicitly enforce RTL FlowDirection.
**Fix:** Added `FlowDirection="RightToLeft"` to ContentPresenter in all three button styles:
- `AccentButtonStyle` (SharedStyles.xaml)
- `SecondaryButtonStyle` (FormStyles.xaml)
- `DangerButtonStyle` (FormStyles.xaml)

Also added `HorizontalContentAlignment="Center"` and `FlowDirection="RightToLeft"` as style Setters.

### 12.3 DataGrid Dark Mode (Scope C — CRITICAL FIX)
**Issue:** Products DataGrid appeared **white** in Dark mode because it was missing `Style="{StaticResource ThemedDataGridStyle}"`. All other pages correctly used this style.
**Fix:** Added `Style="{StaticResource ThemedDataGridStyle}"` to ProductsPage main DataGrid, removed redundant inline `AutoGenerateColumns`, `IsReadOnly`, `SelectionMode` (now inherited from style).

### 12.4 Form Layout Centering (Scope B)
**Issue:** Form StackPanels used `HorizontalAlignment="Right"` — forms were pinned to one side.
**Fix:** Changed to `HorizontalAlignment="Center"` on all 5 form StackPanels. MaxWidth=500 preserved.

### 12.5 Dialogs RTL (Scope A — Limitation)
**Finding:** `MessageService` uses `System.Windows.MessageBox.Show()` which does NOT support `FlowDirection`. The text content is Arabic (from RESX), but the dialog chrome (title bar, button labels "OK"/"Cancel") follows the OS locale, not the app's FlowDirection. On non-Arabic Windows, the dialog will show LTR chrome but RTL Arabic text.
**Decision:** Documented as limitation. A future phase could replace MessageBox with a custom themed dialog.

---

## 13. R2 — DataGrid Theme Checklist (Dark/Light)

| Property | Source | Dark Value | Light Value |
|----------|--------|------------|-------------|
| `Background` | `DynamicResource CardBackgroundBrush` | #2A2A3D | #FFFFFF |
| `Foreground` | `DynamicResource PrimaryTextBrush` | #CDD6F4 | #4C4F69 |
| `HorizontalGridLinesBrush` | `DynamicResource BorderBrush` | #313244 | #CCD0DA |
| `BorderBrush` | `DynamicResource BorderBrush` | #313244 | #CCD0DA |
| Header `Background` | `DynamicResource SidebarBackgroundBrush` | #181825 | #E6E9EF |
| Header `Foreground` | `DynamicResource SecondaryTextBrush` | #A6ADC8 | #6C6F85 |
| Row hover | `DynamicResource NavItemHoverBrush` | #313244 | #DCE0E8 |
| Row selected | `DynamicResource NavItemActiveBrush` | #45475A | #CCD0DA |
| Alt row | `DynamicResource SidebarBackgroundBrush` | #181825 | #E6E9EF |
| Cell selected fg | `DynamicResource AccentBrush` | #89B4FA | #1E66F5 |

**All** DataGrid styles use `DynamicResource` → theme-switch safe.
**ProductsPage fix (R2):** Added `Style="{StaticResource ThemedDataGridStyle}"` — was the only page missing it.

---

## 14. R2 — Warehouse IsActive Create Decision (Scope D)

**Backend evidence:**
```csharp
// Server-side CreateWarehouseRequest (from WarehouseEndpoints.cs):
private sealed record CreateWarehouseRequest(string Code, string Name, string? Address, bool IsDefault = false);
// No IsActive property — server assigns IsActive = true by default.
```

**Decision:** `CreateWarehouseRequest` does NOT support `IsActive`. Therefore:
- **Create form:** No IsActive checkbox. Instead, shows informational note:
  > "يتم إنشاء المخزن نشطًا افتراضيًا، ويمكن تعطيله بعد الحفظ."
  > ("Warehouses are created active by default. You can deactivate after saving.")
- **Edit form:** `IsActive` checkbox shown and functional (via `UpdateWarehouseRequest.IsActive`).
- **RESX key added:** `Warehouse_CreateActiveNote`

---

## 15. R2 — Backend Capability Findings: SKU/Barcode Generation (Scope E)

### Experiment Setup
- Backend: `ElshazlyStore.Api` running at `http://localhost:5238`
- Auth: admin user with full permissions
- Parent product: existing product (بنطلون, ID `81e1cb43-...`)

### Test Results

| # | Request | SKU | Barcode | HTTP Status | Response |
|---|---------|-----|---------|-------------|----------|
| 1 | POST /api/v1/variants | `TEST-SKU-001` | `""` (empty) | **400** | `VALIDATION_FAILED: Barcode is required.` |
| 1b | POST /api/v1/variants | `TEST-SKU-001` | omitted (null) | **400** | `VALIDATION_FAILED: Barcode is required.` |
| 2 | POST /api/v1/variants | `""` (empty) | `1234567890123` | **400** | `VALIDATION_FAILED: SKU is required.` |
| 2b | POST /api/v1/variants | omitted (null) | `1234567890123` | **400** | `VALIDATION_FAILED: SKU is required.` |
| 3 | POST /api/v1/variants | `TEST-SKU-003` | `9999999999999` | **201** | Created. SKU and Barcode stored exactly as sent. |

### Conclusions

1. **SKU is OPTIONAL on CREATE (R5)** — can be empty or null. Server auto-generates 10-digit numeric SKU.
2. **Barcode is OPTIONAL on CREATE (R5)** — can be empty or null. Server auto-generates 13-digit barcode.
3. **Server preserves values exactly** when provided — no transformation, prefix, or modification of SKU/Barcode.
4. **Server-side generation capability confirmed** — per BACKEND 1-R1 IDENTIFIERS FIX CLOSEOUT.
5. **UI sends null** for empty SKU/Barcode, shows success toast with generated values.

---

## 16. R2 — Files Modified in R2

| File | R2 Changes |
|------|------------|
| `Resources/Themes/SharedStyles.xaml` | AccentButtonStyle: +`FlowDirection=RightToLeft`, +`HorizontalContentAlignment=Center`, ContentPresenter +`FlowDirection=RightToLeft` |
| `Resources/Themes/FormStyles.xaml` | SecondaryButtonStyle: +`FlowDirection=RightToLeft`, +`HorizontalContentAlignment=Center`, ContentPresenter +`FlowDirection=RightToLeft`. DangerButtonStyle: same changes. |
| `Localization/Strings.resx` | +1 key: `Warehouse_CreateActiveNote` |
| `Localization/Strings.cs` | +1 accessor: `Warehouse_CreateActiveNote` |
| `Views/Pages/ProductsPage.xaml` | Header DockPanel→Grid (centered title), DataGrid +`Style=ThemedDataGridStyle` (dark mode fix), form `HorizontalAlignment=Center`, detail view title centered |
| `Views/Pages/VariantsPage.xaml` | Header DockPanel→Grid (centered title), form `HorizontalAlignment=Center` |
| `Views/Pages/CustomersPage.xaml` | Header DockPanel→Grid (centered title), form `HorizontalAlignment=Center` |
| `Views/Pages/SuppliersPage.xaml` | Header DockPanel→Grid (centered title), form `HorizontalAlignment=Center` |
| `Views/Pages/WarehousesPage.xaml` | Header DockPanel→Grid (centered title), form `HorizontalAlignment=Center`, +create-mode note for IsActive |

---

## 17. R2 — Build & Test Results (3 runs)

```
dotnet build ElshazlyStore.sln --nologo
Build succeeded.
    0 Warning(s)
    0 Error(s)

=== TEST RUN 1 ===
Passed! - Failed: 0, Passed: 214, Skipped: 0, Total: 214, Duration: 15s

=== TEST RUN 2 ===
Passed! - Failed: 0, Passed: 214, Skipped: 0, Total: 214, Duration: 16s

=== TEST RUN 3 ===
Passed! - Failed: 0, Passed: 214, Skipped: 0, Total: 214, Duration: 16s
```

All 3 runs: 214/214 passed, 0 failed, 0 skipped.

---

## 18. R2 — Manual Human Test Script

### Prerequisites
- Backend running (`run server.bat` or `dotnet run` in `src/ElshazlyStore.Api`)
- Desktop app running (`run front.bat` or `dotnet run` in `src/ElshazlyStore.Desktop`)
- Logged in as admin user with all permissions

### Test A: Centered Page Title
| Step | Action | Expected |
|------|--------|----------|
| 1 | Navigate to Products | Page title "المنتجات" is **centered** horizontally |
| 2 | Navigate to Variants | Title "الأصناف" is centered |
| 3 | Navigate to Customers | Title "العملاء" is centered |
| 4 | Navigate to Suppliers | Title "الموردون" is centered |
| 5 | Navigate to Warehouses | Title "المخازن" is centered |
| 6 | Create button visible on one side, not overlapping title | ✅ |

### Test B: RTL Buttons
| Step | Action | Expected |
|------|--------|----------|
| 1 | On any screen, observe button text | Arabic text is centered within button |
| 2 | Click "إنشاء" (Create) | Button uses accent style, text centered |
| 3 | Click "بحث" (Search) | Button uses secondary style, text centered |
| 4 | Click "مسح" (Clear) | Button uses secondary style, text centered |
| 5 | Click "حفظ" (Save) in form | Text centered in accent button |

### Test C: DataGrid Dark Mode
| Step | Action | Expected |
|------|--------|----------|
| 1 | Switch to Dark mode (settings or toggle) | Theme changes to dark |
| 2 | Navigate to Products | DataGrid background is dark (#2A2A3D), NOT white |
| 3 | Verify column headers are dark (#181825) | ✅ |
| 4 | Verify alternating row backgrounds | Even rows darker, odd rows transparent |
| 5 | Hover over a row | Row highlights with hover color |
| 6 | Select a row | Row highlights with selection color, accent text |
| 7 | Switch to Light mode | DataGrid background turns white, headers light gray |

### Test D: Form Layout
| Step | Action | Expected |
|------|--------|----------|
| 1 | Click "إنشاء" on Products | Form appears **centered** on page (not stuck to right side) |
| 2 | Labels are right-aligned (RTL start) | ✅ Arabic text flows from right |
| 3 | TextBoxes below labels, aligned to same width | ✅ MaxWidth=500 |
| 4 | Verify same centering on Variants, Customers, Suppliers, Warehouses forms | All centered |

### Test E: Warehouses Create Note
| Step | Action | Expected |
|------|--------|----------|
| 1 | Navigate to Warehouses, click "إنشاء" | Form opens |
| 2 | Look below IsDefault checkbox | Informational note visible: "يتم إنشاء المخزن نشطًا افتراضيًا، ويمكن تعطيله بعد الحفظ." |
| 3 | No IsActive checkbox in create mode | ✅ Only the note |
| 4 | Click "تعديل" on existing warehouse | IsActive checkbox appears, note hidden |

### Test F: Product Detail View
| Step | Action | Expected |
|------|--------|----------|
| 1 | On Products screen, click "تفاصيل" | Detail view opens |
| 2 | Product name is **centered** in header | ✅ |
| 3 | "رجوع" (Back) button is visible | ✅ |
| 4 | Variants sub-grid uses themed styles (dark mode correct) | ✅ |

---

## 19. R2 — Scope Verification Summary

| Scope | Description | Status |
|-------|-------------|--------|
| A | RTL buttons/forms/dialogs | ✅ FlowDirection=RTL on all button ContentPresenters. Forms RTL. Dialogs use Arabic text (MessageBox limitation documented). |
| B | Professional layout | ✅ Centered page titles (Grid overlay). Centered forms (MaxWidth=500). DataGrid fully themed. |
| C | Dark/Light DataGrid theme | ✅ All DataGrid styles use DynamicResource. ProductsPage DataGrid fixed (was missing ThemedDataGridStyle). |
| D | Warehouse IsActive create | ✅ Backend does NOT support IsActive on create. Informational note shown in create mode. Edit mode has toggle. |
| E | SKU/Barcode server generation | ✅ **Both OPTIONAL on CREATE (R5).** Server auto-generates when null/empty. Evidence: BACKEND 1-R1 IDENTIFIERS FIX CLOSEOUT. UI sends null and shows generated values in toast. |

---

**STOP — Awaiting human approval before proceeding to next phase.**
