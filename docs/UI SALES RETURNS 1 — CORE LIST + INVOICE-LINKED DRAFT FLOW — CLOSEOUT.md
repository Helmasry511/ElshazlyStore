# UI SALES RETURNS 1 — CORE LIST + INVOICE-LINKED DRAFT FLOW — CLOSEOUT

**Phase:** SR-1  
**Status:** ✅ CLOSED — Build GREEN (0 errors, 0 warnings)  
**Date:** 2025-07-16

---

## 1. Scope Implemented

| Deliverable | Status |
|---|---|
| Sidebar navigation to Sales Returns (wired via existing button) | ✅ |
| `SalesReturnsViewModel` registered in DI + navigation switch | ✅ |
| Main list page: search, refresh, paging, empty/loading/error states | ✅ |
| Create modal: invoice typeahead (debounced, 250 ms, Posted-only) | ✅ |
| 15-day rule enforced in UI (invoice date > 15 days → error banner, lines blocked) | ✅ |
| Load original sales invoice lines into return grid (available-to-return qty) | ✅ |
| Line validation: ReturnQty > 0, ReturnQty ≤ AvailableQty, reason required, disposition in {ReturnToStock, Quarantine} | ✅ |
| SR-1 create draft (`POST /api/v1/sales-returns`) | ✅ |
| SR-1 edit draft (`PUT /api/v1/sales-returns/{id}`) | ✅ |
| SR-1 delete draft (`DELETE /api/v1/sales-returns/{id}`, Draft-only, requires `SalesReturnCreate`) | ✅ |
| Detail overlay (read-only, all status types) | ✅ |
| Arabic RTL, light/dark mode via DynamicResource | ✅ |
| 30 localization strings (Strings.resx + Strings.cs) | ✅ |

---

## 2. Scope Explicitly Excluded (SR-1 boundary)

- **Post** (`POST /api/v1/sales-returns/{id}/post`) — deferred to SR-2
- **Void** (`POST /api/v1/sales-returns/{id}/void`) — deferred to later phase
- **No-invoice route** (returns without original invoice) — deferred
- **Manager override** for expired 15-day window — deferred
- **Print receipt** — deferred to print phase
- **Disposition types** Scrap, Rework, ReturnToVendor, WriteOff — excluded from SR-1 UI picker

---

## 3. Files Created

| File | Description |
|---|---|
| `src/ElshazlyStore.Desktop/Models/Dtos/SalesReturnDto.cs` | `SalesReturnDto`, `SalesReturnLineDto`, `CreateSalesReturnRequest`, `UpdateSalesReturnRequest`, `SalesReturnLineRequest` aligned to openapi contract |
| `src/ElshazlyStore.Desktop/ViewModels/SalesReturnsViewModel.cs` | Full SR-1 ViewModel + `SalesReturnLineVm` + `DispositionOption` |
| `src/ElshazlyStore.Desktop/Views/Pages/SalesReturnsPage.xaml` | List page + detail overlay + create/edit modal |
| `src/ElshazlyStore.Desktop/Views/Pages/SalesReturnsPage.xaml.cs` | Standard code-behind (LoadCommand on Loaded) |

## 4. Files Modified

| File | Changes |
|---|---|
| `src/ElshazlyStore.Desktop/Localization/Strings.resx` | +30 SR-1 Arabic strings |
| `src/ElshazlyStore.Desktop/Localization/Strings.cs` | +30 matching C# properties |
| `src/ElshazlyStore.Desktop/App.xaml.cs` | `services.AddTransient<SalesReturnsViewModel>()` added |
| `src/ElshazlyStore.Desktop/ViewModels/MainViewModel.cs` | `case "SalesReturns"` added to navigation switch |
| `src/ElshazlyStore.Desktop/Views/MainWindow.xaml` | `DataTemplate` for `SalesReturnsViewModel` added (sidebar button already existed) |

---

