# UI SALES 2-R2 — CASHIER FINALIZATION + RECEIPT PAYMENT TRACE + EXPLICIT CREDIT MODE — CLOSEOUT

Date: 2026-03-29
Status: IMPLEMENTED — PENDING HUMAN CASHIER RETEST
Scope: Surgical cashier/POS R2 only. No Sales Returns UI, no Customer Payments standalone page, no Print Config, no backend endpoint invention, no global flicker rollout, and no whole-app redesign.

---

## 1) Grounded Outcome

This phase closes the proven cashier blockers left open after UI SALES 2-R1 without rebuilding the module.

Implemented in this phase:

1. the remaining dark-mode input readability issue was fixed at the shared themed input/date style layer and applied to the actual shared date-filter screen still affected in the current codebase
2. cashier payment state is now explicit and operator-visible instead of being implied only by tender wording
3. partial payment with remaining on customer is now an explicit cashier mode for named cash sales
4. cashier-created customer payments now link back to the posted sale through the existing ledger relation so later sale reprints can show truthful payment trace
5. printed sales document now includes payment method, payment reference, paid amount, remaining amount, and clearer payment-state labeling where truthfully supported
6. current retail/wholesale chips under cashier lines were removed completely
7. cashier row hierarchy was tightened: larger product name and barcode, smaller row editors with more readable text, stronger checkout zone, smaller secondary fields
8. shared print layout now adapts page padding/column width by printable paper width through the existing shared receipt system path
9. cashier/payment failure and print-failure messages were hardened to avoid raw server fragments

Not implemented in this phase:

1. Sales Returns UI
2. Customer Payments standalone page
3. Print Config
4. global old-screen flicker rollout
5. whole-app theme rewrite
6. formal pricing override / discount governance phase

---

## 2) Exact Files Changed

1. src/ElshazlyStore.Desktop/ViewModels/POSViewModel.cs
2. src/ElshazlyStore.Desktop/Views/Pages/POSPage.xaml
3. src/ElshazlyStore.Desktop/Helpers/DocumentPrintHelper.cs
4. src/ElshazlyStore.Desktop/Services/Printing/ReceiptPrintService.cs
5. src/ElshazlyStore.Desktop/Resources/Themes/FormStyles.xaml
6. src/ElshazlyStore.Desktop/Resources/Themes/DarkTheme.xaml
7. src/ElshazlyStore.Desktop/Resources/Themes/LightTheme.xaml
8. src/ElshazlyStore.Desktop/Views/Pages/StockLedgerPage.xaml
9. src/ElshazlyStore.Desktop/Models/Dtos/SalesDto.cs
10. src/ElshazlyStore.Desktop/Models/Dtos/PaymentDto.cs
11. src/ElshazlyStore.Desktop/Services/SalesExecutionService.cs
12. src/ElshazlyStore.Desktop/Localization/Strings.cs
13. src/ElshazlyStore.Desktop/Localization/Strings.resx
14. src/ElshazlyStore.Infrastructure/Services/SalesService.cs
15. src/ElshazlyStore.Infrastructure/Services/AccountingService.cs
16. src/ElshazlyStore.Infrastructure/Services/ImportService.cs
17. src/ElshazlyStore.Api/Endpoints/PaymentEndpoints.cs
18. tests/ElshazlyStore.Tests/Api/SalesInvoiceTests.cs
19. docs/UI SALES 2-R2 — CASHIER FINALIZATION + RECEIPT PAYMENT TRACE + EXPLICIT CREDIT MODE — CLOSEOUT.md

---

## 3) Exact Dark-Mode Bug Root Cause and Fix

### 3A. Root cause actually present in the codebase now

The prior closure claim was incomplete for two separate reasons:

1. the shared compact input styling was still too weak for real cashier usage in dark mode because the compact fields had small typography and the input surface/border contrast was still not strong enough under dark theme
2. the still-affected shared date-input path in the current workspace was not Stock Movements itself; it was Stock Ledger date filters, which were still using bare `DatePicker` controls instead of the shared themed date-picker style
3. the shared themed input layer still was not explicitly forcing enough inner text foreground through `TextElement.Foreground` / `TextBox.Foreground` on all touched text/combo/date paths

### 3B. Exact fix applied

1. dark and light theme input brushes were strengthened for clearer input-surface separation
2. `ThemedTextBoxStyle`, `ThemedComboBoxStyle`, and `ThemedDatePickerStyle` now explicitly apply stronger foreground propagation for inner text surfaces
3. `DatePickerTextBox` styling was strengthened so typed date text does not fall back to weak/default colors
4. compact input styles were rebalanced so the fields stay physically smaller while the text remains readable
5. Stock Ledger date filters were moved onto the shared themed date-picker style, which is the actual shared date-input screen still affected in this codebase

