# UI CUSTOMER PAYMENTS 2-R1 — CUSTOMER CONTEXT SEARCH + OUTSTANDING VISIBILITY + HEADER CLARITY — CLOSEOUT

**Phase:** CP-2-R1
**Date:** 2026-04-01
**Status:** ✅ Implemented — Awaiting Human Test

---

## 1. Root Causes Addressed

### Problem 1 — Customer Context Usability
**Root cause:** `CustomerPaymentsPage` had no way to search or switch customers from within the page itself. The only path to change the customer filter was to navigate back to `CustomersPage`, click the row's "عرض الدفعات" button, and return. The customer identity was displayed only as a small (FontSize=12, `SecondaryTextBrush`) subtitle under the page title — visually negligible.

**Consequence:** Each time the operator needed to record payments for a different customer, they had to leave and reenter the page from the outside. This was the "too tied to Customers page" complaint in the human test notes.

### Problem 2 — Outstanding Visibility
**Root cause:** When `POST /api/v1/payments` returned HTTP 422 `OVERPAYMENT_NOT_ALLOWED`, the error was displayed as "المبلغ يتجاوز الرصيد المستحق" — a true error message, but without any context value. The user had no way to see *what* the outstanding balance was to know how much they could safely enter.

**Context:** A real backend endpoint exists — `GET /api/v1/accounting/balances/Customer/{id}` (requires `ACCOUNTING_READ` permission) — and was not being called from CustomerPaymentsPage at all.

### Problem 3 — Header / Customer Identity Clarity
**Root cause:** Customer name was in the 12px subtitle of the page header. No prominent visual identity for the current customer existed anywhere on the page.

---

## 2. Customer Switch / Search Approach — What Was Implemented and Why

### Chosen approach: Three-state Customer Context Bar

A dedicated **Customer Context Bar** was added to `Grid.Row="3"` of `CustomerPaymentsPage` — positioned between the toolbar and the DataGrid, functioning as a "current filter" summary row. This bar has three mutually exclusive visual states driven by two observable booleans (`HasPresetCustomer`, `IsSelectingCustomer`):

| State | Condition | What the user sees |
|-------|-----------|-------------------|
| **Customer set** | `HasPresetCustomer=true`, `IsSelectingCustomer=false` | Customer name (bold, FontSize=17) + outstanding + [تغيير العميل] [إلغاء التصفية] buttons |
| **Selecting** | `IsSelectingCustomer=true` | Inline typeahead search field with debounced dropdown + [إلغاء] cancel button |
| **All customers** | `HasPresetCustomer=false`, `IsSelectingCustomer=false` | "جميع العملاء" label (SemiBold) + [اختر عميلاً] button |

### Why this approach:
- **No new typeahead infrastructure needed.** The existing `CustomerSearchText`, `CustomerSearchResults`, `SelectCustomerCommand`, and debounce logic were already present in CP-2. The context bar simply exposes the same machinery in the page body (outside the create modal).
- **No new API calls for search.** Customer typeahead reuses `GET /api/v1/customers?q=...` which was already wired.
- **No NavigationService involvement.** The customer can switch without leaving and re-entering the page — `SelectCustomerCommand` already handles the filter change and list reload.
- **Non-destructive.** All existing navigation from `CustomersPage` → `CustomerPaymentsPage` (via `SetCustomerFilter`) continues to work exactly as before. The context bar just lets the user also do it from within the page.

### Commands added:
- `StartChangeCustomerCommand` — sets `IsSelectingCustomer = true`; clears search field
- `ClearCustomerFilterCommand` — resets all filter state; reloads all customer payments
- `CancelCustomerSearchCommand` — exits selecting state without changing current filter; restores search text to current customer name (if any)

### SelectCustomerCommand modifications:
- Now also sets `IsSelectingCustomer = false` after selection (collapses the inline search)
- Now triggers `FetchOutstandingAsync` after switching customers
- `OpenCreate()` also sets `IsSelectingCustomer = false` (prevents modal + context search from being open simultaneously)

---

## 3. Outstanding Visibility Result — ✅ SUPPORTED AND IMPLEMENTED

### Backend truth confirmed
The endpoint `GET /api/v1/accounting/balances/{partyType}/{partyId}` **exists and is real**. It is gated by `ACCOUNTING_READ` permission and returns:
```json
{ "partyId": "...", "partyType": "Customer", "outstanding": 1500.00 }
```
The `outstanding` field is computed by `AccountingService.ComputeOutstandingAsync` — it is derived from ledger entries (invoices debited, payments and credit notes credited) and is the server's single source of truth.

### What was implemented
1. **New DTO:** `PartyOutstandingResponse` in `Models/Dtos/` to deserialize the balance endpoint response.
2. **New VM property:** `CustomerOutstanding` (`decimal?`) — null when not applicable or not yet loaded.
3. **`FetchOutstandingAsync(Guid customerId)`** private method:
   - Called on page init when `HasPresetCustomer && CanViewAccounting`
   - Called after each successful `SelectCustomerCommand` switch
   - Called after each successful `SaveAsync` (to reflect the new lower balance)
   - Gracefully handles 403 (no ACCOUNTING_READ) by leaving `CustomerOutstanding = null` — the UI hidden
