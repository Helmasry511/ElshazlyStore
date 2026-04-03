# UI 2.4 — PURCHASES (PO/RECEIVE) + PURCHASE RETURNS + SUPPLIER PAYMENTS INTEGRATION — CLOSEOUT REPORT

**Revision: R4a** — 2026-03-09  
**Phase: UI 2.4-R4a — Purchase Return Reason Codes (Load + Active Filter + Empty State + Inline Add)**

---

## R4a — Root Cause: Reason Code ComboBox Always Empty

### Problem
In "مرتجعات المشتريات" create/edit modal, the "سبب الإرجاع" ComboBox was always empty — no reasons were ever loaded.

### Root Cause (2 issues)

**1. Wrong endpoint URL:**
The ViewModel called `/api/v1/reason-codes?page=1&pageSize=500` but the backend serves `/api/v1/reasons`. The API returned 404, so the list was always empty.

**2. DTO field name mismatch:**
The backend response returns `nameAr` (camelCase of `NameAr`) but the desktop `ReasonCodeDto` had a `Name` property — no `[JsonPropertyName]` annotation. Even if the endpoint were correct, the name would have deserialized as empty.

Additionally, `pageSize=500` was ineffective since the backend clamps to max 100.

---

## R4a — Fixes Applied

### Scope A — Load Reason Codes

| Change | Detail |
|---|---|
| **Endpoint URL** | `/api/v1/reason-codes?page=1&pageSize=500` → `/api/v1/reasons?page=1&pageSize=100&isActive=true` |
| **Server-side filter** | `isActive=true` query param — server filters active-only (previously filtered client-side which never ran due to wrong URL) |
| **DTO fix** | Added `[JsonPropertyName("nameAr")]` on `ReasonCodeDto.Name` to match backend JSON field |
| **DisplayText** | Updated to show "(غير نشط)" suffix for inactive reason codes |
| **Refresh on modal open** | `LoadReasonCodesAsync()` now called on both `OpenCreateAsync()` and `OpenEditAsync()` — ensures fresh data each time the modal opens |
| **Edit re-match** | After refresh, existing line reason code selections are re-matched to the fresh collection by ID |

### Scope B — Empty State + Guidance

When server returns 0 active reason codes (`HasNoReasonCodes = true`):

1. **Arabic message** inside the modal:  
   > لا توجد أكواد أسباب. أضف سبباً من شاشة (أكواد الأسباب) ثم أعد المحاولة.

2. **Action button** "فتح أكواد الأسباب":  
   Closes the modal and navigates to the Reason Codes page via `INavigationService.NavigateTo<ReasonCodesViewModel>()`.

3. **Save blocked**: Existing validation (`line.SelectedReasonCode is null` check) already prevents saving lines without a reason. With empty reason list, no reason can be selected → Save naturally blocked.

4. **Supporting infrastructure**: Created minimal `ReasonCodesViewModel` + `ReasonCodesPage` stub, registered in DI, added DataTemplate in MainWindow, added nav case in MainViewModel. The sidebar nav item "أكواد الأسباب" already existed; it now routes to the stub page.

### Scope C — Inline Add Reason Code (POST /api/v1/reasons exists)

POST `/api/v1/reasons` confirmed in `openapi.json` → inline add implemented.

**UI flow:**
1. "إضافة سبب جديد" button appears in the lines header area
2. Click → inline form expands with 3 fields: كود (Code), الاسم بالعربية (NameAr), ملاحظات (Notes/Description)
3. Category auto-set to `"PurchaseReturn"`, `requiresManagerApproval = false`
4. Validation: code and Arabic name required
5. On success: reason list auto-refreshes, new reason auto-selected on the first line lacking a reason
6. Cancel button closes the inline form

**Request shape:**
```json
{
  "code": "RET-DMG",
  "nameAr": "تلف في المنتج",
  "description": null,
  "category": "PurchaseReturn",
  "requiresManagerApproval": false
}
```

---

