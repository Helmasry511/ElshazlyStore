# UI SALES RETURNS 1-R2 — CUSTOMER LIST DISPLAY FIX + CUSTOMER COLUMN ORDER — CLOSEOUT

**Phase:** SR-1-R2  
**Status:** ✅ CLOSED — Build GREEN (0 errors, 0 warnings)  
**Date:** 2025-07-31  
**Scope:** Desktop-only fix — `CustomerId` not sent in create/update requests + customer column reorder

---

## 1. Exact Root Cause

### Bug location

`SalesReturnsViewModel.cs` — desktop, `ElshazlyStore.Desktop`

### Failure chain

1. User selects a Posted invoice in the create modal.  
   `SelectInvoiceAsync` → `LoadInvoiceDetailsAsync(invoice.Id)` is called.

2. `LoadInvoiceDetailsAsync` fetches the full `SaleDto` from `/api/v1/sales/{id}`.  
   It correctly calls `FormCustomerName = invoice.CustomerNameDisplay` to display the customer name in the form.  
   **However**: `invoice.CustomerId` was **never captured** anywhere. No field held the actual `Guid?` customer ID.

3. When `SaveAsync` built the `CreateSalesReturnRequest`, it sent:
   ```csharp
   var body = new CreateSalesReturnRequest
   {
       WarehouseId = SelectedWarehouse.Id,
       OriginalSalesInvoiceId = SelectedOriginalInvoice.Id,
       Notes = ...,
       Lines = lineRequests
       // CustomerId omitted — defaults to null
   };
   ```

4. The backend service received `CustomerId = null`.  
   In `CreateAsync`, the customer-validation branch only runs when `request.CustomerId.HasValue`.  
   Since it was null, the `SalesReturn` entity was persisted with `CustomerId = null` and no `Customer` navigation link.

5. When `ListAsync` built the response DTO it mapped:
   ```csharp
   sr.Customer?.Name  // null because CustomerId was null
   ```
   → `CustomerName = null` → `CustomerNameDisplay` fell through to **"بدون عميل"** (anonymous/without-customer string).

6. Same defect affected the `UpdateSalesReturnRequest` path — edit-then-save also omitted `CustomerId`, so any re-save on an existing draft continued to keep the return customer-less.

### Is the bug backend, desktop, or both?

**Desktop only.**