4. **Context bar display:** When `HasOutstandingDisplay` is true (customer set + `CanViewAccounting` + outstanding loaded), the context bar shows under the customer name:
   - Label "الرصيد المستحق" + ": " + bold `AccentBrush` amount (e.g., "1,500.00 جنيه")
5. **Enhanced error message:** When `POST /api/v1/payments` returns HTTP 422 and `CustomerOutstanding.HasValue`, the error message becomes: `"المبلغ يتجاوز الرصيد المستحق (1,500.00 جنيه)"` — telling the user exactly how much they can enter.
6. **Loading indicator:** `IsLoadingOutstanding` shows "جاري تحميل الرصيد…" below the customer name while the API call is in flight.

### Permission boundary
- Requires `ACCOUNTING_READ` permission (separate from `PAYMENTS_READ`/`PAYMENTS_WRITE`)
- If user has `PAYMENTS_WRITE` but not `ACCOUNTING_READ`, outstanding is not shown and the enhanced error message falls back to the plain "المبلغ يتجاوز الرصيد المستحق" — honest and correct
- No fake zeros or invented numbers are ever shown

---

## 4. Header / Customer Identity Clarity

### Before (CP-2)
- Customer name: 12px subtitle, `SecondaryTextBrush` — visually negligible
- No way to see clearly without reading a tiny subtitle

### After (CP-2-R1)
- **Page header simplified** to just the page title "مدفوعات العملاء" (22px Bold) and the back button — clean and unambiguous
- **Customer context bar** (full-width, `CardBackgroundBrush` background, 1px top/bottom border) has the customer name at **FontSize=17, FontWeight=Bold, `PrimaryTextBrush`** — clearly readable in both light and dark mode
- Outstanding displayed below the name as **FontSize=13, FontWeight=SemiBold, `AccentBrush`** — visually distinct and easy to glance

---

## 5. Explicit Scope Statements

### Prepayment / "له / عليه" logic — NOT IMPLEMENTED
No advance payment, credit balance, or "له / عليه" accounting UI was implemented.  
The backend `allowOverpay = false` guard is left intact.  
Future phase if backend adds support for prepayment or credit balance accounting.

### Batch / A4 multi-payment statement printing — NOT IMPLEMENTED
The existing single-receipt printing (`PrintCustomerPaymentReceipt`) was not touched.  
Multi-payment statement / A4 report is deferred — requires design + backend summary endpoint.

### Supplier payment context parity — NOT IMPLEMENTED
`SupplierPaymentsPage` was not modified. No cross-page refactor in this phase.

---

## 6. Files Changed

### Created
| File | Purpose |
|------|---------|
| `src/ElshazlyStore.Desktop/Models/Dtos/PartyOutstandingResponse.cs` | DTO for `GET /api/v1/accounting/balances/{partyType}/{partyId}` response |
| `docs/UI CUSTOMER PAYMENTS 2-R1 — CUSTOMER CONTEXT SEARCH + OUTSTANDING VISIBILITY + HEADER CLARITY — CLOSEOUT.md` | This file |

### Modified
| File | Changes |
|------|---------|
| `src/ElshazlyStore.Desktop/ViewModels/CustomerPaymentsViewModel.cs` | Added: `CanViewAccounting`, `IsSelectingCustomer`, `CustomerOutstanding`, `IsLoadingOutstanding`; computed `IsShowingCustomerContext`, `IsShowingAllContext`, `HasOutstandingDisplay`, `OutstandingAmountDisplay`; commands `StartChangeCustomer`, `ClearCustomerFilter`, `CancelCustomerSearch`; `FetchOutstandingAsync`; modified `SelectCustomer`, `OpenCreate`, `InitializeAsync`, `SaveAsync`, `SetCustomerFilter` |
| `src/ElshazlyStore.Desktop/Views/Pages/CustomerPaymentsPage.xaml` | Replaced two-line header with clean single-line header; replaced unused Row 3 content gap with three-state Customer Context Bar |
| `src/ElshazlyStore.Desktop/Localization/Strings.cs` | Added 7 CP-2-R1 string accessors |
| `src/ElshazlyStore.Desktop/Localization/Strings.resx` | Added 7 CP-2-R1 Arabic string values |

### Not Modified
- `CustomersPage.xaml` — no change; CP-1/CP-2 navigation flow unchanged
- `CustomersViewModel.cs` — no change; `OpenCustomerPaymentsCommand` unchanged
- `NavigationService.cs` / `INavigationService.cs` — no change
- `MainWindow.xaml` — no change; sidebar entry unchanged
- `DocumentPrintHelper.cs` — no change; receipt printing untouched
- `PaymentDto.cs` — no change
- `CreatePaymentRequest.cs` — no change
- `LightTheme.xaml` / `DarkTheme.xaml` — no change; all new UI uses `DynamicResource` brushes

---

## 7. Build / Test / Run Result

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## 8. Human Test Script

### Setup
1. Ensure a test customer exists with at least one posted sales invoice (to generate outstanding balance).
2. Ensure a second test customer exists (with or without outstanding).
3. Ensure test user has `PAYMENTS_WRITE` + `PAYMENTS_READ` + `ACCOUNTING_READ`.