### 3C. Verification truth

Verified code paths after the fix:

1. cashier inputs on `POSPage`
2. touched Sales inputs already using the same shared style layer
3. Stock Ledger date inputs using the same themed date picker

Important precision note:

The user report referenced a stock-movement date issue. In the current workspace implementation, the shared date-picker path that actually exists and needed fixing is `StockLedgerPage`, not `StockMovementsPage`, which does not currently expose a date picker.

---

## 4) Exact Cashier Payment Modes Now Supported

The cashier now exposes explicit grounded modes instead of leaving the operator to infer behavior from the tendered field alone.

### A) Anonymous immediate sale

1. visible as a distinct cashier payment state
2. no backend payment record is created
3. payment method/reference can still be used operationally on the cashier surface and immediate print override
4. any such anonymous payment trace is clearly marked operational-only, not persisted payment truth
5. anonymous underpaid sale remains blocked

### B) Named customer full cash sale

1. visible as explicit `سداد كامل`
2. cashier can enter received amount for change calculation
3. backend persists payment only up to invoice total
4. over-tender remains cashier-side change display only
5. under-tender is blocked in full-payment mode

### C) Named customer partial cash sale with remaining on customer

1. visible as explicit `دفع جزئي مع متبقي`
2. requires named customer
3. requires cash method
4. requires the temporary credit gate described in section 6
5. entered amount must be below invoice total
6. the actual paid amount is persisted via `/api/v1/payments`
7. the remaining amount stays outstanding on the customer through the existing receivable/ledger behavior

### D) Named customer fully paid non-cash sale

1. visible as distinct named fully-paid non-cash mode
2. supported methods remain grounded to current backend contract only: `Cash`, `Visa`, `EWallet`, `InstaPay`
3. unsupported manual payment-method path was removed from cashier UI because the backend does not accept arbitrary payment method values

---

## 5) Exact Persistence Truth vs Operational-Only Truth

### 5A. Persisted truth now available

1. sale create/post still uses the same existing sales contract
2. named-customer payment persistence still uses `POST /api/v1/payments`
3. this phase added an optional `relatedInvoiceId` field to the existing payment create contract so cashier-created payments can link back to the sale through the already-existing `LedgerEntry.RelatedInvoiceId`
4. `GET /api/v1/sales/{id}` now returns sale-level payment trace for linked cashier payments:
   1. paid amount
   2. remaining amount
   3. payment method
   4. wallet name when relevant
   5. payment reference
   6. payment count
5. later reprints of linked named-customer cashier sales now stay truthful because the trace comes from backend-linked sale/payment data, not cashier memory

### 5B. Operational-only truth intentionally preserved

1. anonymous sale still creates no backend payment record
2. anonymous payment method/reference and optional paid/remaining display are immediate cashier/print context only
3. when an anonymous sale is printed immediately after checkout, the document marks this payment trace as operational-only
4. when an anonymous sale is printed later from sale data only, payment amount/reference fields show that the data was not saved in the backend instead of pretending it exists

### 5C. Remaining amount truth

1. named customer partial sale: remaining amount is authoritative via sale total minus linked persisted payment amount
2. named customer sale with no linked payment trace: the sale print shows the outstanding amount still remaining on the customer
3. anonymous sale: remaining amount is not treated as a persisted receivable, because that would be false under the current backend contract

---

## 6) Credit / Permission Micro-Audit

### 6A. What exact permission currently gates credit / remaining sales?

There is still no dedicated backend/domain permission such as:

1. `SALES_CREDIT`
2. `SALES_RECEIVABLE_OVERRIDE`
3. `SALES_MANAGER_APPROVAL`

### 6B. Exact temporary grounded gate used in this phase

This phase uses the safest grounded Desktop-side restriction available from existing permission inventory:

1. `PAYMENTS_WRITE`
2. `ACCOUNTING_READ`

Meaning:

1. a user must already be able to persist customer payments
2. a user must also have accounting visibility before the cashier unlocks explicit partial/remaining mode
3. users without that combined gate can still perform full-payment cashier flows, but not explicit remaining-on-customer flow

Why this gate was chosen:

1. it avoids brittle role-name checks such as hardcoding `Admin` or `Manager`
2. it is stricter than plain payment-write alone
3. it aligns the temporary credit unlock with roles that can already see AR/AP state

Important truth:

This is a Desktop gate, not a new backend policy model. A formal backend-enforced credit authorization permission still remains future work.

### 6C. Next small phase needed

The next small formalization phase should add:

1. explicit manager-only credit authorization permission and backend validation
2. explicit pricing override permission and audit trail
3. explicit discount policy / approval contract for sales lines

No attempt was made to broaden into that redesign here.

---

## 7) Exact Removal of Retail / Wholesale Chips

The current retail/wholesale buttons under cashier lines were removed entirely from `POSPage`.

What remains true:

1. barcode lookup still provides retail/wholesale data from the backend
2. the cashier still starts from the grounded default unit price resolved from barcode data
3. no new pricing-policy engine or override governance was introduced in this phase

Formal pricing/discount governance remains intentionally deferred.

---

## 8) Exact Cashier Layout / Hierarchy Improvements

Implemented cashier hierarchy changes:

1. product name is now the dominant line text
2. barcode text is larger and clearer
3. SKU / color / size meta moved into a secondary line under the product name instead of competing with it
4. qty / unit price / discount editors are physically narrower but use a denser, more readable text style
5. secondary payment fields (`wallet`, `reference`, `notes`) now use a smaller secondary field style
6. explicit payment-state card added at the top of the payment section
7. explicit cash-mode selector added for named cash flows
8. total / checkout card now has stronger border emphasis, larger total typography, and clearer hierarchy
9. unnecessary barcode helper text was removed to keep the flow calmer

---

## 9) Exact Receipt / Sales Document Enhancements

The shared sale print path now includes stronger traceability fields through the existing `DocumentPrintHelper.PrintSale()` and `ReceiptPrintService` architecture.

Added / strengthened sale document fields:

1. professional header and title
2. invoice number
3. invoice date/time
4. customer or anonymous state
5. warehouse
6. cashier/user
7. posted time
8. invoice total
9. payment state label
10. payment method
11. payment reference
12. paid amount
13. remaining amount
14. notes only when present
15. line items table
16. signature block

Truthfulness rules now implemented on print:

1. linked named-customer cashier payments print from persisted sale/payment trace
2. partial paid sales print paid + remaining from authoritative linked values
3. anonymous immediate print can show operational payment trace only when it exists in the cashier session, and it is explicitly marked operational-only
4. later anonymous reprint does not pretend that payment trace exists in the backend when it does not

---

## 10) Thermal / A5 Readiness Through Shared Print System

No separate chaotic sale print path was introduced.

This phase keeps one shared document path and makes it more resilient by:

1. continuing to print sale documents through `DocumentPrintHelper` + `ReceiptPrintService`
2. adapting shared `PrintDocument()` page padding and effective column width based on printable paper width
3. keeping the content built from shared field rows, field pairs, notes, tables, and signature blocks so the same document structure survives both narrower and wider printable widths

Practical result:

1. narrow thermal-style paper gets tighter shared padding
2. mid-width A5-style output gets moderate padding
3. wider layouts keep the standard padding path

No Print Config UI or profile-selection logic was implemented in this phase.

---

## 11) Exact Notification / Message Improvements

Cashier message behavior was hardened further in this phase:

1. payment failure success-split message no longer appends raw server fragments to the operator message
2. print-fetch failure message no longer appends raw server fragments to the operator message
3. mode descriptions and persistence hints now explicitly explain when a flow is operational-only vs persisted
4. partial-payment validation now uses direct cashier wording instead of vague tender language
5. full-payment under-tender now tells the operator to switch to partial mode when allowed instead of silently implying credit

Notification hygiene from R1 remains preserved:

1. stale page-entry notification clearing still runs on navigation re-entry
2. raw API detail suppression remains in the touched client error path

---

## 12) Explicit Deferred Items

Still deferred after this phase:

1. global flicker rollout across older screens
2. whole-app theme rollout
3. formal pricing override governance phase
4. formal discount governance phase
5. Print Config
6. Sales Returns UI
7. Customer Payments standalone page

Explicit non-scope confirmation:

1. Sales Returns was NOT implemented in this phase
2. Customer Payments standalone page was NOT implemented in this phase
3. Print Config was NOT implemented in this phase

---

## 13) Build / Test / Run Result

### Build

Command:

`dotnet build .\ElshazlyStore.sln -c Debug`

Result:

1. Build succeeded
2. 0 warnings
3. 0 errors

### Targeted regression subset

Command:

`dotnet test .\tests\ElshazlyStore.Tests\ElshazlyStore.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~SalesInvoiceTests|FullyQualifiedName~AccountingPaymentTests"`