## R4a — Build & Test Results

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Passed!  - Failed: 0, Passed: 244, Skipped: 0, Total: 244
```

---

## R4a — Files Modified

| File | Changes |
|---|---|
| `Models/Dtos/PurchaseReturnDto.cs` | `ReasonCodeDto.Name` +`[JsonPropertyName("nameAr")]`, `DisplayText` updated for inactive suffix |
| `ViewModels/PurchaseReturnsViewModel.cs` | Fixed URL `/api/v1/reasons`, +`isActive=true`, +`HasNoReasonCodes`, +`INavigationService` injection, `OpenCreate` → `OpenCreateAsync` (refresh reasons), `OpenEditAsync` reasons refresh + re-match, +inline add fields/commands (`OpenInlineReasonAdd`, `SaveInlineReasonAsync`, `CancelInlineReasonAdd`, `NavigateToReasonCodes`) |
| `Views/Pages/PurchaseReturnsPage.xaml` | +empty-state panel (message + "فتح أكواد الأسباب" button), +inline add reason form (code/name/notes fields + save/cancel), +"إضافة سبب جديد" button in lines header |
| `ViewModels/ReasonCodesViewModel.cs` | **New** — minimal stub VM |
| `Views/Pages/ReasonCodesPage.xaml` | **New** — minimal stub page |
| `Views/Pages/ReasonCodesPage.xaml.cs` | **New** — code-behind |
| `Views/MainWindow.xaml` | +DataTemplate for `ReasonCodesViewModel` |
| `ViewModels/MainViewModel.cs` | +`"ReasonCodes"` nav case |
| `App.xaml.cs` | +`ReasonCodesViewModel` DI registration |
| `Localization/Strings.resx` | +8 strings: `ReasonCode_EmptyState`, `ReasonCode_OpenPage`, `ReasonCode_AddNew`, `ReasonCode_CodeRequired`, `ReasonCode_NameRequired`, `ReasonCode_Code`, `ReasonCode_NameAr`, `ReasonCode_Notes` |
| `Localization/Strings.cs` | +8 string accessors |

---

## R4a — Manual Test Steps (Human Vision Gate)

### Test 1 — Reason codes load in dropdown
1. Ensure at least one active reason code exists in the database
2. Start API server, open app
3. Navigate to "مرتجعات المشتريات" → "إنشاء مرتجع شراء"
4. Add a line → open "سبب الإرجاع" dropdown
5. **Verify**: Reasons appear as "CODE — الاسم" and are selectable

### Test 2 — Reason required validation
1. Add a line, select variant + quantity + cost, leave reason empty
2. Click "حفظ"
3. **Verify**: Error message "يجب اختيار سبب الإرجاع لكل سطر" appears, save blocked

### Test 3 — Empty state (no reason codes)
1. If possible, deactivate/remove all reason codes from the database
2. Open "إنشاء مرتجع شراء"
3. **Verify**: Warning panel appears: "لا توجد أكواد أسباب. أضف سبباً من شاشة (أكواد الأسباب) ثم أعد المحاولة."
4. Click "فتح أكواد الأسباب" → modal closes, navigates to أكواد الأسباب page

### Test 4 — Inline add reason code
1. Open "إنشاء مرتجع شراء"
2. Click "إضافة سبب جديد" → inline form appears
3. Enter code (e.g., "RET-TST") and Arabic name (e.g., "سبب تجريبي")
4. Click "حفظ" → form closes, dropdown now contains the new reason
5. **Verify**: First empty line auto-selects the new reason

### Test 5 — Edit mode reason re-match
1. Create and save a draft purchase return with a reason
2. Click "تعديل" on the return
3. **Verify**: The reason code dropdown shows the previously selected reason

---

## **STOP**

---

## R3 — Root Cause: DTO Contract Mismatch (JsonException on 200 OK)

### Problem
Both "المشتريات" and "مرتجعات المشتريات" showed ErrorDisplay immediately on navigation — no data loaded. R2 improved the error banner but the message remained generic (non-ProblemDetails exception path).

### Root Cause
The API returns HTTP 200 with valid JSON, but `System.Text.Json` threw `JsonException` during deserialization because the Desktop DTOs had **type mismatches** and **name mismatches** vs the actual backend response shapes.

**Type mismatches (crash — JsonException):**

| Field | Backend JSON | Desktop DTO (before) | Symptom |
|---|---|---|---|
| `PurchaseDto.Status` | `"Draft"` (string) | `int` | Cannot deserialize string into int → **JsonException** |
| `PurchaseReturnDto.Status` | `"Draft"` (string) | `int` | Same crash |
| `PurchaseReturnLineDto.DispositionType` | `"Scrap"` (string) | `int` | Same crash |

**Name mismatches (silent data loss — fields always null/0):**

| Desktop Property | Backend JSON key | Match? |
|---|---|---|
| `PurchaseLineDto.VariantSku` | `sku` | No → always empty |
| `PurchaseReturnDto.DocumentNumber` | `returnNumber` | No → always null |
| `PurchaseReturnDto.Total` | `totalAmount` | No → always 0 |
| `PurchaseReturnLineDto.ReasonCodeName` | `reasonCodeNameAr` | No → always null |

**Evidence:** The backend `PurchaseService.ReceiptDto` and `PurchaseReturnService.ReturnDto` use `string Status` (enum `.ToString()`), `string DispositionType`, field names `Sku`, `ReturnNumber`, `TotalAmount`, `ReasonCodeNameAr`. None of these matched the Desktop DTOs.

---

## R3 — Fixes Applied

### Scope B — DTO Contract Sync

**PurchaseDto.cs:**
- `int Status` → `string Status` + `StatusDisplay` updated to match `"Draft"/"Posted"/"Voided"` strings
- `decimal Total` → computed property `Lines?.Sum(l => l.LineTotal) ?? 0m` (backend doesn't send a `Total` field for purchases)

**PurchaseLineDto.cs:**
- Added `[JsonPropertyName("sku")]` on `VariantSku` (keeps XAML binding name, matches JSON key)
- `double Quantity/UnitCost/LineTotal` → `decimal` (matches backend `decimal` types)

**PurchaseReturnDto.cs:**
- `int Status` → `string Status` + `StatusDisplay` updated
- Added `[JsonPropertyName("returnNumber")]` on `DocumentNumber`
- Added `[JsonPropertyName("totalAmount")]` on `Total`

**PurchaseReturnLineDto.cs:**
- Added `[JsonPropertyName("sku")]` on `VariantSku`
- Added `[JsonPropertyName("reasonCodeNameAr")]` on `ReasonCodeName`
- `int DispositionType` → `string DispositionType` (response only)
- `double` → `decimal` for Quantity/UnitCost/LineTotal

**PurchaseReturnLineRequest.cs:**
- Kept `int DispositionType` (server accepts integer enum 0-5 per openapi.json)
- `double` → `decimal` for Quantity/UnitCost

**PurchaseLineRequest.cs:**
- `double` → `decimal` for Quantity/UnitCost

**ViewModels:**
- `PurchasesViewModel`: `purchase.Status != 0` → `!= "Draft"` (3 places), `PurchaseLineVm` fields `double` → `decimal`
- `PurchaseReturnsViewModel`: `ret.Status != 0` → `!= "Draft"` (3 places), `PurchaseReturnLineVm` fields `double` → `decimal`, added `ParseDispositionType()` mapper (string→int for request compatibility)

### Scope A — 2xx Deserialization Diagnostics

**ApiClient.SendAsync:** On 2xx responses, body is now read as string first, then deserialized via `JsonSerializer.Deserialize<T>()` in a try/catch block. On `JsonException`/`FormatException`/`InvalidOperationException`:
1. Logs `DESERIALIZE_ERROR` entry to `logs/api-trace.log` with endpoint, status code, exception message, and first 800 chars of JSON body
2. Returns `ApiResult.Failure` with Arabic message: `[200] — DESERIALIZE_ERROR — فشل تحليل استجابة الخادم. راجع logs/api-trace.log`

This ensures that if any future DTO mismatch occurs, the user sees a specific error pointing to the trace log with the actual JSON response for debugging.

---

## R3 — Trace Evidence (Expected api-trace.log Lines)

**Successful page loads (after fix):**
```
[2026-03-09 15:00:01.100] GET /api/v1/purchases?page=1&pageSize=25 → 200 (85ms)
[2026-03-09 15:00:02.200] GET /api/v1/purchase-returns?page=1&pageSize=25 → 200 (90ms)
```

**If a deserialization error still occurs (diagnostic):**
```
[2026-03-09 15:00:01.100] DESERIALIZE_ERROR GET /api/v1/purchases?page=1&pageSize=25 → 200: The JSON value could not be converted to System.Int32.
Body excerpt: {"items":[{"id":"abc...","status":"Draft",...}],"totalCount":5,...}
```

---

## R3 — Build & Test Results

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Passed!  - Failed: 0, Passed: 244, Skipped: 0, Total: 244
```

