# UI CUSTOMER PAYMENTS 2-R2 — TOP SEARCH CUSTOMER FLOW + SINGLE SEARCH SOURCE + RECEIPT ID/WORDS — CLOSEOUT

**Phase:** CP-2-R2
**Date:** 2026-04-01
**Status:** ✅ Implemented — Awaiting Human Test

---

## 1. Root Cause of the Duplicate Customer-List Problem

**Root cause:** `CustomerPaymentsViewModel` had a single shared set of customer-search observables (`CustomerSearchText`, `CustomerSearchResults`, `HasCustomerSearchResults`, `SelectCustomerCommand`) that were used by **both**:
1. The context-bar inline search (state 2: `IsSelectingCustomer = true`)
2. The Create Payment modal's customer typeahead TextBox

Because both UI regions bound to the **same** properties:
- Any text typed in the modal would trigger `OnCustomerSearchTextChanged` → debounce → results → `HasCustomerSearchResults = true`
- The context-bar popup (`IsOpen="{Binding HasCustomerSearchResults}"`) was also visible whenever the modal popup was visible (if the context-bar was in state 2)
- Additionally, if the context-bar search was open while the modal was opened, **two visible dropdown lists** appeared simultaneously

**Secondary issue:** The customer search was not present in the toolbar. The user had to click the "تغيير العميل" button in the context bar to open an inline search field. This added an unnecessary interaction step and was the "tied to last customer" UX complaint.

---

## 2. Customer-Selection UX Approach — What Was Implemented and Why

### Chosen approach: Toolbar-first customer typeahead with modal separation

#### A — Toolbar becomes the primary customer search/select entry point

The `SearchText` TextBox (used for payment list text filtering) in the toolbar was **replaced** with a customer-name typeahead TextBox bound to `CustomerSearchText`. This box is **always visible** in the toolbar row, next to [تسجيل دفعة جديدة] and [تحديث].

- In global mode: the box is empty; user types 2+ chars → dropdown appears → select customer → page filters to that customer, box shows their name
- In selected-customer mode: the box shows the current customer's name; user can type to search and switch, OR select all text and delete to return to global mode (empty text clears the filter)
- No "Change Customer" button or separate activate-step required

**Why:** This is the direct path the human test feedback requested. The toolbar search is always there, always the same element, and its state directly reflects who is selected (shows the name when selected, empty when in global mode).

The payment text search (`SearchText` from base class) was removed from the toolbar — the customer-level filter already narrows scope sufficiently and the primary use case is selecting a customer, not full-text payment search.

#### B — Context bar simplified to 2 states

The context bar (Row 3) was simplified from 3 states to 2:

| State | Condition | UI |
|-------|-----------|-----|
| **Customer selected** | `HasPresetCustomer = true` | Customer name (17px Bold) + المتبقي + [إلغاء التصفية] |
| **Global / all customers** | `HasPresetCustomer = false` | "جميع العملاء" label (SemiBold, secondary) |

The "State 2: IsSelectingCustomer" search-in-context-bar mode was **removed**. Search always happens in the toolbar. The context bar is now purely an **identity/information display**, not an interaction trigger.

The `IsSelectingCustomer` observable, `StartChangeCustomerCommand`, and `CancelCustomerSearchCommand` were all removed — they are no longer needed.

#### C — Empty text in toolbar → automatic global mode restore

`OnCustomerSearchTextChanged` now checks: if value becomes empty AND `HasPresetCustomer` is true, it performs an inline clear-filter operation (sets `HasPresetCustomer = false`, clears `_presetCustomerId`, reloads). This means clearing the toolbar field is the natural way to "deselect" a customer without any button.

---

## 3. Behavior: Selected-Customer Mode

- Toolbar shows the selected customer's name in the search box
- Context bar shows the customer name (17px Bold) + المتبقي (if `ACCOUNTING_READ` granted) + [إلغاء التصفية]
- Payment list is filtered to that customer
- Clicking [إلغاء التصفية] → global mode, toolbar clears, context bar shows "جميع العملاء"
- Typing in the toolbar and selecting a new customer → switches filter directly, no button click required

---

## 4. Behavior: Global / All-Customers Mode

- Toolbar search box is empty
- Context bar shows "جميع العملاء" label
- User types 2+ chars in toolbar → dropdown shows matching customers → click to select
- After selection, page immediately filters to that customer (same logic as SelectCustomerCommand)

---

## 5. Modal Customer Field — Readonly When Page Context Already Selected a Customer

**Explicit statement:** ✅ When `HasPresetCustomer = true`, the Create Payment modal shows the customer name as a **readonly styled TextBlock** (not an editable TextBox). No typeahead is active inside the modal. There is no dropdown in the modal. This is unconditional.