### Test A — Navigation from Customers page (original flow preserved)
1. Open Customers page.
2. Click **"عرض الدفعات"** on Customer A row.
3. → CustomerPaymentsPage opens.
4. **Verify:** Context bar shows Customer A's name in bold (large, readable).
5. **Verify:** If ACCOUNTING_READ is granted, outstanding amount appears below name in `AccentBrush`.
6. **Verify:** Page title is simply "مدفوعات العملاء" — no small subtitle.

### Test B — Switch customer directly from the page
1. On CustomerPaymentsPage (filtered to Customer A), click **[تغيير العميل]**.
2. → Context bar switches to search mode; search field appears.
3. Type the first 2+ letters of Customer B.
4. → Dropdown appears with matching customers.
5. Click Customer B.
6. → Context bar updates: shows Customer B name in bold.
7. → Payment list reloads to Customer B's payments.
8. → If ACCOUNTING_READ granted, loading indicator appears briefly then shows Customer B's outstanding.

### Test C — Clear filter (global view)
1. On CustomerPaymentsPage (filtered to Customer A), click **[إلغاء التصفية]**.
2. → Context bar shows "جميع العملاء" (SemiBold, secondary text) + [اختر عميلاً] button.
3. → Payment list reloads showing all customer payments.
4. → Outstanding display disappears (no customer selected).

### Test D — Select customer from global view
1. On CustomerPaymentsPage in global mode, click **[اختر عميلاً]**.
2. Type a customer name.
3. Select from dropdown.
4. → Reverts to Test A/B behavior (filtered to selected customer).

### Test E — Overpayment error with outstanding context
1. Filter to a customer with a known outstanding balance (e.g., 500.00 جنيه).
2. Click **[تسجيل دفعة جديدة]**.
3. Customer is pre-filled from context.
4. Enter an amount greater than the outstanding (e.g., 1000.00).
5. Click Save.
6. → Error message shows: **"المبلغ يتجاوز الرصيد المستحق (500.00 جنيه)"** — specific, actionable.

### Test E2 — Overpayment error without ACCOUNTING_READ
1. Log in as a user with PAYMENTS_WRITE but NOT ACCOUNTING_READ.
2. Repeat Test E.
3. → Outstanding not shown in context bar.
4. → Error message shows: **"المبلغ يتجاوز الرصيد المستحق"** — plain, no value (acceptable, honest).

### Test F — Normal payment save + outstanding refresh
1. Filter to a customer with outstanding > 0.
2. Note the displayed outstanding amount.
3. Record a valid payment (amount ≤ outstanding).
4. → Payment succeeds; notification bar shows success.
5. → Outstanding amount in context bar decreases by the payment amount.
6. → Payment appears in the list.

### Test G — Receipt printing (regression check)
1. Find any payment in the list.
2. Click **[طباعة الإيصال]**.
3. → Print dialog opens; receipt preview shows correctly.
4. → No regression in CP-2 receipt content.

### Test H — Dark mode regression check
1. Switch to dark mode.
2. Open CustomerPaymentsPage.
3. → Context bar readable; customer name clearly visible.
4. → Outstanding text visible in accent color.
5. → No unreadable white-on-white or black-on-black text.

---

## Cleanup Audit

### Files touched reviewed
| File | Review result |
|------|---------------|
| `CustomerPaymentsViewModel.cs` | All new properties and commands are used; no dead code introduced. `_customerSearchCts` cancellation token was already correct. `CanViewAccounting` is checked before every `FetchOutstandingAsync` call — no unauthorized calls possible. |
| `CustomerPaymentsPage.xaml` | Old subtitle `TextBlock` with DataTrigger removed (replaced by context bar). The previously unused Row 3 is now occupied — no wasted rows. |
| `PartyOutstandingResponse.cs` | Minimal; only three properties matching exactly what the API returns. |
| `Strings.cs` / `Strings.resx` | 7 new keys added; all 7 are used in the XAML. No unused keys. |

### Unused code removed
- **Removed:** The two-line header `StackPanel` (title + DataTrigger subtitle) from `CustomerPaymentsPage.xaml`. The subtitle was the original "customer name or all-payments" text. This is replaced cleanly by the context bar. The old `CustomerPayments_AllSubtitle` and `CustomerPayments_ContextSubtitle` string keys in `Strings.resx` remain (were present from CP-2) but are no longer referenced in the XAML. They are **kept** because removing resx keys is unnecessary churn and future features may reference them.

### What was intentionally left and why
- `CustomerPayments_AllSubtitle` and `CustomerPayments_ContextSubtitle` resx keys — kept; harmless, no code reference means no cost.
- `_presetCustomerId` private field — unchanged; still the correct internal filter state.
- Modal typeahead popup inside the create form — unchanged; the context bar search uses the same `CustomerSearchResults` collection, but both cannot be active simultaneously (`IsEditing` and `IsSelectingCustomer` guard against it).

### Build/test verification after changes
```
Build succeeded.  0 Warning(s)  0 Error(s)
```