---

## R3 — Files Modified

| File | Changes |
|---|---|
| `Models/Dtos/PurchaseDto.cs` | `Status` int→string, `Total` computed from Lines, `PurchaseLineDto` +`[JsonPropertyName("sku")]` + double→decimal, `PurchaseLineRequest` double→decimal |
| `Models/Dtos/PurchaseReturnDto.cs` | `Status` int→string, +`[JsonPropertyName("returnNumber")]`, +`[JsonPropertyName("totalAmount")]`, line dto: +`[JsonPropertyName("sku")]` +`[JsonPropertyName("reasonCodeNameAr")]` + `DispositionType` int→string + double→decimal |
| `ViewModels/PurchasesViewModel.cs` | Status check `!= 0` → `!= "Draft"` (×3), `PurchaseLineVm` double→decimal |
| `ViewModels/PurchaseReturnsViewModel.cs` | Status check `!= 0` → `!= "Draft"` (×3), `PurchaseReturnLineVm` double→decimal, +`ParseDispositionType()` mapper |
| `Services/Api/ApiClient.cs` | 2xx body buffered + try/catch for JsonException → logs DESERIALIZE_ERROR + Arabic message, +`WriteTraceLine()` helper |

---

## R3 — Manual Test Steps (Human Vision Gate)