**Implementation:**
- VM: added `IsModalCustomerReadonly` computed property (`=> HasPresetCustomer`)
- XAML modal: two-element conditional:
  - `IsModalCustomerReadonly = true` → `Border + TextBlock` showing `PresetCustomerName` (styled as a read-only field; same height/border as form inputs for visual consistency)
  - `IsModalCustomerReadonly = false` → `Grid` with `ModalCustomerSearchText` TextBox + `ModalCustomerSearchResults` Popup

**When page is in global mode (no preset customer):**
- The modal shows an editable modal-specific typeahead bound to `ModalCustomerSearchText` and `ModalCustomerSearchResults`
- These are **separated from** the toolbar's `CustomerSearchText` and `CustomerSearchResults`
- They use their own debounce method (`DebounceSearchModalCustomersAsync`) and their own `SelectModalCustomerCommand`
- No overlap with the toolbar popup is possible

---

## 6. Outstanding / "المتبقي" Presentation Result

### Label change
- **Before:** "الرصيد المستحق"
- **After:** "المتبقي"
- Changed in `Strings.resx` (`CustomerPayments_OutstandingLabel` key)

### Placement (unchanged from CP-2-R1)
- Shown below the customer name in the context bar when:
  - `HasPresetCustomer = true`
  - `CanViewAccounting = true` (user has `ACCOUNTING_READ` permission)
  - `CustomerOutstanding` has a value
  - `IsLoadingOutstanding = false`
- Displayed as: **`المتبقي: 1,500.00 جنيه`** (FontSize=13, FontWeight=SemiBold, AccentBrush)
- Loading indicator "جاري تحميل الرصيد…" shown while fetching
- Backend truth: `GET /api/v1/accounting/balances/Customer/{id}` — unchanged from CP-2-R1

---

## 7. Receipt Number / Payment Number — Traceability Result

### Source
The `PaymentNumber` field in `PaymentDto` is assigned server-side by the backend at creation time. It is the real, unique, persisted payment document number (e.g. `PMT-00042`). This was already being displayed on the receipt from CP-2.

### What CP-2-R2 added
- `PaymentNumber` continues to be shown at the top of the receipt in the "رقم الإيصال:" field pair
- **No invented IDs** — the receipt number is always the real `PaymentNumber` from the backend DTO

### Honest limitation documented
If `payment.PaymentNumber` is null (e.g., legacy payments created before the number was assigned server-side), the receipt displays "—" (dash). This is the honest fallback already in place. No fake sequential IDs were introduced.

---

## 8. Amount in Words — Result

### ✅ IMPLEMENTED

Added `ReceiptPrintService.AddAmountInWords(doc, payment.Amount)` to `PrintCustomerPaymentReceipt` in `DocumentPrintHelper.cs`, placed immediately after the amount/method field pair.

