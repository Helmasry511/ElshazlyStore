# UI SALES 2 — POS SCREEN + NUMERIC INPUT FOUNDATION + SOFT REFRESH UX — CLOSEOUT

Date: 2026-03-28
Status: IMPLEMENTED
Scope: POS screen + numeric input foundation + soft refresh/flicker reduction + Sales sort cleanup

---

## 1) Grounded Summary

This phase delivers a new dedicated POS screen as a separate sidebar destination from Sales Admin, while keeping one shared sales execution contract path for:

1. create draft sale
2. post sale
3. optionally create customer payment
4. fetch fresh sale DTO for printing

POS is barcode-first, cashier-speed oriented, Arabic-first/RTL, and backend-grounded.

No backend endpoints were invented.
No tax, no invoice-level discount, no Sales Returns UI, no Customer Payments standalone page, and no Print Config were implemented.

---

## 2) Exact Endpoints Used

POS flow and touched Sales flow use these existing endpoints:

1. POST /api/v1/sales
2. POST /api/v1/sales/{id}/post
3. GET /api/v1/sales/{id}
4. GET /api/v1/barcodes/{barcode}
5. POST /api/v1/payments (named customer only)

Supporting POS header/search operations (existing patterns):

1. GET /api/v1/warehouses
2. GET /api/v1/customers
3. POST /api/v1/customers

Sales list refresh/sort (touched cleanup):

1. GET /api/v1/sales with backend sort values date, number, total (+ _desc)

---

## 3) Files Created / Modified

### Created

1. src/ElshazlyStore.Desktop/ViewModels/POSViewModel.cs
2. src/ElshazlyStore.Desktop/Views/Pages/POSPage.xaml
3. src/ElshazlyStore.Desktop/Views/Pages/POSPage.xaml.cs
4. src/ElshazlyStore.Desktop/Services/SalesExecutionService.cs
5. src/ElshazlyStore.Desktop/Helpers/NumericInputFoundation.cs
6. src/ElshazlyStore.Desktop/Helpers/SalesWarehousePolicy.cs
7. docs/UI SALES 2 — POS SCREEN + NUMERIC INPUT FOUNDATION + SOFT REFRESH UX — CLOSEOUT.md

### Modified

1. src/ElshazlyStore.Desktop/App.xaml.cs
2. src/ElshazlyStore.Desktop/ViewModels/MainViewModel.cs
3. src/ElshazlyStore.Desktop/Views/MainWindow.xaml
4. src/ElshazlyStore.Desktop/ViewModels/SalesViewModel.cs
5. src/ElshazlyStore.Desktop/Views/Pages/SalesPage.xaml
6. src/ElshazlyStore.Desktop/Views/Pages/SalesPage.xaml.cs
7. src/ElshazlyStore.Desktop/ViewModels/PagedListViewModelBase.cs
8. src/ElshazlyStore.Desktop/Localization/Strings.cs
9. src/ElshazlyStore.Desktop/Localization/Strings.resx

---

## 4) POS Navigation + DI + Shell Wiring

Implemented as a real separate destination (not a fallback):

1. New route key: SalesPos
2. MainViewModel navigation case added for SalesPos
3. MainWindow sidebar button added under Sales section
4. MainWindow DataTemplate added for POSViewModel -> POSPage
5. DI registrations added in App.xaml.cs:
   1. POSViewModel
   2. shared ISalesExecutionService

Permission-gated visibility:

1. POS nav visibility is tied to existing sales permissions (read + write + post)
2. POS payment persistence is additionally gated by PAYMENTS_WRITE in POSViewModel logic

---

## 5) One Core Sales Execution Path

A shared desktop service was introduced:

1. ISalesExecutionService / SalesExecutionService

Shared path implemented once and reused:

1. create sale (POST /sales)
2. post sale (POST /sales/{id}/post)
3. optional payment (POST /payments for named customer)
4. fresh sale fetch for print (GET /sales/{id})

Reuse in touched existing flow:

1. SalesViewModel post action now uses shared service post method
2. SalesViewModel print fetch now uses shared service fetch method

This avoids a second contradictory implementation of sale post/fetch semantics.

---

## 6) Barcode-First POS Workflow

Implemented behavior:

1. Dedicated barcode input with Enter-to-scan action
2. Focus is re-requested after scan/add/split/checkout paths via page-level focus hook
3. Successful scan does fast barcode lookup and line fill
4. Repeated scan of same variant merges into existing line and increments quantity
5. Not-found/failed lookup shows truthful error via NotificationBar
6. Inactive variant barcode is blocked with explicit message

No fake state is introduced: basket lines are always based on barcode endpoint response data.

---

## 7) Split Merged Quantity Control

Implemented cashier split actions on each splittable line:

1. Split 1: detach one unit into a separate line
2. Split All: break full quantity into single-unit lines

Safety rules:

1. Split allowed only when quantity is whole number and > 1
2. Non-whole quantities are blocked from split to avoid impossible states
3. Discount amount is redistributed proportionally, preserving total discount and invoice total consistency

---

## 8) POS Header / Customer / Warehouse / Date

Implemented practical POS header with minimal friction:

1. Warehouse selector using shared Sales warehouse policy filtering
2. Optional customer selector with typeahead
3. Anonymous sale shortcut
4. Quick customer add modal with duplicate suggestions reuse pattern
5. Explicit sale mode indicator (anonymous vs named)
6. Invoice date field (safe request normalization preserved via shared execution service)
7. Optional notes and payment reference fields

---

## 9) Immediate Payment Orchestration

### A) Named customer flow (supported paid-now persistence)

POS now executes:

1. create draft sale
2. post sale
3. create payment with amount = invoice total and selected method

Outcome handling:

1. Full success: clear success message
2. Post succeeded but payment failed: explicit split outcome warning with backend error detail (no false success)

### B) Anonymous walk-in flow (truthful no-payment persistence)

POS executes:

1. create draft sale with customerId = null
2. post sale
3. no payment API call

UI explicitly states the limitation:

1. sale posted successfully
2. no backend payment record is created by design for anonymous flow

No fake payment persistence is created.

---

## 10) Totals / Pricing / Discount / Validation

Implemented rules:

1. quantity > 0
2. unitPrice >= 0
3. discountAmount >= 0
4. discountAmount <= line gross amount
5. line total and invoice total always computed from current line model

Pricing helpers:

1. retail button applies retailPrice
2. wholesale button applies wholesalePrice
3. manual override remains editable

Scope preserved:

1. per-line discount only
2. no invoice-level discount
3. no tax

---

## 11) Receipt Printing from POS

After successful post:

1. POS fetches fresh sale DTO from GET /api/v1/sales/{id}
2. Printing uses existing shared architecture via DocumentPrintHelper.PrintSale and ReceiptPrintService

No separate print architecture was introduced.
No Print Config UI was introduced.

---

## 12) Numeric Input Foundation (Carry-Over Requirement)

A reusable numeric foundation was added:

1. NumericInputFoundation (parser/formatter)
2. NumericTextBoxBehavior (reusable attached behavior for TextBox)

Behavior details:

1. Accepts Arabic digits and common separators
2. Normalizes Arabic decimal/group separators to safe parse format
3. On LostFocus/Enter: parses, normalizes, and formats display consistently
4. Applied with configurable fraction digits and auto-scale policy per field

Integer typing policy now implemented:

1. For money fields (unitPrice, discountAmount) where AutoScaleIntegerInput=true and fractionDigits=2:
   1. typing integer without decimal separator is interpreted as minor units
   2. example: 1234 -> 12.34
2. For quantity fields where AutoScaleIntegerInput=false and fractionDigits=3:
   1. integer typing remains integer quantity (displayed with quantity precision formatting)

Applied now (surgical rollout):

1. POS line numeric fields: quantity, unit price, discount
2. Sales Admin touched numeric fields: quantity, unit price, discount

Not rolled out globally to all screens in this phase by design (risk-limited rollout).

---

## 13) Soft Refresh / Flicker Reduction + Sales Sort Cleanup

### Soft refresh and loading behavior

Implemented surgical flicker reduction in touched flows:

1. Added delayed overlay state in PagedListViewModelBase (IsLoadingOverlayVisible with 180ms delay)
2. SalesPage now binds BusyOverlay to delayed state instead of immediate IsLoading
3. POS barcode lookup uses lightweight inline loading indicator (no hard full-page blanking)
4. POS submit overlay is delayed via IsSubmittingOverlayVisible to avoid flash on quick operations

Result: reduced aggressive flash/flicker during quick refresh/lookup operations.

### Sales sort cleanup

Removed extra sort controls from Sales toolbar:

1. removed sort-field combo and sort-direction combo

Replaced with grid-header sorting:

1. sortable headers enabled for invoice number, date, total
2. mapped to backend sort values: number, date, total
3. sort direction toggles by repeated header click
4. backend query sorting still used (not local-only), so paging consistency is preserved
5. non-contract columns remain non-sortable to avoid misleading local sort with paged data

---

## 14) Build / Test / Run Verification

### Build

Command:

1. dotnet build .\ElshazlyStore.sln -c Debug

Result:

1. Build succeeded
2. 0 warnings
3. 0 errors

### Tests

Command:

1. dotnet test .\tests\ElshazlyStore.Tests\ElshazlyStore.Tests.csproj -c Debug --no-build

Result:

1. Passed: 252
2. Failed: 0
3. Skipped: 0

### Desktop startup smoke

Command:

1. dotnet run --project .\src\ElshazlyStore.Desktop\ElshazlyStore.Desktop.csproj -c Debug

Result:

1. Command launched with no immediate terminal error output
2. Full interactive UI validation remains manual (WPF GUI not automated in this environment)

---

## 15) Human Test Script (Concrete)

### Prerequisites

1. API server running
2. Desktop app running
3. At least one active warehouse
4. At least one active variant with barcode and retail/wholesale prices
5. One cashier user with sales + payments permissions
6. One cashier user without payments permission (optional negative test)

### A) POS route and permission wiring

1. Login with full sales permissions
2. Confirm sidebar shows POS entry
3. Open POS and confirm it loads POS page (not Home fallback)
4. Login with read-only sales user and confirm POS nav is hidden

### B) Barcode merge flow

1. On POS, scan a valid barcode once
2. Confirm one line added, qty = 1
3. Scan same barcode again
4. Confirm same line qty increments, no duplicate line added
5. Confirm barcode field focus remains practical for repeated scanning

### C) Split merged line flow

1. With line qty > 1, click Split 1
2. Confirm one separate line is created and totals remain unchanged
3. Click Split All on a merged line
4. Confirm quantity is split into unit lines and totals remain unchanged
5. Enter non-whole qty (example 1.5) then try split
6. Confirm split is blocked with truthful validation

### D) Anonymous paid-now flow truthfulness

1. Set anonymous mode (no customer selected)
2. Scan/add lines and complete sale
3. Confirm sale is created and posted
4. Confirm notification clearly states no backend payment record is persisted for anonymous flow
5. Confirm receipt prints from fresh sale fetch

### E) Named customer paid-now full success

1. Select existing customer
2. Choose payment method Cash/Visa/InstaPay/EWallet (for EWallet fill wallet name)
3. Complete sale
4. Confirm sequence succeeds: create + post + payment
5. Confirm success message indicates payment persisted
6. Confirm receipt prints

### F) Named customer partial-success transparency

1. Select customer
2. Choose Other (manual) method and enter unsupported text to force backend rejection
3. Complete sale
4. Confirm sale still posts successfully
5. Confirm payment step fails with clear split-outcome warning
6. Confirm no false full-success message

### G) Numeric input behavior

1. In POS price field (auto-scale), type 1234 and leave field
2. Confirm value formats as 12.34
3. In POS discount field, type Arabic digits and confirm parse/format works
4. In quantity field, type integer (example 5) and confirm it remains quantity value (not money auto-scale)
5. Repeat checks in Sales Admin line editor for touched numeric fields

### H) Sales sort cleanup and behavior

1. Open Sales screen
2. Confirm old sort combos are removed
3. Click invoice number/date/total headers
4. Confirm direction toggles and list reloads through backend sort
5. Confirm paging remains coherent while sorting

---

## 16) Explicit Non-Scope Confirmation

The following were NOT implemented in this phase (by requirement):

1. Sales Returns UI
2. Customer Payments standalone page
3. Print Config screen
4. Invoice-level discount
5. Tax
6. Backend CustomerType additions
7. Backend WarehouseType additions
8. Any backend endpoint invention
9. Any fake anonymous payment persistence

---

## 17) Next Recommended Phase (after human approval)

Recommended next phase:

1. UI SALES 3 — Sales Returns screen implementation, strictly honoring current backend constraints (draft-only void, posted-return limitations, current disposition policy)

Stop condition honored: this phase ends here and does not proceed into Sales Returns / Customer Payments page / Print Config work.
