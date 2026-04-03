# UI SALES 1 — SALES ADMIN SCREEN — CLOSEOUT

**Date:** 2026-03-28  
**Status:** COMPLETE  
**Scope:** Sales Admin desktop screen only. No POS, no Customer Payments screen, no Sales Returns UI, no Print Config work.

---

## 1. Grounded Outcome

Sales Admin is now implemented in the WPF desktop app against the existing backend contract.

The screen provides:
- paged Sales list with search, sort, refresh, and status-aware row actions
- create/edit draft sales invoice modal
- details modal
- draft-only delete
- draft-only post
- professional sales invoice printing through the shared receipt system
- optional customer workflow with anonymous sale support
- inline quick-add customer modal with duplicate-suggestion guardrails
- line editor with proven variant search and retail/wholesale/manual pricing helpers
- NotificationBar-first success UX and permission-gated actions

This phase did not invent backend fields or endpoints. The implementation stays within the current Sales contract: list/get/create/update/delete/post.

---

## 2. Contract / Policy Alignment

The implemented UI honors the current backend and documented policy exactly:

- `customerId` is nullable, so the UI supports anonymous sale.
- invoice date is editable on create only; the update contract does not expose `invoiceDateUtc`.
- discounts are line-level only; no invoice-level discount UI was added.
- no tax UI was added.
- no customer pricing type enforcement was invented; retail/wholesale buttons are helpers only.
- no warehouse-type schema was invented; the UI applies a conservative sales-appropriate warehouse filter using existing warehouse name/code markers and falls back to all active warehouses if the filter would hide everything.
- print flows through the existing shared `ReceiptPrintService` architecture.

---

## 3. Files Added / Updated

### New

- `src/ElshazlyStore.Desktop/Models/Dtos/SalesDto.cs`
  - Desktop DTOs and request payloads aligned to backend Sales contract.

- `src/ElshazlyStore.Desktop/ViewModels/SalesViewModel.cs`
  - Sales Admin ViewModel with list, modals, commands, customer workflow, line editor, posting, delete, and print orchestration.

- `src/ElshazlyStore.Desktop/Views/Pages/SalesPage.xaml`
  - Sales Admin page UI.

- `src/ElshazlyStore.Desktop/Views/Pages/SalesPage.xaml.cs`
  - Standard page-load bootstrap calling initialize + load.

### Updated

- `src/ElshazlyStore.Desktop/App.xaml.cs`
  - registered `SalesViewModel` in DI.

- `src/ElshazlyStore.Desktop/ViewModels/MainViewModel.cs`
  - added real `Sales` navigation route.

- `src/ElshazlyStore.Desktop/Views/MainWindow.xaml`
  - added `SalesViewModel` DataTemplate.

- `src/ElshazlyStore.Desktop/Helpers/DocumentPrintHelper.cs`
  - added professional Sales invoice print method using `ReceiptPrintService`.

- `src/ElshazlyStore.Desktop/Localization/Strings.cs`
- `src/ElshazlyStore.Desktop/Localization/Strings.resx`
  - added Sales-specific Arabic-first strings.

- `tests/ElshazlyStore.Tests/ElshazlyStore.Tests.csproj`
  - surgically aligned ASP.NET Core and EF Core package versions to `8.0.25` so solution build and test execution are no longer blocked by the pre-existing package drift noted in R10.

---

## 4. UX / Behavior Notes

### 4A. Sales List

- search box uses the standard paged list pattern
- sort buttons added for date, number, and total
- actions per row: details, edit, print, post, delete
- edit/post/delete are disabled or rejected for posted invoices

### 4B. Sales Form

- draft-only create/edit modal
- warehouse selection from active warehouses, filtered for likely sales warehouses
- optional customer selection
- explicit anonymous-sale path
- quick-add customer modal with existing-customer suggestions by name/phone
- line editor supports variant search through the existing variants endpoint
- pricing helper buttons apply retail or wholesale price into the editable unit price field
- line discount is per-line only
- total is recomputed from line values in the form

### 4C. Printing

Sales print now uses the shared print engine and includes:
- title
- invoice number
- date
- customer
- warehouse
- cashier
- notes
- lines table
- total
- signature block

This keeps Sales printing consistent with the R10 receipt/document standardization work.

---

## 5. Build / Test Result

### Build

```text
dotnet build .\ElshazlyStore.sln -c Debug
Build succeeded.
0 Warning(s)
0 Error(s)
```

### Tests

```text
dotnet test .\tests\ElshazlyStore.Tests\ElshazlyStore.Tests.csproj -c Debug --no-build
Passed!  - Failed: 0, Passed: 250, Skipped: 0, Total: 250
```

---

## 6. Human Test Script

### Prerequisites

1. Run the API and Desktop app.
2. Ensure at least one active warehouse exists.
3. Ensure variants exist with retail and/or wholesale prices.
4. Ensure at least one customer exists for selection tests.

### Test A: Open Sales Screen

1. Log in with `SALES_READ` permission.
2. Open `المبيعات` from the sidebar.
3. Confirm the Sales page renders instead of falling back to Home.
4. Confirm list load, search box, paging, and sort buttons appear.

### Test B: Create Anonymous Draft Sale

1. Click `إنشاء فاتورة بيع`.
2. Leave customer empty.
3. Select a warehouse.
4. Add one line.
5. Select a variant.
6. Keep retail price helper or enter a manual price.
7. Save.
8. Confirm:
   - draft invoice is created
   - invoice number is generated by server
   - customer displays as `بدون عميل`
   - success banner appears in NotificationBar

### Test C: Create Customer-Linked Draft Sale With Quick Add

1. Open create form.
2. In customer area, use quick add.
3. Enter a new customer name and optional phone.
4. If suggestions appear, confirm existing customer reuse option is visible.
5. Save customer.
6. Save invoice.
7. Confirm:
   - new customer is selected automatically
   - invoice saves successfully
   - success banner appears

### Test D: Edit Draft Sale

1. Open a draft sale.
2. Click edit.
3. Change notes, warehouse, quantity, price, or discount.
4. Save.
5. Confirm changes persist.
6. Confirm invoice date is shown read-only/locked in edit mode.

### Test E: Post Draft Sale

1. Use a user with `SALES_POST`.
2. Post a draft sale.
3. Confirm:
   - confirmation prompt appears
   - status becomes posted
   - edit/delete actions are disabled or rejected afterward
   - success banner appears

### Test F: Print Sale

1. Open a sale detail or row action.
2. Click print.
3. Confirm print preview / print dialog opens.
4. Confirm the document shows:
   - title `فاتورة بيع`
   - invoice number
   - date
   - customer or anonymous label
   - warehouse
   - cashier
   - notes if present
   - line table with SKU/product/qty/unit price/discount/line total
   - total
   - signature block

### Test G: Permission Checks

1. Log in without `SALES_WRITE`.
2. Confirm create/edit/delete are hidden.
3. Log in without `SALES_POST`.
4. Confirm post is hidden.

---

## 7. Known Limits Kept Intentionally

- no void action for Sales invoices, because no such backend endpoint exists
- no POS flow
- no customer payments screen
- no sales returns UI implementation
- no print profile/config UI
- no shared SearchComboBox refactor

These are intentional scope boundaries, not omissions.