Uses the existing `ArabicAmountInWords.Convert(decimal)` helper (tested in `ArabicAmountInWordsTests.cs` — 274 tests passed including this helper's 15 tests).

**Receipt line:** `المبلغ بالحروف: ستة آلاف وخمسمئة وعشرون جنيهًا فقط لا غير`

---

## 9. Treasury-Signature Username — Result

### ✅ IMPLEMENTED

**Before:** Username was printed as a plain `"بواسطة: username"` field above the signature block (a flat text line).

**After:**
- The `"بواسطة:"` plain text field was **removed** from `PrintCustomerPaymentReceipt`
- `ReceiptPrintService.AddSignatureBlock` was extended with an optional `treasuryPerson` parameter
- `PrintCustomerPaymentReceipt` passes `payment.CreatedByUsername` as `treasuryPerson`
- The cashier's name now appears **inside the treasury-signature cell**, below the "___________________" signature line, centered under "توقيع الخزينة"
- If `CreatedByUsername` is null/empty, no name row is added (behavior is unchanged from before)

This places the operator identity exactly in the treasury-signature area for formal traceability, not as a loose text field above.

---

## 10. Explicit Scope Statements

### Prepayment / "له / عليه" — NOT IMPLEMENTED
No advance payment, credit balance, "له / عليه" accounting toggle, or overpayment bypass was implemented. `allowOverpay = false` backend guard remains intact.

### Batch / A4 multi-payment statement — NOT IMPLEMENTED
`PrintCustomerPaymentReceipt` remains a single dual-copy (أصل + صورة) receipt per payment. No batch print, no multi-row summary report.

### Supplier page parity — NOT TOUCHED
`SupplierPaymentsPage.xaml` and `SupplierPaymentsViewModel.cs` were not modified. The `ReceiptPrintService.AddSignatureBlock` change is additive (new optional parameter with a default of `null`) — all existing callers compile unchanged and behave identically.

---

## 11. Files Changed

### Modified
| File | Changes |
|------|---------|
| `src/ElshazlyStore.Desktop/ViewModels/CustomerPaymentsViewModel.cs` | **Removed:** `IsSelectingCustomer`, `IsShowingCustomerContext`, `IsShowingAllContext`, `StartChangeCustomerCommand`, `CancelCustomerSearchCommand`, `OnIsSelectingCustomerChanged`. **Added:** `_suppressCustomerSearch`, `_suppressModalSearch`, `ModalCustomerSearchText`, `HasModalCustomerSearchResults`, `ModalCustomerSearchResults`, `IsModalCustomerReadonly`, `SelectModalCustomerCommand`, `DebounceSearchModalCustomersAsync`, `OnModalCustomerSearchTextChanged`. **Modified:** `OnCustomerSearchTextChanged` (suppress guard + empty-text → global mode); `SelectCustomer` (suppress wrap); `InitializeAsync` (suppress wrap); `OpenCreate` (reset modal state, remove IsSelectingCustomer); `ClearCustomerFilter` (suppress wrap, remove IsSelectingCustomer); `SetCustomerFilter` (remove IsSelectingCustomer); `OnHasPresetCustomerChanged` (notify IsModalCustomerReadonly instead of IsShowingCustomerContext/IsShowingAllContext) |
| `src/ElshazlyStore.Desktop/Views/Pages/CustomerPaymentsPage.xaml` | **Toolbar:** replaced `SearchText` TextBox with customer typeahead `CustomerSearchText` Grid+Popup. **Context bar:** reduced from 3-state to 2-state (removed State 2 inline search, removed "تغيير العميل" button from State 1). **Modal:** replaced single shared customer TextBox+Popup with two-state conditional (readonly TextBlock when `IsModalCustomerReadonly=true`; modal-specific typeahead with `ModalCustomerSearchText`/`ModalCustomerSearchResults`/`SelectModalCustomerCommand` when false) |
| `src/ElshazlyStore.Desktop/Localization/Strings.resx` | `CustomerPayments_OutstandingLabel`: "الرصيد المستحق" → "المتبقي" |
| `src/ElshazlyStore.Desktop/Helpers/DocumentPrintHelper.cs` | `PrintCustomerPaymentReceipt`: added `AddAmountInWords`; moved username from plain "بواسطة:" field to `treasuryPerson` parameter of `AddSignatureBlock` |
| `src/ElshazlyStore.Desktop/Services/Printing/ReceiptPrintService.cs` | `AddSignatureBlock`: added optional `treasuryPerson` parameter; when provided, a new row with the name is rendered centered under the treasury signature line |

### Not Modified
- `SupplierPaymentsPage.xaml` / `SupplierPaymentsViewModel.cs` — unchanged
- `CustomersPage.xaml` / `CustomersViewModel.cs` — unchanged
- `NavigationService.cs` / `INavigationService.cs` — unchanged
- `DocumentPrintHelper.cs` `PrintPaymentReceipt` (supplier) — unchanged
- `Strings.cs` — no new accessors needed (existing `CustomerPayments_OutstandingLabel` accessor reused)
- `LightTheme.xaml` / `DarkTheme.xaml` — unchanged; all new UI uses `DynamicResource` brushes

---

## 12. Build / Test / Run Result

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Passed!  - Failed: 0, Passed: 274, Skipped: 0, Total: 274
```

---

## 13. Cleanup Audit

### Touched files reviewed
All 5 modified files were audited:

| Removal | Why safe |
|---------|----------|
| `IsSelectingCustomer` observable + `OnIsSelectingCustomerChanged` partial | Context-bar inline search is replaced by toolbar typeahead; no other code references this |
| `IsShowingCustomerContext` / `IsShowingAllContext` computed props | Replaced in XAML by direct `HasPresetCustomer` and `InverseBoolToVisConv` bindings — simpler and fewer intermediary booleans |
| `StartChangeCustomerCommand` | No longer needed: there is no "activate search" button; toolbar is always-on |
| `CancelCustomerSearchCommand` | No longer needed: no inline context-bar search mode to cancel |
| `IsSelectingCustomer = false;` in `SetCustomerFilter` and `OpenCreate` | Property removed |
| Old modal `CustomerSearchText` + `CustomerSearchResults` + `SelectCustomerCommand` bindings in modal | Replaced by modal-specific equivalent (`ModalCustomerSearchText` etc.) |
| `"بواسطة:"` plain text field from `PrintCustomerPaymentReceipt` | Username is now in the signature block itself; plain duplicate removed |

### Strings kept but no longer referenced from XAML
- `CustomerPayments_ChangeCustomer` — still present in `.resx` and `Strings.cs` from CP-2-R1; harmless dead string, not removed to avoid risk of breakage if referenced elsewhere
- `CustomerPayments_SelectCustomer` — same

These can be removed in a future cleanup pass if confirmed unused globally. Not touched here to keep this a surgical phase.

### What was intentionally left
- `CustomerPayments_AllSubtitle` / `CustomerPayments_ContextSubtitle` — present from CP-2; not used in R2 XAML but harmless
- Payment text search (`SearchText` from base class) — still defined in `PagedListViewModelBase`; it is simply no longer bound in the toolbar XAML for this page. The URL-building logic in `FetchPageAsync` still has a `q=...` guard; that code path may now only be reached if some other code path sets `SearchText`. This is safe: if `SearchText` is empty (which it always is now), the guard is a no-op.

### Build/test verification after cleanup
```
Build succeeded.  0 Warning(s)  0 Error(s)
Tests: Passed 274 / Failed 0 / Skipped 0
```

---

## 14. Human Test Script

### Setup
1. User has `PAYMENTS_WRITE` + `PAYMENTS_READ` + `ACCOUNTING_READ`.
2. Customer A: has posted invoices (outstanding balance > 0).
3. Customer B: different name, at least 2 chars unique prefix.
4. Application running with real backend.

---

### Test 1 — Global mode toolbar search + select customer
1. Open Customer Payments from sidebar (direct navigation, no preset customer).
2. **Verify toolbar:** search box is empty. Context bar shows "جميع العملاء".
3. Type first 2+ letters of Customer A name in the toolbar search box.
4. **Verify:** dropdown appears beneath the toolbar search box with matching customers.
5. Click Customer A.
6. **Verify:** toolbar search box now shows Customer A's name. Context bar shows Customer A's name in bold (17px). المتبقي appears below name (after brief loading). Payment list filtered to Customer A.

---

### Test 2 — Switch customer directly from toolbar (no button click needed)
1. (Continuing from Test 1, filtered to Customer A.)
2. Click into the toolbar search box and replace the text with first 2+ letters of Customer B.
3. **Verify:** dropdown appears with Customer B (and others if matching).
4. Click Customer B.
5. **Verify:** toolbar shows Customer B's name. Context bar shows Customer B bold. Outstanding updates. Payment list reloads for Customer B.
6. **Verify: NO duplicate dropdown appeared anywhere in the page during this flow.**

---

### Test 3 — Clear filter by emptying toolbar search
1. (Continuing from Test 2, filtered to Customer B.)
2. Clear the text in the toolbar search box (select all, delete).
3. **Verify:** toolbar is empty. Context bar shows "جميع العملاء". Payment list reloads showing all customers.

---

### Test 4 — Create payment from selected-customer mode (readonly modal customer)
1. Filter to Customer A (via toolbar search or via Customers page → عرض الدفعات).
2. Click [تسجيل دفعة جديدة].
3. **Verify:** modal opens. Customer field shows Customer A's name as a **non-editable styled readonly block** (no text input cursor, no typeahead popup visible).
4. **Verify: NO dropdown or search results appear.** Only the name is shown.
5. Enter amount, select method, click Save.
6. **Verify:** payment saved, list reloads, outstanding refreshes.

---

### Test 5 — Create payment from global mode (editable modal customer)
1. Clear filter (toolbar empty, context bar shows "جميع العملاء").
2. Click [تسجيل دفعة جديدة].
3. **Verify:** modal opens. Customer field is an **editable TextBox**.
4. Type 2+ letters of a customer name.
5. **Verify:** dropdown appears ONLY inside the modal — no second dropdown appeared in the toolbar area.
6. Select the customer from the modal dropdown.
7. **Verify:** modal dropdown closes. Customer name shown in the modal field.
8. Enter amount, click Save.
9. **Verify:** payment saved. Page context updates to the selected customer (toolbar shows their name, context bar shows identity + outstanding).

---

### Test 6 — Receipt traceability
1. In any mode, after at least one payment exists for Customer A.
2. Click [طباعة] next to a payment row.
3. **Verify receipt content:**
   - رقم الإيصال: `PMT-XXXXX` (real backend payment number)
   - العميل: Customer A name
   - المبلغ: numeric amount
   - المبلغ بالحروف: Arabic words for the amount (e.g. "خمسمئة جنيهًا فقط لا غير")
   - توقيع الخزينة area: shows the cashier's username **beneath the signature line**
4. **Verify:** both أصل and صورة copies print with the same traceability fields.

---

### Test 7 — Outstanding label
1. Filter to Customer A with a known outstanding balance.
2. **Verify:** context bar shows **"المتبقي: X,XXX.XX جنيه"** (NOT "الرصيد المستحق").

---

### Test 8 — ACCOUNTING_READ absent
1. Log in as a user with `PAYMENTS_WRITE` but without `ACCOUNTING_READ`.
2. Filter to any customer.
3. **Verify:** no outstanding amount or loading indicator appears in the context bar (graceful hide).
4. Overpayment error shows plain text without the outstanding hint.
