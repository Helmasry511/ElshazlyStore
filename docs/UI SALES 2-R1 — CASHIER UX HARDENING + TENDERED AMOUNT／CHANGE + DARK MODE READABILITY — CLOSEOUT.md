# UI SALES 2-R1 — CASHIER UX HARDENING + TENDERED AMOUNT/CHANGE + DARK MODE READABILITY — CLOSEOUT

Date: 2026-03-29
Status: IMPLEMENTED — PENDING HUMAN CASHIER RETEST
Scope: Surgical cashier/POS R1 hardening only. No Sales Returns UI, no Customer Payments standalone page, no Print Config, no backend contract invention, no whole-app redesign.

---

## 1) Grounded Outcome

This R1 closes the proven cashier-operability gaps on the Desktop POS screen without rebuilding the phase.

Implemented in this phase:

1. cashier-side paid-amount input for cash flow
2. live remaining/change calculation against invoice total
3. truthful named-customer partial cash payment persistence when underpaid
4. truthful change-due UX when cash tender exceeds invoice total
5. stronger total/checkout emphasis and lighter secondary fields
6. dark-mode typed-input readability hardening on POS and touched Sales Admin fields
7. centered quick-add customer overlay on the cashier screen
8. NotificationBar hygiene for touched Sales/POS flows
9. user-facing rename from `POS / نقطة البيع` to `الكاشير`

Not implemented in this phase:

1. whole-app anti-flicker rollout
2. whole-app numeric-input rollout beyond already touched screens
3. Sales Returns UI
4. Customer Payments standalone page
5. Print Config
6. backend price/discount governance invention

---

## 2) Exact Files Changed

1. `src/ElshazlyStore.Desktop/ViewModels/POSViewModel.cs`
2. `src/ElshazlyStore.Desktop/Views/Pages/POSPage.xaml`
3. `src/ElshazlyStore.Desktop/Views/Pages/POSPage.xaml.cs`
4. `src/ElshazlyStore.Desktop/Services/SalesExecutionService.cs`
5. `src/ElshazlyStore.Desktop/ViewModels/SalesViewModel.cs`
6. `src/ElshazlyStore.Desktop/Views/Pages/SalesPage.xaml`
7. `src/ElshazlyStore.Desktop/Views/Pages/SalesPage.xaml.cs`
8. `src/ElshazlyStore.Desktop/Resources/Themes/FormStyles.xaml`
9. `src/ElshazlyStore.Desktop/Resources/Themes/DarkTheme.xaml`
10. `src/ElshazlyStore.Desktop/Resources/Themes/LightTheme.xaml`
11. `src/ElshazlyStore.Desktop/Services/Api/ApiClient.cs`
12. `src/ElshazlyStore.Desktop/Models/ProblemDetails.cs`
13. `src/ElshazlyStore.Desktop/Models/ErrorCodeMapper.cs`
14. `src/ElshazlyStore.Desktop/Localization/Strings.cs`
15. `src/ElshazlyStore.Desktop/Localization/Strings.resx`
16. `docs/UI SALES 2-R1 — CASHIER UX HARDENING + TENDERED AMOUNT／CHANGE + DARK MODE READABILITY — CLOSEOUT.md`

Note: Windows cannot create a file containing a literal `/`, so the on-disk filename uses the visually equivalent fullwidth slash `／` while preserving the requested wording.

---

## 3) Exact Cashier UX Issues Fixed

1. The cashier now has a dedicated `المبلغ المستلم` field in the emphasized total card.
2. Remaining/change updates live from the current invoice total and tendered amount.
3. The primary checkout action is now embedded in the emphasized total card and marked as the default Enter action.
4. Success messaging is clearer and cash-aware.
5. Secondary fields (`مرجع الدفع`, `ملاحظات داخلية`) are visually lighter and smaller.
6. Noisy helper copy under the warehouse selector and Sales pricing area was removed.
7. The total area is visually stronger with larger typography and a dedicated cashier summary block.
8. Basket numeric fields were visually compacted so product rows and totals dominate more of the screen.
9. The quick-add customer overlay now spans the full cashier surface and centers correctly.
10. POS screen/nav/title naming now shows `الكاشير`.