## 5. Backend Endpoints Used

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/api/v1/sales-returns` | Paged list (search, page, pageSize) |
| `POST` | `/api/v1/sales-returns` | Create draft |
| `GET` | `/api/v1/sales-returns/{id}` | Detail + load for edit |
| `PUT` | `/api/v1/sales-returns/{id}` | Update draft |
| `DELETE` | `/api/v1/sales-returns/{id}` | Delete draft |
| `GET` | `/api/v1/sales?q=…&page=1&pageSize=10` | Invoice typeahead (filtered to Posted) |
| `GET` | `/api/v1/sales/{id}` | Load original invoice lines for return grid |
| `GET` | `/api/v1/warehouses` | Warehouse dropdown |
| `GET` | `/api/v1/reasons` | Reason code dropdown |

---

## 6. Permissions Used

| Permission | Usage |
|---|---|
| `ViewSalesReturns` | `CanViewSalesReturns` in `MainViewModel` (sidebar visibility) — pre-existing |
| `SalesReturnCreate` | `CanCreate` — controls New button visibility + Delete button enabled state |

---

## 7. DispositionType Mapping (SR-1)

| Backend int | Label (AR) | Exposed in SR-1 |
|---|---|---|
| 0 | Scrap | ❌ |
| 1 | Rework | ❌ |
| 2 | ReturnToVendor | ❌ |
| 3 | ReturnToStock | ✅ |
| 4 | Quarantine | ✅ |
| 5 | WriteOff | ❌ |

---

## 8. Known Constraints / Backend Contract Notes

- `originalSalesInvoiceId` is nullable in the backend schema. SR-1 enforces it as required at the UI validation layer.
- `returnDateUtc` is nullable in `CreateSalesReturnRequest`. SR-1 omits it from the form; backend defaults apply.
- The backend `SalesReturnLineDto.dispositionType` field is typed `string` in the openapi spec response — `DispositionDisplay` is a computed property on the client DTO that maps the string to a localized label.
- Invoice search filters to `Status == "Posted"` client-side since the backend `/api/v1/sales` endpoint returns all statuses.

---

## 9. Build Result

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Build blockers encountered and resolved:
- `NullToCollapsedConv` converter referenced in XAML did not exist in app resources → replaced with `BoolToVisConv` bound to new computed property `HasInvoiceSelected` on ViewModel. `OnSelectedOriginalInvoiceChanged` partial method raises `PropertyChanged` for `HasInvoiceSelected`.

---

## 10. Human Test Script

**Prerequisites:** App running, at least one Posted sales invoice exists, user has `ViewSalesReturns` + `SalesReturnCreate` permissions.

| # | Action | Expected |
|---|---|---|
| 1 | Click "مرتجعات المبيعات" in sidebar | Sales Returns list page loads, shows empty state or existing returns |
| 2 | Click "إضافة مرتجع" | Create modal opens |
| 3 | Type 2+ chars of invoice number in typeahead | Dropdown populates with matching Posted invoices |
| 4 | Select an invoice dated > 15 days ago | Error banner appears "الفاتورة أقدم من 15 يوم"; lines grid stays hidden |
| 5 | Select a recent invoice (≤ 15 days) | Lines populate from original invoice; column headers + items visible |
| 6 | Set ReturnQty > SoldQty on any line | Save blocked with "الكمية تتجاوز المتاح" error |
| 7 | Leave ReturnQty at 0 on all lines | Save blocked with qty validation error |
| 8 | Leave Reason blank on a line with ReturnQty > 0 | Save blocked with reason validation error |
| 9 | Pick Disposition outside {3,4} (should not be possible — only 2 options shown) | N/A — only ReturnToStock + Quarantine appear in picker |
| 10 | Fill valid form (warehouse, invoice, ≥1 line with qty/reason/disposition) → Save | Success notification; new Draft row appears in list |
| 11 | Click row Details (عرض) | Detail overlay shows all fields read-only |
| 12 | Click Edit (تعديل) on Draft | Edit modal opens pre-filled |
| 13 | Change a ReturnQty → Save | Updated; success notification |
| 14 | Click Delete (حذف) on Draft → Confirm | Row removed; success notification |
| 15 | Edit/Delete buttons on a Posted/Voided row | Buttons are disabled (grayed out) |

---

## 11. Next Phase Gate

SR-1 is complete. To proceed to SR-2 (Post + status transitions), confirm:
- [ ] Human test script passes on real data
- [ ] Backend `POST /api/v1/sales-returns/{id}/post` contract is stable
- [ ] Void flow requirements are defined (manager override, partial void, etc.)
