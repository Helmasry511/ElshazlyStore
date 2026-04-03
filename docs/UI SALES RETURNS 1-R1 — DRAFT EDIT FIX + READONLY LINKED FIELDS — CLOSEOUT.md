# UI SALES RETURNS 1-R1 — DRAFT EDIT FIX + READONLY LINKED FIELDS — CLOSEOUT

**Phase:** SR-1-R1  
**Status:** ✅ CLOSED — Build GREEN (0 errors, 0 warnings)  
**Date:** 2025-07-17  
**Scope:** Backend EF tracking fix + desktop edit-mode rehydration + readonly linked fields  

---

## 1. Exact Root Cause of the Update Failure

### Bug location
`SalesReturnService.UpdateAsync` — backend, `ElshazlyStore.Infrastructure`

### Failure chain

1. `UpdateAsync` loaded the `SalesReturn` entity with `Include(r => r.OriginalSalesInvoice)`.  
   EF's change tracker began tracking the `SalesInvoice` entity (call it **instance A**).

2. Inside the `request.OriginalSalesInvoiceId.HasValue` branch the code then ran:
   ```csharp
   var origInvoice = await _db.SalesInvoices
       .AsNoTracking()                          // ← bypasses identity map
       .FirstOrDefaultAsync(si => si.Id == ..., ct);
   ...
   sr.OriginalSalesInvoice = origInvoice;        // ← assigns untracked instance B
   ```
   `AsNoTracking()` bypasses EF's identity map and returns a **new, detached object** (**instance B**) with the same primary key as instance A.

3. Assigning instance B to the navigation property caused EF to attempt to attach instance B to the change tracker.  
   EF already has instance A (same PK) tracked → **`InvalidOperationException`**:  
   > "The instance of entity type 'SalesInvoice' cannot be tracked because another instance with the same key value for {'Id'} is already being tracked."

4. Exception propagated as a 500 from `PUT /api/v1/sales-returns/{id}`.

### Is the bug backend, desktop, or both?

**Backend only.**  
The desktop client was sending a correct, well-formed PUT payload. The crash happened entirely inside `SalesReturnService.UpdateAsync` due to the conflicting EF tracked instances.

---

## 2. Exact Fix Path

### Backend fix — `SalesReturnService.cs`

**Removed** the navigation-property assignment `sr.OriginalSalesInvoice = origInvoice;`.

Updating only the FK scalar (`sr.OriginalSalesInvoiceId`) is sufficient for EF to persist the relationship. The change tracker never receives a conflicting instance. The final `GetByIdAsync` call at the end of `UpdateAsync` re-loads the full entity (with correct navigation properties) for the response DTO.

Added an explanatory comment so the fix is self-documenting:

```csharp
// Intentionally not assigning sr.OriginalSalesInvoice — FK update is sufficient.
// Assigning the AsNoTracking instance here would conflict with the already-tracked
// instance loaded via .Include(r => r.OriginalSalesInvoice) above.
sr.OriginalSalesInvoiceId = request.OriginalSalesInvoiceId.Value;
```

The `AsNoTracking()` query is **kept** for validation (existence + Posted status check) — no tracking is needed for validation-only reads.

No other changes were needed to the backend.

---

## 3. Edit Mode Rehydration Changes

### Problem

When `OpenEditAsync` set `InvoiceSearchText = detail.OriginalInvoiceNumber`, the `OnInvoiceSearchTextChanged` partial method fired unconditionally. Even though it did not clear `SelectedOriginalInvoice` (because the text matched the existing value), it still kicked off a 250-ms debounced invoice search that populated `InvoiceSearchResults` and showed the search popup — making the edit form look like create mode and confusing the user.

There was also no customer name displayed anywhere in the create/edit form; the customer field existed only in the list and detail overlay.

### Fixes applied — `SalesReturnsViewModel.cs`

| # | Change | Detail |
|---|---|---|
| 1 | Added `[ObservableProperty] private string _formCustomerName = string.Empty;` | New observable for customer display in both create and edit mode |
| 2 | Guard in `OnInvoiceSearchTextChanged` | `if (IsEditMode) return;` — exits immediately in edit mode; no search, no popup, no clearing |
| 3 | `LoadInvoiceDetailsAsync` now sets `FormCustomerName = invoice.CustomerNameDisplay;` | Customer is shown immediately after invoice selection in create mode |
| 4 | `OpenEditAsync` now sets `FormCustomerName = detail.CustomerNameDisplay;` | Customer pre-loaded from the existing draft return; no reselection required |
| 5 | `ResetForm()` now resets `FormCustomerName = string.Empty;` | Cleans up on cancel/close |

---

## 4. Readonly Linked Fields in Edit Mode

### Invoice field — `SalesReturnsPage.xaml`

Added `IsReadOnly="{Binding IsEditMode}"` to the invoice search `TextBox`.  
In edit mode: the TextBox shows the existing invoice number but cannot be typed into. The search handler guard (`if (IsEditMode) return;`) ensures no popup ever appears.

### Customer field — `SalesReturnsPage.xaml`

Added a new `StackPanel` with a read-only `TextBox` bound to `FormCustomerName`, visible via `StringNotEmptyToVis` converter. The TextBox is always `IsReadOnly="True"`.

In **create mode**: appears after the user selects an invoice (customer name is derived from the invoice).  
In **edit mode**: appears immediately when the modal opens (customer name pre-loaded from the return record).

---

## 5. Explicit Read-Only Statements

- ✅ **Original invoice field is read-only in Edit mode** — `IsReadOnly="{Binding IsEditMode}"` on the TextBox; search handler guarded by `if (IsEditMode) return;`
- ✅ **Customer field is read-only in Edit mode** — `TextBox` with `IsReadOnly="True"`, populated from `detail.CustomerNameDisplay` in `OpenEditAsync`