---

## 4) Exact Paid-Amount / Change Behavior

### A) Anonymous cashier sale

1. The paid-amount field is shown as operational cash UX.
2. If the field is blank, checkout behaves like the prior anonymous flow: sale is created and posted, and no backend payment record is created.
3. If the entered amount equals the invoice total, checkout succeeds normally.
4. If the entered amount exceeds the invoice total, checkout succeeds and the success message shows the change due.
5. If the entered amount is below the invoice total, checkout is blocked.

Why blocked:
Because anonymous sale flow still has no backend receivable/payment persistence path, allowing an explicit underpaid anonymous checkout would create an untracked outstanding amount.

### B) Named customer sale with `Cash`

1. The paid-amount field is shown.
2. If the field is blank, POS persists a payment equal to the invoice total, matching the previous exact-paid-now behavior.
3. If the entered amount is below the invoice total, POS now persists a truthful partial customer payment equal to the entered amount.
4. The remaining amount stays outstanding on the customer account by existing backend accounting behavior.
5. If the entered amount equals the invoice total, POS persists the full payment normally.
6. If the entered amount exceeds the invoice total, POS persists only the invoice total and shows the change due in cashier UX.

### C) Named customer sale with non-cash method

1. The paid-amount field is hidden.
2. POS keeps exact-total payment persistence behavior.
3. No change-due UX is invented for non-cash methods.

---

## 5) Truthfulness Rule: Backend Payment Persistence vs Cashier-Side Change Display

This phase keeps backend persistence honest.

1. Anonymous sale still creates no backend payment record.
2. Named-customer payment persistence uses `POST /api/v1/payments` only with supported amount rules.
3. Underpaid named cash sale persists the actual entered amount only.
4. Overpaid cash sale never sends the overage to the backend.
5. When cash tender exceeds invoice total, the backend only receives the invoice total and the extra amount is treated as cashier-side operational change display.

This avoids fake accounting persistence for change while still giving the cashier the operational answer needed at the counter.

---

## 6) Exact Dark-Mode Readability Fixes

Applied as a safe shared input-style hardening, not a broad theme rewrite.

1. Added dedicated input background/border/selection theme brushes in Light and Dark themes.
2. Updated shared `ThemedTextBoxStyle`, `ThemedComboBoxStyle`, and `ThemedDatePickerStyle` to use the stronger input brushes.
3. Added `DatePickerTextBox` and `ComboBoxItem` styling so typed text and selected values no longer depend on unreadable default system colors in dark mode.
4. Added compact text-box styles for lighter secondary fields and cashier row inputs.
5. Explicitly set popup/search-result text to `PrimaryTextBrush` in touched Sales/POS popups so dark-mode popups no longer rely on default black text.
6. Applied the hardened date picker style to the touched Sales Admin invoice-date field.

---

## 7) Exact NotificationBar Hygiene Changes

1. `ApiClient` no longer returns status/title/traceId/raw JSON excerpts to end-user UI strings.
2. `ProblemDetails` no longer appends raw `INTERNAL_ERROR` detail into user-facing text.
3. Unmapped/technical server fragments now fall back to safe generic UI messages instead of leaking raw code/detail strings.
4. POS and Sales page-entry code now clears cached NotificationBar state on navigation re-entry because shell navigation caches ViewModels.
5. Cashier success/warning messages are now explicit for:
   1. anonymous sale posted
   2. anonymous sale posted with change due
   3. named sale + full payment saved
   4. named sale + partial payment saved
   5. named sale + change due
   6. sale posted but payment failed

---

## 8) Exact Modal Positioning Fix

