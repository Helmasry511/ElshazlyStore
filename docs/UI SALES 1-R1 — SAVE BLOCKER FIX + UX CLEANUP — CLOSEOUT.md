# UI SALES 1-R1 — SAVE BLOCKER FIX + UX CLEANUP — CLOSEOUT

**Date:** 2026-03-28  
**Status:** IMPLEMENTED — PENDING HUMAN RETEST  
**Scope:** Surgical Sales Admin fix only. No POS, no Sales Returns UI, no Customer Payments UI, no Print Config work.

---

## 1. Grounded Outcome

This phase fixed the proven Sales save blocker and the two UX failures that made the blocker misleading:

- Sales create now normalizes invoice dates to UTC-compatible values before persistence.
- The backend no longer reports generic `DbUpdateException` save failures as fake `CONFLICT` duplicate-value errors.
- The Sales form no longer leaves a false success signal behind when quick-add customer succeeds but invoice save fails.
- The Sales list sort buttons were replaced with a cleaner sort selector + direction pattern.
- Customer re-activation was confirmed to already exist in the backend contract and is now wired in the Desktop UI.

Manual GUI retest is still required from the human side because this environment can build, run, and smoke-check startup, but cannot drive the WPF UI interactively.

---

## 2. Before-Fix Evidence

### Human / server truth source

Human testing reported the real server failure on `POST /api/v1/sales`:

- `DbUpdateException`
- Npgsql error: `Cannot write DateTime with Kind=Local to PostgreSQL type 'timestamp with time zone', only UTC is supported.`

### Desktop trace evidence of the misleading UX

The Desktop logs showed the failure surfacing as conflict/duplicate-style UX instead of a save/infrastructure failure:

- `src/ElshazlyStore.Desktop/bin/Debug/net8.0-windows/logs/api-trace.log:672`
  - `POST /api/v1/sales → 409 ... title="CONFLICT" detail="An error occurred while saving the entity changes. See the inner exception for details."`
- `src/ElshazlyStore.Desktop/logs/elshazly-desktop-20260328.log:3283`
  - `API error 409 on POST /api/v1/sales: [409] — CONFLICT — يوجد تعارض — القيمة مستخدمة بالفعل`

That matched the human report exactly: the real failure was a date persistence problem, but the UI was implying a duplicate/conflict problem.

---

## 3. Exact Root Cause

The blocker was a two-layer bug:

1. **Desktop create path sent a raw non-UTC invoice date**
   - The Sales form used a WPF-bound `DateTime?` (`FormInvoiceDate`) initialized from local time / `DatePicker` values.
   - The create request posted that value directly as `invoiceDateUtc`.

2. **Backend Sales create path trusted the incoming `DateTime` kind**
   - `SalesService.CreateAsync()` passed `request.InvoiceDateUtc` straight into `SalesInvoice.InvoiceDateUtc`.
   - PostgreSQL `timestamp with time zone` under Npgsql requires `DateTimeKind.Utc`.

Then a third layer made the failure misleading:

3. **Global exception mapping mislabeled generic save failures as `CONFLICT`**
   - `GlobalExceptionMiddleware` mapped every unhandled `DbUpdateException` to `409 CONFLICT`.
   - Desktop `ErrorCodeMapper` then translated `CONFLICT` as `يوجد تعارض — القيمة مستخدمة بالفعل`.

That is why a DateTime persistence failure looked like a duplicate-value business conflict.

---

## 4. Exact Files / Lines / Layers Fixed

### Backend error classification

- `src/ElshazlyStore.Api/Middleware/GlobalExceptionMiddleware.cs:42-126`
  - `WriteProblemDetailsAsync()` now uses explicit exception classification instead of mapping every `DbUpdateException` to `409 CONFLICT`.
  - `ClassifyDbUpdateException()` keeps real unique-constraint style failures as `CONFLICT`, but reclassifies generic save failures as `INTERNAL_ERROR` and unwraps the innermost error detail.

### Backend Sales UTC guard

- `src/ElshazlyStore.Infrastructure/Services/SalesService.cs:140-146`
  - Sales create now routes the incoming invoice date through a normalization step before assignment.
- `src/ElshazlyStore.Infrastructure/Services/SalesService.cs:493-507`
  - Added `NormalizeInvoiceDateUtc(DateTime?)` so Sales create never persists local/unspecified values into PostgreSQL `timestamptz`.

### Desktop Sales request normalization + honest save UX

- `src/ElshazlyStore.Desktop/ViewModels/SalesViewModel.cs:78-89`
  - Added sort-selection state and inline `FormInfo` state.