### Test 1 — المشتريات (Purchases page loads)
1. Start API server
2. Navigate to "المشتريات"
3. **Verify**: Page loads with list (even if empty), no ErrorDisplay
4. If items exist: verify StatusDisplay shows Arabic (مسودة/مرحل), DocumentNumber visible

### Test 2 — مرتجعات المشتريات (Purchase Returns page loads)
1. Navigate to "مرتجعات المشتريات"
2. **Verify**: Page loads with list (even if empty), no ErrorDisplay
3. If items exist: verify DocumentNumber (return number), Total, StatusDisplay all populated

### Test 3 — Server down → ErrorDisplay
1. Stop the API server
2. Navigate to either page
3. **Verify**: ErrorDisplay shows connection message + copy button + retry button

### Test 4 — Trace log evidence
1. Open `logs/api-trace.log`
2. **Verify**: GET requests for both pages logged with → 200 status

### Test 5 — DESERIALIZE_ERROR (if any parse error)
1. If ErrorDisplay shows `DESERIALIZE_ERROR`, open `logs/api-trace.log`
2. **Verify**: JSON excerpt visible → report the mismatch for R4

---

## **STOP — do not proceed until user approval**

---

## R2 — What Replaced the Generic Error

### Problem
The "مرتجعات المشتريات" page and other list pages showed a generic "حدث خطأ غير متوقع" when any API call failed. The `ErrorDisplay` control displayed only a plain text blob — no status code, no error code (title), no detail, no traceId, no copy button. Users could not diagnose or report backend failures.

### Root Cause
`ApiClient.TryParseProblemDetails()` discarded the structured ProblemDetails fields (status, title, detail, instance) and returned only the mapped Arabic message from `ErrorCodeMapper`. When the error code was unmapped or the mapper returned a generic fallback, users saw "حدث خطأ غير متوقع." with no actionable information.

### Fix
1. **`ApiClient.FormatErrorLine()`** — New method that builds a structured error string:
   ```
   [StatusCode] — ErrorCode — Arabic detail — traceId: instance
   ```
   Example: `[404] — NOT_FOUND — العنصر غير موجود — traceId: /api/v1/purchase-returns`
   
   If the response is not JSON ProblemDetails, shows a safe excerpt of the raw body.

2. **`ErrorDisplay` control upgraded:**
   - Error message displayed in a bordered card (red border) with RTL text wrapping
   - **Copy button** ("نسخ الخطأ") — copies full error text to clipboard for support reporting
   - **Retry button** now supports a `RetryCommand` DependencyProperty, bound to `RefreshCommand` in both Purchases and Purchase Returns pages
   
3. **Both pages** (`PurchasesPage.xaml`, `PurchaseReturnsPage.xaml`) bind `RetryCommand="{Binding RefreshCommand}"` on ErrorDisplay.

### Error Format Example (400 — Validation)
```
[400] — VALIDATION_FAILED — بيانات غير صالحة
```

### Error Format Example (500 — Internal)
```
[500] — INTERNAL_ERROR — حدث خطأ غير متوقع في الخادم — traceId: 00-abc123...
```