Root cause:
The POS page root grid has multiple rows, but the quick-add customer overlay grid was not spanning them. In WPF that meant the modal overlay was effectively constrained to the first row, which visually pushed it high/off-center.

Fix applied:

1. POS quick-add overlay grid now uses full-row span.
2. Overlay z-index was made explicit.
3. Busy overlay was given the same full-surface span treatment for consistency.

---

## 9) Exact Naming Cleanup to `الكاشير`

User-facing `Nav_SalesPos` was changed from `نقطة البيع (POS)` to `الكاشير`.

Route keys and internal navigation token `SalesPos` were intentionally left unchanged to avoid destabilizing navigation wiring.

---

## 10) Micro-Audit: Manager-Only Price Edit / Discount Policy Readiness

### 10A. Can unit price editing be safely gated by an existing manager/admin permission without backend change?

Verdict: **No, not safely enough for rollout.**

Grounded source result:

1. Existing sales permissions are coarse: `SALES_READ`, `SALES_WRITE`, `SALES_POST`.
2. There is no existing explicit permission such as `SALES_PRICE_OVERRIDE` or manager-only sales pricing permission.
3. Desktop sales/POS price editing currently depends only on the existing sales write flow and editable line models.
4. The backend Sales contract accepts `unitPrice` directly on create/update and does not enforce a manager-only override path.

Conclusion:
An explicit manager-only price gate cannot be delivered cleanly from existing policy source alone. A role-name-only UI check against something like seeded `Admin` would be brittle and not grounded enough for final policy rollout.

### 10B. Is there existing support for policy-driven discount control from reason-code infrastructure?

Verdict: **No direct sales support exists today.**

Grounded source result:

1. `ReasonCode` has `RequiresManagerApproval`.
2. That approval infrastructure is wired for dispositions and return flows.
3. Current Sales contract line DTOs include only:
   1. `variantId`
   2. `quantity`
   3. `unitPrice`
   4. `discountAmount`
4. Sales create/update endpoints do not accept a discount reason code, approval token, or policy identifier.

Conclusion:
Current reason-code infrastructure is not enough to govern Sales discounts without a separate contract/policy phase.

### 10C. Exact minimal next phase needed

Minimal grounded next phase:

1. Add an explicit sales price-override permission such as `SALES_PRICE_OVERRIDE` and gate Sales/POS unit-price editing on it in Desktop.
2. If auditability is required, add backend-side validation and optional override audit fields so unauthorized clients cannot bypass the Desktop UI.
3. Add explicit sales discount-governance contract support, for example a sales discount reason/policy field or a dedicated discount-approval model, then validate it server-side on Sales create/update/post.

This was intentionally deferred from R1.

---

## 11) Explicit Deferrals

Intentionally deferred in this phase:

1. whole-app anti-flicker rollout
2. whole-app numeric-input rollout
3. Print Config
4. Sales Returns UI
5. Customer Payments standalone page
6. full price/discount governance phase

---

## 12) Build / Test / Run Result

### Build

Command:

`dotnet build .\ElshazlyStore.sln -c Debug`

Result:

1. Build succeeded
2. 0 warnings
3. 0 errors

### Full test suite

Command:

`dotnet test .\tests\ElshazlyStore.Tests\ElshazlyStore.Tests.csproj -c Debug --no-build`

Result:

1. Passed: 251
2. Failed: 1
3. Total: 252

Unrelated failing test observed:

1. `InventoryConsistencyTests.ConcurrentPurchasePost_BalanceUpdatedExactlyOnce`
2. Failure text: expected `200` or `409`, got `500`

This failure is outside the cashier/POS/Desktop scope of this R1.

### Relevant regression subset

Command:

`dotnet test .\tests\ElshazlyStore.Tests\ElshazlyStore.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~SalesInvoiceTests|FullyQualifiedName~AccountingPaymentTests"`