---

## 6. Files Changed

| File | Change |
|---|---|
| `src/ElshazlyStore.Infrastructure/Services/SalesReturnService.cs` | Backend: removed `sr.OriginalSalesInvoice = origInvoice;` in `UpdateAsync`; added explanatory comment |
| `src/ElshazlyStore.Desktop/ViewModels/SalesReturnsViewModel.cs` | Added `FormCustomerName` property; guard in `OnInvoiceSearchTextChanged`; customer assignment in `LoadInvoiceDetailsAsync` and `OpenEditAsync`; reset in `ResetForm()` |
| `src/ElshazlyStore.Desktop/Views/Pages/SalesReturnsPage.xaml` | `IsReadOnly="{Binding IsEditMode}"` on invoice TextBox; added read-only customer StackPanel |

---

## 7. Correctness Preserved

| Area | Verified |
|---|---|
| Create draft — invoice typeahead and selection still work | ✅ (guard only applies in `IsEditMode`) |
| Create draft — customer auto-populated from selected invoice | ✅ (`LoadInvoiceDetailsAsync` sets `FormCustomerName`) |
| Create draft — lines loaded from invoice | ✅ unchanged |
| PUT validation — existence + status check unchanged | ✅ (`AsNoTracking` query kept for validation) |
| PUT saves correct FK | ✅ (`sr.OriginalSalesInvoiceId` set; EF persists relationship) |
| PUT response DTO has correct invoice/customer | ✅ (`GetByIdAsync` reloads full entity at end of `UpdateAsync`) |
| DELETE still works | ✅ not touched |
| List + paging still works | ✅ not touched |
| Detail overlay still works | ✅ not touched |

---

## 8. Build / Test Result

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## 9. Human Test Script

**Prerequisites:** App running. At least one Posted sales invoice ≤ 15 days old exists. User has `ViewSalesReturns` + `SalesReturnCreate` permissions.

| # | Action | Expected |
|---|---|---|
| 1 | Navigate to "مرتجعات المبيعات" | List loads |
| 2 | Click "إضافة مرتجع" | Create modal opens; invoice field empty; customer field hidden |
| 3 | Type 2+ chars of invoice number | Dropdown populates with matching Posted invoices |
| 4 | Select a recent invoice (≤ 15 days) | Lines load; **customer name appears** in read-only field below invoice |
| 5 | Set valid ReturnQty, reason, disposition on ≥ 1 line → Save | Draft created; notification; row appears in list |
| 6 | Click Edit (تعديل) on the new Draft row | Edit modal opens; **invoice number pre-filled (read-only)**; **customer pre-filled (read-only)**; lines pre-loaded with existing return quantities |
| 7 | Verify invoice TextBox **cannot be typed into** | Field is read-only; no search popup appears |
| 8 | Verify customer field **cannot be typed into** | Always read-only |
| 9 | Verify **no invoice search popup appears** when edit modal opens | Popup does not appear at any point |
| 10 | Change a ReturnQty on one line → Save | **Saves successfully** (no 500 error); updated draft row in list |
| 11 | Open Edit again; verify changed qty is reflected | Pre-filled with updated quantity |
| 12 | Open Create modal again | Invoice field empty, editable; customer field hidden |
| 13 | Select an invoice in create; verify typeahead still works | Dropdown populates normally |
| 14 | Delete a Draft → Confirm | Row removed; no regression |
| 15 | Edit/Delete on Posted/Voided row | Buttons disabled |

---

## 10. Cleanup Audit

### Touched files reviewed

**`SalesReturnService.cs`**
- Removed one line (`sr.OriginalSalesInvoice = origInvoice;`). All surrounding logic, validations, and other navigation-property assignments in other code paths are unchanged. No dead code introduced.

**`SalesReturnsViewModel.cs`**
- Added 5 focused changes. No dead code. The `IsEditMode` guard in `OnInvoiceSearchTextChanged` replaces previously-unguarded search trigger — no logic path lost (edit mode should never trigger a new invoice search).

**`SalesReturnsPage.xaml`**
- Added one attribute (`IsReadOnly="{Binding IsEditMode}"`) to existing TextBox.
- Added one new `StackPanel` (customer display). Uses only existing converters (`StringNotEmptyToVis`) and styles (`FormLabelStyle`, `ThemedTextBoxStyle`). No new resources added.

### What was removed
- `sr.OriginalSalesInvoice = origInvoice;` — removed because it caused EF tracking conflict and was never necessary (FK update is sufficient).

### Why removal was safe
- EF persists foreign-key relationships via the scalar FK property (`OriginalSalesInvoiceId`). Navigation property reassignment is optional and was only cosmetically populating the in-memory graph for a transient object whose lifetime ends at `SaveChangesAsync`. The returned DTO is built by `GetByIdAsync` which does its own fresh query — it never relies on the in-memory navigation property of `sr`.

### What was intentionally left
- `AsNoTracking()` on the validation query — kept because it is correct and efficient for read-only validation.
- All other navigation-property assignments in `UpdateAsync` (Warehouse, Customer) — left because those use non-`AsNoTracking` queries, so EF's identity map returns the already-tracked instance, avoiding any conflict.

### Build/test after cleanup
```
Build succeeded. 0 Warning(s), 0 Error(s)
```

---

## 11. SR-2 Explicitly NOT Implemented

SR-2 (Post, Void, manager override, no-invoice route, print receipt) was **NOT implemented** in this phase.

This phase is strictly SR-1-R1: Draft Edit Fix + Readonly Linked Fields only.