- `src/ElshazlyStore.Desktop/ViewModels/SalesViewModel.cs:414-447`
  - Sales create now posts `NormalizeInvoiceDateForRequest(FormInvoiceDate)` instead of the raw bound `DateTime`.
  - Save failures now flow through `BuildSaveFailureMessage()` instead of showing a stale or misleading success state.
- `src/ElshazlyStore.Desktop/ViewModels/SalesViewModel.cs:675`
  - Quick-add customer success now sets an inline “customer created, invoice not saved yet” note instead of a success banner.
- `src/ElshazlyStore.Desktop/ViewModels/SalesViewModel.cs:893-918`
  - Added explicit save-failure message building and desktop-side invoice-date normalization.

### Desktop truthful internal-error display

- `src/ElshazlyStore.Desktop/Models/ProblemDetails.cs:49-55`
  - `INTERNAL_ERROR` responses now retain useful server detail in Desktop messaging instead of collapsing to a generic Arabic message only.

### Sort UX cleanup

- `src/ElshazlyStore.Desktop/Views/Pages/SalesPage.xaml:64-75`
  - Replaced the three clunky sort buttons with one sort-field combo box plus one direction combo box.
- `src/ElshazlyStore.Desktop/ViewModels/SalesViewModel.cs:155-156, 826-853`
  - Added sort selection handling that maps the new UI back onto the existing backend `sort` query contract.

### Customer re-activation micro-fix

- Backend support already existed:
  - `src/ElshazlyStore.Api/Endpoints/CustomerEndpoints.cs:154`
    - `if (req.IsActive.HasValue) customer.IsActive = req.IsActive.Value;`
  - `src/ElshazlyStore.Api/Endpoints/CustomerEndpoints.cs:183-184`
    - update contract already exposed `bool? IsActive`.
- Desktop DTO support already existed:
  - `src/ElshazlyStore.Desktop/Models/Dtos/CustomerDto.cs:24-31`
    - `UpdateCustomerRequest.IsActive` already present.
- UI wiring added in this phase:
  - `src/ElshazlyStore.Desktop/ViewModels/CustomersViewModel.cs:169-186`
    - added `ReactivateAsync()`.
  - `src/ElshazlyStore.Desktop/Views/Pages/CustomersPage.xaml:103-108`
    - added a Reactivate button for inactive customers.

### Regression tests

- `tests/ElshazlyStore.Tests/Api/SalesInvoiceTests.cs:77-126`
  - added a local-DateTime normalization test.
  - added an unspecified-DateTime date-only normalization test.

---

## 5. Why the DateTime Bug Happened

This was not a PostgreSQL schema bug. The schema was already correct.

The failure happened because:

- Sales create accepts `invoiceDateUtc`.
- The WPF create form was using a local / date-picker `DateTime` value.
- That value reached EF unchanged.
- Npgsql rejects `Local` or `Unspecified` `DateTime` for PostgreSQL `timestamp with time zone`.

So the problem was the **kind** on the `DateTime`, not the field name, not the table schema, and not the invoice number logic.

---

## 6. How UTC Is Now Guaranteed

UTC compatibility is now guaranteed in the two correct places for a surgical fix:

### 1) Desktop request construction guard

`SalesViewModel.NormalizeInvoiceDateForRequest()` ensures the Sales create request does not send raw local/unspecified values.

- `Utc` stays `Utc`
- date-only / midnight `DatePicker` values are stamped as UTC without shifting the chosen calendar date
- non-midnight local values are converted to UTC

### 2) Backend source-of-truth guard

`SalesService.NormalizeInvoiceDateUtc()` repeats the normalization on the server before persistence.

This is the critical backstop because the backend is the single source of truth and must not trust every client to send a correctly-kinded `DateTime`.

### Sales create/update/edit/post audit result

- **Create:** fixed on Desktop and guarded again in the service.
- **Update:** no `invoiceDateUtc` field exists in the update contract, so the failing date path does not exist there.
- **Edit draft:** edit only reads the existing server value back into the form; it does not persist invoice date changes in update mode.
- **Post:** already uses server-side `DateTime.UtcNow`; no client-supplied invoice date flows through posting.

---

## 7. How Error Mapping Was Corrected

Before this phase:

- generic `DbUpdateException` => `409 CONFLICT`
- Desktop `CONFLICT` => duplicate-value Arabic text

After this phase:

- actual duplicate / unique-constraint style failures still map to `409 CONFLICT`
- generic database save failures map to `500 INTERNAL_ERROR`
- the Desktop now preserves the useful inner detail for `INTERNAL_ERROR` in Development so the operator sees a grounded technical cause instead of a fake business conflict

This is surgical because it fixes the misleading `DbUpdateException` bucket without changing already-proven business conflict codes such as real duplicate-code or duplicate-identifier scenarios.