Result:

1. Passed: 32
2. Failed: 0

### Desktop startup smoke test

Result:

1. Built Desktop executable launched successfully.
2. Process stayed alive for a short smoke-check interval.
3. Process was then terminated intentionally to avoid leaving a stray GUI process running.

---

## 13) Concrete Human Test Script

### Preconditions

1. Run API.
2. Run Desktop app.
3. Use a user with Sales permissions and, for named payment tests, `PAYMENTS_WRITE`.
4. Ensure at least one active warehouse and at least one barcode-resolvable variant exist.
5. Ensure at least one customer exists, or use quick-add.

### Test A: Open `الكاشير`

1. Open the sidebar entry now labeled `الكاشير`.
2. Confirm the page title and nav label both show `الكاشير`.
3. Confirm the total card is visually stronger than the secondary fields.

### Test B: Anonymous sale, exact amount

1. Keep sale anonymous.
2. Scan one or more products.
3. Enter an exact paid amount equal to the total.
4. Press `Enter` from the paid-amount field or click the primary checkout button.
5. Confirm:
   1. sale posts successfully
   2. success banner is clear
   3. no server payment record is claimed in the message

### Test C: Anonymous sale, change due

1. Keep sale anonymous.
2. Scan products totaling a known amount.
3. Enter a higher paid amount.
4. Complete checkout.
5. Confirm the success message explicitly shows `الباقي للعميل`.

### Test D: Anonymous sale, underpaid blocked

1. Keep sale anonymous.
2. Scan products.
3. Enter a paid amount lower than the total.
4. Attempt checkout.
5. Confirm checkout is blocked with an honest message explaining that anonymous underpayment cannot be tracked in the backend path.

### Test E: Named customer, cash partial payment

1. Select a named customer.
2. Keep payment method = `Cash`.
3. Scan products.
4. Enter a paid amount lower than the total.
5. Complete checkout.
6. Confirm:
   1. sale posts successfully
   2. payment is saved successfully
   3. success message shows the exact remaining amount
   4. customer balance/outstanding reflects the remaining amount

### Test F: Named customer, cash overpay

1. Select a named customer.
2. Keep payment method = `Cash`.
3. Enter a paid amount higher than the total.
4. Complete checkout.
5. Confirm:
   1. sale posts successfully
   2. payment persistence uses invoice total only
   3. success message shows the change due
   4. no overpayment backend error occurs

### Test G: Named customer, non-cash exact payment

1. Select a named customer.
2. Change payment method to `Visa`, `InstaPay`, or `EWallet`.
3. Confirm paid-amount field is hidden.
4. Complete checkout.
5. Confirm the full payment is saved and success message remains clear.

### Test H: Split outcome honesty

1. Force a named-customer payment failure if possible (for example invalid wallet requirement path).
2. Confirm the sale-posted/payment-failed message is understandable and does not show raw status code, traceId, or server fragment noise.

### Test I: NotificationBar hygiene on navigation

1. Trigger a success or error on `الكاشير`.
2. Navigate to another screen.
3. Return to `الكاشير`.
4. Confirm the old banner is gone.
5. Repeat on the Sales Admin screen.

### Test J: Quick-add modal centering

1. On `الكاشير`, open `إضافة عميل سريعاً`.
2. Confirm the overlay appears centered over the cashier screen, not awkwardly high.
3. In dark mode, confirm modal typed text and duplicate suggestion rows are readable.

### Test K: Dark mode readability

1. Enable dark mode.
2. Test typed input readability on:
   1. cashier barcode input
   2. cashier tendered amount input
   3. cashier payment reference / notes
   4. Sales Admin invoice date field
   5. touched Sales line numeric fields
   6. touched customer/variant popup rows
3. Confirm text, caret, selection, and selected values remain readable.

---

## 14) Stop Condition

This phase stops here.

Await:

1. agent report review
2. human cashier retest result