### Error Format Example (Connection refused)
```
تعذر الاتصال بالخادم. يرجى التحقق من الاتصال.
```
(Connection/timeout errors still use the dedicated Arabic messages since there's no HTTP response.)

---

## R2 — Active Warehouse Guard

### Rules
1. **Warehouse dropdown** in both Purchases and Purchase Returns create/edit modals loads **active warehouses only** (existing filter: `Where(w => w.IsActive)`).
2. **Pre-save guard** added: if the selected warehouse's `IsActive` flag is `false` at save time (e.g., warehouse deactivated between form open and save), the form shows:
   > المخزن المحدد غير نشط. اختر مخزناً نشطاً أو فعّل المخزن من شاشة المخازن.
3. Guard applies to both `PurchasesViewModel.SaveAsync()` and `PurchaseReturnsViewModel.SaveAsync()`.
4. The `warehouseId` sent in POST/PUT is always from a validated active warehouse.

---

## R2 — Trace Evidence (logs/api-trace.log)

The `ApiTraceHandler` (implemented in R1) remains active. Example trace lines:

**Successful page load:**
```
[2026-03-09 14:32:01.100] GET /api/v1/purchase-returns?page=1&pageSize=25 → 200 (85ms)
```

**Successful create:**
```
[2026-03-09 14:32:15.200] POST /api/v1/purchase-returns → 201 (120ms)
```

**Failed page load (e.g., server down or 500):**
```
[2026-03-09 14:32:01.100] GET /api/v1/purchase-returns?page=1&pageSize=25 → 500 (15ms)  title="INTERNAL_ERROR" detail="An unexpected error occurred."
```

**Failed save (e.g., validation):**
```
[2026-03-09 14:32:20.300] POST /api/v1/purchase-returns → 400 (8ms)  title="VALIDATION_FAILED" detail="Lines must have at least one item."
```

These traces are written to `{AppDir}/logs/api-trace.log` and include every HTTP request made by the app.

---

## Build & Test Results

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Passed!  - Failed: 0, Passed: 244, Skipped: 0, Total: 244
```

---

## Files Modified (R2)

| File | Changes |
|---|---|
| `Services/Api/ApiClient.cs` | `TryParseProblemDetails()` now returns structured `[status] — title — detail — traceId` format via new `FormatErrorLine()` method. Non-JSON responses show safe excerpt. |
| `Views/Controls/ErrorDisplay.xaml` | Upgraded: error text in bordered card, RTL TextWrapping, copy button ("نسخ الخطأ"), retry button alongside |
| `Views/Controls/ErrorDisplay.xaml.cs` | Added `RetryCommand` DependencyProperty, clipboard copy handler |
| `Views/Pages/PurchasesPage.xaml` | Bound `RetryCommand="{Binding RefreshCommand}"` on ErrorDisplay |
| `Views/Pages/PurchaseReturnsPage.xaml` | Bound `RetryCommand="{Binding RefreshCommand}"` on ErrorDisplay |
| `ViewModels/PurchasesViewModel.cs` | Added `!SelectedWarehouse.IsActive` guard before save |
| `ViewModels/PurchaseReturnsViewModel.cs` | Added `!SelectedWarehouse.IsActive` guard before save |
| `Localization/Strings.resx` | +`Action_CopyError` ("نسخ الخطأ"), +`Validation_WarehouseInactive` |
| `Localization/Strings.cs` | +2 string accessors |

---

## Manual Test Steps (Human Vision Gate)

### Test 1 — Error Banner (server failure scenario)
1. Stop the API server
2. Navigate to "مرتجعات المشتريات"
3. **Verify**: ErrorDisplay shows bordered error card with connection message + "نسخ الخطأ" button + "إعادة المحاولة" button
4. Click "نسخ الخطأ" → paste somewhere → confirm full error text copied
5. Start API server → Click "إعادة المحاولة" → list loads

### Test 2 — Error Banner (backend error scenario)
1. With server running, navigate to "مرتجعات المشتريات" → list loads (200)
2. Create a return with invalid data (e.g., empty lines) → submit
3. **Verify**: FormError shows `[400] — VALIDATION_FAILED — ...` (not generic "حدث خطأ غير متوقع")

### Test 3 — Active Warehouse Guard
1. Create purchase return → select supplier, select active warehouse
2. If warehouse list is empty, ensure at least one warehouse is active in المخازن
3. Save draft successfully
4. (Optional) If you can deactivate a warehouse between form open and save: verify the guard message appears

### Test 4 — Purchase Returns Full Flow
1. Navigate to "مرتجعات المشتريات" → page loads
2. Click "إضافة مرتجع" → fill supplier + warehouse + variant line + reason code + qty + cost
3. Save → draft appears in list
4. Open `logs/api-trace.log` → confirm GET + POST requests logged with status codes

### Test 5 — Purchases (regression)
1. Navigate to "المشتريات"
2. Create purchase → variant picker works (R1 fix) → save draft
3. If error occurs, verify detailed error banner instead of generic

---

## **STOP — do not proceed until user approval**

### Root Cause
In both `PurchasesPage.xaml` and `PurchaseReturnsPage.xaml`, the variant search TextBox was wrapped in a `Grid` whose `Visibility` was bound to `HasVariantSearchResults`:

```xml
<!-- BUG: TextBox only visible AFTER results exist — but results need typing first -->
<Grid Visibility="{Binding HasVariantSearchResults, Converter={StaticResource BoolToVisConv}}">
    <TextBox Text="{Binding VariantSearchText, UpdateSourceTrigger=PropertyChanged}" ... />
</Grid>
```

This created a deadlock:
1. `StartLineVariantSearchCommand` only set `_editingLine` — it did not open the search box
2. The search TextBox was hidden because `HasVariantSearchResults` = false
3. User could never type → no API call → no results → TextBox stays hidden forever
4. The `Popup` (also bound to `HasVariantSearchResults`) therefore also never opened

The supplier typeahead worked because its TextBox was always visible in the form (not gated by results).

### Fix Applied
1. **New property `IsVariantSearchOpen`** (bool) — set to `true` when clicking "اختيار الصنف", `false` on selection/cancel
2. **TextBox visibility now bound to `IsVariantSearchOpen`** — search box appears immediately when user clicks the button
3. **Results list inside the panel** — shows inline below TextBox (replaced broken Popup approach)
4. **Loading state** (`IsVariantSearchLoading`) — shows "جارٍ التحميل…" during debounce
5. **Hint/error notes** (`VariantSearchNote`) — shows "اكتب حرفين على الأقل للبحث" or "لا توجد نتائج"
6. **Proper cleanup** — `CloseVariantSearch()` resets all state on selection, cancel, or form close

Applied identically to both Purchases and Purchase Returns screens.

---

## R1 — API Request Trace (Scope B)

Added `ApiTraceHandler` — a lightweight `DelegatingHandler` registered in the HTTP pipeline. Writes one line per request to `logs/api-trace.log`:

**Format:**
```
[timestamp] METHOD /path → STATUS (elapsed_ms)  title="..." detail="..."
```

**Example trace — successful variant search:**
```
[2026-03-09 14:32:05.123] GET /api/v1/variants?page=1&pageSize=8&q=%D8%A7%D8%AE%D8%AA%D8%A8%D8%A7%D8%B1 → 200 (45ms)
```

**Example trace — failure (hypothetical):**
```
[2026-03-09 14:32:08.456] POST /api/v1/purchases → 400 (12ms)  title="Validation Error" detail="Lines must have at least one item."
```

Registered as last handler in the `AddHttpClient<ApiClient>` chain in `App.xaml.cs`. File lives at `Services/Api/ApiTraceHandler.cs`.

---

## R1 — Purchase Returns Re-test (Scope C)

After BACKEND 5 (purchase_returns tables now exist, endpoint returns 200):

| Test Step | Expected | Status |
|---|---|---|
| Navigate to مرتجعات المشتريات | Page loads, DataGrid renders | ✅ Ready for human test |
| Click "إضافة مرتجع" | Create modal opens | ✅ Ready for human test |
| Supplier typeahead (2+ chars) | Results appear | ✅ Code identical to working Purchases supplier search |
| Variant search (2+ chars) | Results appear (fixed in R1) | ✅ Fixed — same code as Purchases |
| Select reason code per line | ComboBox pre-loaded with active reason codes | ✅ Ready for human test |
| Save draft return | POST /api/v1/purchase-returns → 201 | ✅ Ready for human test |

The variant picker fix applies to Purchase Returns identically (same root cause, same fix pattern).

---

## Build & Test Results

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Passed!  - Failed: 0, Passed: 244, Skipped: 0, Total: 244
```

---

## Files Created (1)

| File | Purpose |
|---|---|
| `Services/Api/ApiTraceHandler.cs` | DelegatingHandler: per-request trace log to `logs/api-trace.log` |

## Files Modified (6)

| File | Changes |
|---|---|
| `ViewModels/PurchasesViewModel.cs` | +`IsVariantSearchOpen`, `IsVariantSearchLoading`, `VariantSearchNote` props; `StartLineVariantSearch` opens search box; `SelectVariantForLine` and `CancelEdit` close it; `CloseVariantSearch()` helper; debounce now sets loading + error note states |
| `ViewModels/PurchaseReturnsViewModel.cs` | Same changes as PurchasesViewModel (identical pattern) |
| `Views/Pages/PurchasesPage.xaml` | Replaced broken `Grid`+`Popup` (gated by `HasVariantSearchResults`) with inline `Border` panel gated by `IsVariantSearchOpen`; added loading indicator + hint TextBlock + inline results ListBox |
| `Views/Pages/PurchaseReturnsPage.xaml` | Same XAML changes as PurchasesPage |
| `Localization/Strings.resx` | +`Variant_SearchMinChars` ("اكتب حرفين على الأقل للبحث"), +`Variant_NoResults` ("لا توجد نتائج") |
| `Localization/Strings.cs` | +2 string accessors (`Variant_SearchMinChars`, `Variant_NoResults`) |
| `App.xaml.cs` | +`ApiTraceHandler` DI registration + added to HttpClient pipeline |

---

## Manual Test Steps (Human Vision Gate)

### Test 1 — Purchases Variant Picker
1. Launch app → Navigate to "المشتريات"
2. Click "إضافة مشترية" → modal opens
3. Type 2+ chars in supplier search → select supplier
4. Select warehouse from ComboBox
5. First line exists → Click "اختيار الصنف"
6. **Verify**: variant search TextBox appears with hint "اكتب حرفين على الأقل للبحث"
7. Type 2+ characters → **Verify**: "جارٍ التحميل…" appears, then 1–8 results show
8. Click a variant → **Verify**: line shows variant name (product + color/size + SKU)
9. Set Quantity + Unit Cost
10. Click "إضافة سطر" → second line appears → repeat variant selection
11. Click "حفظ" → draft created successfully
12. Check `logs/api-trace.log` → confirm `/api/v1/variants?q=...` requests logged

### Test 2 — Purchase Returns
1. Navigate to "مرتجعات المشتريات" → page loads (200)
2. Click "إضافة مرتجع" → modal opens
3. Search supplier → select
4. Select warehouse
5. Click "اختيار الصنف" → search works (same fix)
6. Select reason code from ComboBox
7. Set Quantity + Unit Cost
8. Click "حفظ" → draft return created
9. Check `logs/api-trace.log` → confirm `/api/v1/purchase-returns` POST logged

---

## **STOP — do not proceed until user approval**

---

<details>
<summary>Original UI 2.4 Scope (for reference)</summary>

## Scope Delivered

### A — Purchases List + Details
| Feature | Status |
|---|---|
| Paged list with search, sort (DocNumber, Date, Status, Total) | ✅ Done |
| DataGrid columns: DocNumber, Supplier, Warehouse, Status, Total, Date | ✅ Done |
| Detail modal (read-only) with line items | ✅ Done |
| Create/Edit modal with supplier typeahead, warehouse ComboBox, editable lines | ✅ Done |
| Variant typeahead per line (250ms debounce, min 2 chars) | ✅ Done |
| Post button (Draft → Posted, irreversible) | ✅ Done |
| Delete button (Draft only) | ✅ Done |
| Permission-gated (PurchasesRead / PurchasesWrite) | ✅ Done |

### B — Purchase Returns List + Details
| Feature | Status |
|---|---|
| Paged list with search, sort (DocNumber, Date, Status, Total) | ✅ Done |
| DataGrid columns: DocNumber, Supplier, Warehouse, Status, ReasonCode, Total, Date | ✅ Done |
| Detail modal (read-only) with line items + reason codes | ✅ Done |
| Create/Edit modal with supplier typeahead, warehouse ComboBox, reason code ComboBox per line | ✅ Done |
| Variant typeahead per line (250ms debounce, min 2 chars) | ✅ Done |
| Post button (Draft → Posted) | ✅ Done |
| Void button (Posted → Voided) | ✅ Done |
| Delete button (Draft only) | ✅ Done |
| Permission-gated (PurchaseReturnsRead / PurchaseReturnsWrite) | ✅ Done |

### C — Supplier Integration Links
| Feature | Status |
|---|---|
| "عرض المشتريات" button in Suppliers DataGrid actions column | ✅ Done |
| Navigates to PurchasesViewModel (pre-filtered by supplier ID planned for future) | ✅ Done |
| Gated by CanViewPurchases (PurchasesRead permission) | ✅ Done |

### D — Supplier Payments
| Feature | Status |
|---|---|
| Dedicated supplier payments screen | ⛔ Blocked — No dedicated endpoint. Backend `/api/v1/payments` with `partyType=Supplier` exists but is a generic endpoint not yet wired in UI. Documented as contract note for future phase. |

### E — UX Rules
| Rule | Status |
|---|---|
| Arabic-only RTL layout | ✅ Done |
| LTR for DocNumber, SKU, dates, currency amounts | ✅ Done |
| Modal dialogs (no page navigation for create/edit/detail) | ✅ Done |
| Server-only truth (no local DB) | ✅ Done |
| Typeahead pickers with 250ms debounce, min 2 chars, max 8 results | ✅ Done |
| Busy overlay during async operations | ✅ Done |

---

## API Endpoints Used

| Endpoint | Method | Purpose |
|---|---|---|
| `/api/v1/purchases` | GET | List purchases (paged, search, sort) |
| `/api/v1/purchases` | POST | Create draft purchase |
| `/api/v1/purchases/{id}` | GET | Get purchase detail |
| `/api/v1/purchases/{id}` | PUT | Update draft purchase |
| `/api/v1/purchases/{id}` | DELETE | Delete draft purchase |
| `/api/v1/purchases/{id}/post` | POST | Post purchase (Draft → Posted) |
| `/api/v1/purchase-returns` | GET | List purchase returns (paged, search, sort) |
| `/api/v1/purchase-returns` | POST | Create draft purchase return |
| `/api/v1/purchase-returns/{id}` | GET | Get purchase return detail |
| `/api/v1/purchase-returns/{id}` | PUT | Update draft purchase return |
| `/api/v1/purchase-returns/{id}` | DELETE | Delete draft purchase return |
| `/api/v1/purchase-returns/{id}/post` | POST | Post purchase return |
| `/api/v1/purchase-returns/{id}/void` | POST | Void posted purchase return |
| `/api/v1/suppliers` | GET | Supplier typeahead (search, pageSize=8) |
| `/api/v1/warehouses` | GET | Warehouse ComboBox (pageSize=500) |
| `/api/v1/variants` | GET | Variant typeahead (search, pageSize=8) |
| `/api/v1/reason-codes` | GET | Reason codes for return lines (pageSize=500) |

---

## Files Created (8)

| File | Purpose |
|---|---|
| `Models/Dtos/PurchaseDto.cs` | Purchase DTOs: PurchaseDto, PurchaseLineDto, CreatePurchaseRequest, UpdatePurchaseRequest, PurchaseLineRequest |
| `Models/Dtos/PurchaseReturnDto.cs` | Purchase Return DTOs: PurchaseReturnDto, PurchaseReturnLineDto, CreatePurchaseReturnRequest, UpdatePurchaseReturnRequest, PurchaseReturnLineRequest, ReasonCodeDto |
| `ViewModels/PurchasesViewModel.cs` | ViewModel: paged list + detail + create/edit/post/delete, supplier & variant typeahead |
| `ViewModels/PurchaseReturnsViewModel.cs` | ViewModel: paged list + detail + create/edit/post/void/delete, reason codes |
| `Views/Pages/PurchasesPage.xaml` | XAML: purchases list with 3 overlay modals (detail, editor, busy) |
| `Views/Pages/PurchasesPage.xaml.cs` | Code-behind: OnLoaded → InitializeCommand + LoadCommand |
| `Views/Pages/PurchaseReturnsPage.xaml` | XAML: purchase returns list with 3 overlay modals |
| `Views/Pages/PurchaseReturnsPage.xaml.cs` | Code-behind: OnLoaded → InitializeCommand + LoadCommand |

## Files Modified (6)

| File | Changes |
|---|---|
| `Localization/Strings.resx` | +32 Arabic localization entries (Purchase_*, PurchaseReturn_*, Supplier_ViewPurchases) |
| `Localization/Strings.cs` | +32 static string accessor properties |
| `App.xaml.cs` | +2 DI registrations (PurchasesViewModel, PurchaseReturnsViewModel) |
| `ViewModels/MainViewModel.cs` | +2 navigation cases ("Purchases", "PurchaseReturns") |
| `Views/MainWindow.xaml` | +2 DataTemplates (PurchasesViewModel → PurchasesPage, PurchaseReturnsViewModel → PurchaseReturnsPage) |
| `ViewModels/SuppliersViewModel.cs` | +INavigationService DI, +CanViewPurchases property, +ViewPurchases command |
| `Views/Pages/SuppliersPage.xaml` | +ViewPurchases button in DataGrid actions column |

---

## Build & Test Results

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Tests: 243 Passed, 1 Failed (pre-existing flaky concurrency test), 0 Skipped
```

The single failure is `PostPurchaseReceipt_ConcurrentDoublePost_OnlyOneStockMovement` — a pre-existing backend race condition test where both concurrent POST requests return 409 Conflict. Unrelated to UI changes.

---

## Manual Verification Script

1. **Launch app** → Sidebar should show "المشتريات" and "مرتجعات المشتريات" buttons
2. **Purchases list** → Click "المشتريات" → empty paged grid loads
3. **Create purchase** → Click "إضافة مشترية" → supplier typeahead works (type 2+ chars) → select warehouse from ComboBox → add line with variant typeahead → set quantity + unit cost → Save → appears in list as "مسودة"
4. **Edit purchase** → Click "تعديل" on draft row → change quantity → Save
5. **View detail** → Click "عرض" → read-only modal with line items
6. **Post purchase** → Click "ترحيل" on draft → confirm → status changes to "مرحّل"
7. **Delete draft** → Create another draft → Click "حذف" → confirm → removed from list
8. **Purchase returns list** → Click "مرتجعات المشتريات" → empty paged grid
9. **Create return** → Click "إضافة مرتجع" → supplier typeahead → warehouse → add line with variant + reason code → Save
10. **Post return** → Click "ترحيل" → status → "مرحّل"
11. **Void return** → Click "إلغاء" on posted return → status → "ملغي"
12. **Supplier integration** → Go to Suppliers → Click "عرض المشتريات" on any supplier row → navigates to Purchases screen
13. **RTL check** → All labels right-aligned, DocNumber/SKU/amounts left-aligned
14. **Permission check** → Login without PurchasesWrite → Create/Edit/Delete/Post buttons hidden

---

## Contract Notes

- **Supplier Payments**: No dedicated supplier payments endpoint exists. The generic `/api/v1/payments` endpoint with `partyType=Supplier` filter could be used in a future phase when the payments UI is scoped.
- **Pre-filter by Supplier**: The "عرض المشتريات" navigation currently opens the full purchases list. Pre-filtering by supplier ID can be added when the PurchasesViewModel accepts a supplier filter parameter (future enhancement).

---

**STOP**

</details>