---

## 8. How False Success Was Corrected

Before this phase:

- quick-add customer success raised a success NotificationBar immediately
- if invoice save failed afterward, the operator still had a positive success signal on screen even though the invoice was not saved

After this phase:

- quick-add customer success now sets an inline form note: customer created and selected, invoice not saved yet
- no invoice success notification is shown unless the Sales create/update API call succeeds
- if quick-add customer succeeded but invoice save fails, the form error explicitly says:
  - customer creation succeeded
  - invoice save failed
- the success notification is only set after the Sales save call succeeds and the list reload is executed

This removes the false-success path without changing the validated NotificationBar pattern for real confirmed saves.

---

## 9. Sort UX Change

The three separate Sales sort buttons were removed:

- `فرز التاريخ`
- `فرز الرقم`
- `فرز الإجمالي`

They were replaced by:

- one sort-field dropdown
- one direction dropdown

This keeps the existing admin-toolbar visual language but makes sorting clearer and less clunky.

No backend sort contract was changed. The Desktop still maps back to the same `sort` query values (`date`, `number`, `total`, plus `_desc`).

---

## 10. Customer Active / Inactive Micro-Check Result

This was **already supported by the backend** and was missing only in Desktop UI.

Grounded result:

- backend update endpoint already accepts `IsActive`
- Desktop DTO already exposed `UpdateCustomerRequest.IsActive`
- only the deactivate action had been wired in the Customers screen

So this phase safely included the UI-only reactivate fix. No backend change was invented.

---

## 11. Build / Test / Run Result

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
Passed!  - Failed: 0, Passed: 252, Skipped: 0, Total: 252
```

### Runtime smoke

```text
dotnet run --project .\src\ElshazlyStore.Api\ElshazlyStore.Api.csproj
Application started successfully and is listening on http://localhost:5238

dotnet run --project .\src\ElshazlyStore.Desktop\ElshazlyStore.Desktop.csproj
Desktop process launched without startup errors in terminal output
```

### Verification limit

I could build, test, and start both processes here, but I could **not** click through the WPF UI automatically in this environment.

So the following still require human execution on the real screen:

- save sales invoice with customer
- save sales invoice without customer
- edit draft then save again
- verify list refresh visually
- verify no false success visually
- verify the new sort UI visually
- verify customer quick-add split outcome visually

---

## 12. Human Test Script

### A. Save Sales Invoice With Customer

1. Open `المبيعات`.
2. Click `إنشاء فاتورة بيع`.
3. Pick a warehouse.
4. Select an existing customer.
5. Add one line and save.
6. Confirm:
   - save succeeds
   - success message appears only after save completes
   - draft appears in the list

### B. Save Sales Invoice Without Customer

1. Open create form.
2. Use `بيع بدون عميل`.
3. Add one line and save.
4. Confirm:
   - save succeeds
   - customer displays as `بدون عميل`
   - no false customer-linked message appears

### C. Edit Draft Then Save Again

1. Open a draft sale.
2. Change notes / warehouse / lines.
3. Save.
4. Confirm:
   - save succeeds
   - updated values appear in details and list
   - no invoice-date write error appears

### D. Quick-Add Customer Positive Path

1. Open create form.
2. Use quick-add customer.
3. Create a new customer.
4. Confirm the form shows that the customer was created and selected, but the invoice is not saved yet.
5. Save the invoice.
6. Confirm:
   - invoice success appears only after invoice save succeeds
   - no earlier fake success remained on screen

### E. Quick-Add Customer Split-Outcome Negative Path

1. Open create form.
2. Use quick-add customer and create a new customer.
3. Before saving the invoice, force an API save failure in a safe dev-only way
   - example: stop the API process after customer creation and before clicking Save
4. Click Save.
5. Confirm:
   - the form clearly says customer creation succeeded
   - the form clearly says invoice save failed
   - no invoice success notification appears

### F. Sort UX

1. Open Sales list.
2. Use the new sort field dropdown.
3. Switch among date / number / total.
4. Change direction.
5. Confirm the list reorders correctly and the new control is clearer than the old three-button pattern.

### G. Customer Reactivation Micro-Check

1. Open `العملاء`.
2. Deactivate one active customer.
3. Confirm the row now shows a Reactivate action.
4. Click Reactivate.
5. Confirm the customer returns to active status.

---

## 13. Explicit Non-Scope Statement

The following were **not** implemented in this phase:

- POS
- Sales Returns UI
- Customer Payments UI
- Print Config work
- broad Sales refactor
- backend contract invention

This phase was a surgical blocker fix and UX cleanup only.