Result:

1. Passed: 33
2. Failed: 0

### Full automated test suite

Command:

`dotnet test .\tests\ElshazlyStore.Tests\ElshazlyStore.Tests.csproj -c Debug --no-build`

Result:

1. Passed: 253
2. Failed: 0

### Runtime smoke check

API:

1. `dotnet run --project .\src\ElshazlyStore.Api\ElshazlyStore.Api.csproj -c Debug --no-build`
2. API started successfully
3. bound to `http://localhost:5238`

Desktop:

1. `dotnet run --project .\src\ElshazlyStore.Desktop\ElshazlyStore.Desktop.csproj -c Debug --no-build`
2. Desktop process launched without immediate console crash
3. runtime environment had an expired stored session token, so the app immediately hit `GET /api/v1/auth/me` with a 401 due expired auth
4. that is an environment/session-state issue for manual retest, not a cashier compile failure

---

## 14) Concrete Human Test Script

### Pre-setup

1. start API
2. start Desktop
3. sign in again if the Desktop opens with an expired cached session
4. enable dark mode in Desktop
5. use a user with:
   1. sales read/write/post
   2. payments write
   3. accounting read
   for the partial-credit scenario

### Test A — Dark-mode readability

1. open `الكاشير`
2. verify barcode input, warehouse/customer controls, payment fields, and line editors in dark mode
3. open Sales Admin and verify the touched compact inputs are still readable in dark mode
4. open Stock Ledger and verify the date filters are readable in dark mode and use the themed date surface
5. confirm text is readable in normal, focused, and selected states

### Test B — Full paid cash sale

1. open `الكاشير`
2. choose warehouse
3. add named customer
4. scan/add product line
5. keep payment method `نقدي`
6. keep cash mode `سداد كامل`
7. enter received amount greater than total
8. complete sale
9. confirm:
   1. success message is clear
   2. change due appears clearly
   3. printed document shows full-paid state
   4. printed document shows payment method
   5. printed document shows payment reference if entered
   6. printed document shows paid amount and remaining amount = 0

### Test C — Partial paid sale with remaining on customer

1. sign in with the user that has `PAYMENTS_WRITE` + `ACCOUNTING_READ`
2. open `الكاشير`
3. choose named customer
4. scan/add product line
5. keep payment method `نقدي`
6. switch cash mode to `دفع جزئي مع متبقي`
7. enter a paid-now amount below invoice total
8. complete sale
9. confirm:
   1. sale posts successfully
   2. partial payment success message is explicit
   3. printed document shows partial/remaining state
   4. printed document shows paid amount and remaining amount correctly
   5. later open Sales Admin, print the same sale again, and confirm the payment trace still appears from server-linked sale/payment data

### Test D — Credit gate behavior

1. sign in with a user that can use cashier but does NOT have `ACCOUNTING_READ`
2. open named customer cash sale
3. confirm partial/remaining mode is not usable as an authorized cash-credit path
4. enter a cash amount below total while still in full mode
5. confirm cashier blocks the action with a clear message instead of silently creating remaining-on-customer behavior

### Test E — Named fully paid non-cash sale

1. open named customer sale
2. choose `فيزا` or `إنستاباي` or `محفظة إلكترونية`
3. for wallet, enter wallet name
4. enter payment reference
5. complete sale
6. confirm:
   1. no cash tender box is shown
   2. payment persists normally
   3. printed document shows non-cash method and reference clearly
   4. later reprint still shows payment trace

### Test F — Anonymous immediate sale

1. switch cashier to anonymous sale
2. add item(s)
3. optionally choose operational payment method / reference
4. if using cash, optionally enter received amount for change
5. complete sale
6. confirm:
   1. sale posts successfully
   2. no backend payment record is implied in the message
   3. immediate printed document clearly marks the payment trace as operational-only if shown
   4. later reprint from Sales Admin does NOT pretend the anonymous payment trace was persisted

### Test G — Receipt layout path

1. print one cashier sale to a narrow receipt-style target such as Microsoft Print to PDF with a narrow page setup if available
2. print the same sale to a wider/A5-style target
3. confirm both outputs keep:
   1. readable header
   2. payment trace block
   3. lines table
   4. signature/footer structure
   5. no broken overlapping layout

---

## 15) Final Scope Truth

This phase stopped after cashier finalization and document/payment trace hardening.

Specifically NOT implemented:

1. Sales Returns UI
2. Customer Payments standalone page
3. Print Config
4. whole-app flicker fix rollout
5. full pricing-policy engine
6. discount governance redesign