The backend correctly:
- validates `CustomerId` when present
- persists `CustomerId` to the database
- includes `sr.Customer?.Name` in both `ListAsync` and `GetByIdAsync` mappings
- returns `CustomerName` correctly to the desktop for the detail/edit modal (because `GetByIdAsync` uses the tracked entity's navigation props)

The backend never received a `CustomerId` because the desktop never sent one. The list showed "بدون عميل" because the database row had `CustomerId = null`.

---

## 2. Exact Fix Path

### File: `SalesReturnsViewModel.cs`

**Change 1 — Added private field next to `FormCustomerName`:**
```csharp
[ObservableProperty] private string _formCustomerName = string.Empty;
private Guid? _invoiceCustomerId;   // ← new
```
Holds the actual customer ID from the selected invoice or the loaded draft detail.

**Change 2 — `LoadInvoiceDetailsAsync` now captures the customer ID:**
```csharp
FormCustomerName = invoice.CustomerNameDisplay;
_invoiceCustomerId = invoice.CustomerId;   // ← new
```

**Change 3 — `OpenEditAsync` now restores the customer ID from the draft detail:**
```csharp
FormCustomerName = detail.CustomerNameDisplay;
_invoiceCustomerId = detail.CustomerId;   // ← new
```
Ensures that edit-then-save does not clobber an already-saved customer.

**Change 4 — `SaveAsync` create path now includes `CustomerId`:**
```csharp
var body = new CreateSalesReturnRequest
{
    WarehouseId = SelectedWarehouse.Id,
    CustomerId = _invoiceCustomerId,   // ← new
    OriginalSalesInvoiceId = SelectedOriginalInvoice.Id,
    Notes = ...,
    Lines = lineRequests
};
```

**Change 5 — `SaveAsync` update path now includes `CustomerId`:**
```csharp
var body = new UpdateSalesReturnRequest
{
    WarehouseId = SelectedWarehouse.Id,
    CustomerId = _invoiceCustomerId,   // ← new
    OriginalSalesInvoiceId = SelectedOriginalInvoice.Id,
    Notes = ...,
    Lines = lineRequests
};
```

**Change 6 — `ResetForm()` clears `_invoiceCustomerId`:**
```csharp
FormCustomerName = string.Empty;
_invoiceCustomerId = null;   // ← new
```
Prevents stale customer ID leaking from a previous create session.

No backend changes were needed.

---

## 3. Column Order Fix

### File: `SalesReturnsPage.xaml`

**Previous column order:**
1. رقم المرتجع (Return Number)
2. التاريخ (Date)
3. رقم الفاتورة الأصلية (Original Invoice)
4. العميل (Customer)
5. المخزن (Warehouse)
6. الحالة (Status)
7. الإجمالي (Total)
8. إجراءات (Actions)

**New column order:**
1. رقم المرتجع (Return Number)
2. العميل (Customer)  ← moved from position 4
3. التاريخ (Date)     ← moved from position 2
4. رقم الفاتورة الأصلية (Original Invoice)
5. المخزن (Warehouse)
6. الحالة (Status)
7. الإجمالي (Total)
8. إجراءات (Actions)

The customer column XAML definition (`DataGridTextColumn` bound to `CustomerNameDisplay`, `Width="*"`) was physically relocated from after "Original Invoice" to immediately after "Return Number".

---

## 4. Explicit Correctness Statements

- ✅ **Invoice-linked returns now show the real customer name in the list.** The `CustomerId` from the original sales invoice is captured when the user selects an invoice and is sent in both `CreateSalesReturnRequest` and `UpdateSalesReturnRequest`. The backend persists it, and `ListAsync` maps `sr.Customer.Name` correctly.
- ✅ **Invoice-linked returns with no customer on the original invoice continue to show "بدون عميل"** (correct — no regression on anonymous-customer sales).
- ✅ **Customer displayed in the list is consistent with the edit modal and detail view.** All three read from the same backend entity.
- ✅ **Customer column order is now: return number → customer → date.** The XAML column sequence was updated accordingly.

---

## 5. Files Changed

| File | Change |
|---|---|
| `src/ElshazlyStore.Desktop/ViewModels/SalesReturnsViewModel.cs` | Added `_invoiceCustomerId` field; capture in `LoadInvoiceDetailsAsync` and `OpenEditAsync`; include in create + update request bodies; clear in `ResetForm()` |
| `src/ElshazlyStore.Desktop/Views/Pages/SalesReturnsPage.xaml` | Reordered list columns: Customer moved to position 2 (before Date) |

---

## 6. Build / Test Result

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## 7. Human Test Script

**Prerequisites:** App running. At least one Posted sales invoice ≤ 15 days old with a real customer exists. User has `ViewSalesReturns` + `SalesReturnCreate` permissions.

| # | Action | Expected |
|---|---|---|
| 1 | Navigate to مرتجعات المبيعات | List loads; column order visible is: رقم المرتجع → العميل → التاريخ → ... |
| 2 | Click إضافة مرتجع | Create modal opens |
| 3 | Type 2+ chars of invoice number | Dropdown populates with matching Posted invoices |
| 4 | Select a recent invoice that has a real customer | Lines load; customer name appears in read-only customer field |
| 5 | Set valid ReturnQty, reason, disposition on ≥ 1 line → Save | Draft created; notification shown |
| 6 | Return to list | **New row shows the real customer name — NOT "بدون عميل"** |
| 7 | Verify the customer column is the second column (after return number, before date) | Column order is رقم المرتجع → العميل → التاريخ |
| 8 | Click Open Detail (عرض) on the new row | Detail overlay shows same customer name |
| 9 | Click Edit (تعديل) on the new row | Edit modal shows same customer name in read-only customer field |
| 10 | Change a ReturnQty on one line → Save | Saves successfully; list still shows the real customer name |
| 11 | Reopen detail/edit; verify customer name is still correct | Customer not lost on re-save |
| 12 | Test with an anonymous-customer invoice (if available) | List shows "بدون عميل" — correct, no regression |
| 13 | Delete the draft → Confirm | Row removed; no regression |
| 14 | Edit/Delete on Posted/Voided row | Buttons disabled — no regression |

---

## 8. Cleanup Audit

### Touched files reviewed

**`SalesReturnsViewModel.cs`**
- Added one private field (`_invoiceCustomerId`).
- Added one assignment in `LoadInvoiceDetailsAsync` (captures from invoice).
- Added one assignment in `OpenEditAsync` (restores from draft detail).
- Added one clear in `ResetForm()`.
- Added `CustomerId = _invoiceCustomerId` in create and update request builders (two sites).
- No dead code added. No existing logic removed. The `FormCustomerName` display property is unchanged and still correct.

**`SalesReturnsPage.xaml`**
- Physically reordered three column definitions (Customer, Date, Original Invoice) within the existing `<DataGrid.Columns>` block.
- No columns added or removed. No bindings changed. No styles changed.

### What was removed
- Nothing was removed.

### What was intentionally left
- `FormCustomerName` display-only property — still needed for the customer read-only display in the create/edit form.
- Backend `UpdateSalesReturnRequest.ClearCustomer` — not sent (defaults null → false) because SR-1 never clears the customer from an existing draft. Correct SR-1 behaviour.
- All original invoice column, warehouse column, actions column — unchanged.

### Build/test after cleanup
```
Build succeeded. 0 Warning(s), 0 Error(s)
```

---

## 9. SR-2 Explicitly NOT Implemented

SR-2 (Post flow, Void, Print, No-invoice route, Manager override) was not implemented or modified in this phase.
