# UI CUSTOMER PAYMENTS 2 — CUSTOMER PAYMENTS CORE + RECEIPT — CLOSEOUT

**Phase:** CP-2  
**Date:** 2026-04-01  
**Status:** ✅ Implemented — Awaiting Human Test  

---

## 1. Grounded Summary — What Was Implemented

Phase CP-2 delivered the full customer payment creation, history, and receipt printing workflow. All implementation is grounded in the actual backend generic payments contract (`/api/v1/payments?partyType=Customer`). No endpoints were invented.

### What changed:

#### 1.1 New: CustomerPaymentsPage + CustomerPaymentsViewModel
- A dedicated `CustomerPaymentsPage.xaml` / `CustomerPaymentsViewModel.cs` was created, mirroring the existing `SupplierPaymentsPage` architecture but for `partyType=Customer`.
- Page includes:
  - Structured two-line page header: title "مدفوعات العملاء" + contextual subtitle (shows customer name when pre-filtered, "جميع مدفوعات العملاء" otherwise)
  - Back-to-Customers navigation button (← العودة للعملاء) always visible in header
  - NotificationBar for success/error feedback
  - Toolbar: "تسجيل دفعة جديدة" (guarded by PaymentsWrite), Refresh, Search
  - DataGrid card (card wrapping with CornerRadius=8, border) matching Customers page visual baseline
  - Columns: Payment Number, Customer Name (SemiBold), Amount (SemiBold), Method, Reference, Date, Print button
  - Paging bar
  - Create Payment modal with sectioned, polished design:
    - Customer typeahead (debounced search → /api/v1/customers)
    - Amount (numeric, LTR)
    - Method (ComboBox: Cash, Visa, InstaPay)
    - Wallet Name (optional, for EWallet/InstaPay)
    - Reference (optional, LTR)
    - Styled error block (red background, white text)
    - Save button with "جاري الحفظ…" loading state
  - DropShadowEffect on modal card (consistent with CP-1 quality)
  - BusyOverlay during load

#### 1.2 Updated: CustomersPage — Actions Column
- New "عرض الدفعات" button added to every row in the DataGrid actions column
- Gated by `CanViewPayments` (PaymentsRead permission) — respects role-based access
- DataGrid actions column width widened from 290 to 360 to accommodate the new button without clipping

#### 1.3 Updated: Customer Details Modal — Payments Section
- The placeholder card "الملخص المالي والدفعات — قريبًا" has been **replaced** with a live action card
- The card contains an "عرض الدفعات" (AccentButtonStyle) that navigates immediately to CustomerPaymentsPage with the customer pre-filtered
- Card is gated by `CanViewPayments` permission — hidden for roles without PaymentsRead
- A subtle label "دفعات العميل" provides context inside the card

#### 1.4 Updated: CustomersViewModel
- Added `INavigationService` injection (constructor parameter)
- Added `CanViewPayments` computed property (PaymentsRead permission)
- Added `OpenCustomerPaymentsCommand(CustomerDto?)` — closes the details overlay, then navigates to CustomerPaymentsPage with the customer pre-filtered using the new `NavigateTo<T>(Action<T>)` overload

#### 1.5 Extended: INavigationService + NavigationService
- Added `NavigateTo<TViewModel>(Action<TViewModel> configure)` overload to both interface and implementation
- This overload:
  - Removes any existing cached VM for that type
  - Creates a fresh DI-resolved instance
  - Applies the configure action (e.g., SetCustomerFilter)
  - Caches and navigates to the fresh instance
- This is what enables clean customer-context navigation without stale state from previous navigation sessions

#### 1.6 Updated: DocumentPrintHelper
- Added `PrintCustomerPaymentReceipt(PaymentDto payment)` method
- Dual-copy receipt (أصل + صورة) using the shared `ReceiptPrintService`
- Header: "إيصال دفع عميل" (distinct from "إيصال دفع مورد")
- Fields: Receipt Number, Date, Customer Name (العميل:), Amount, Method, Reference (if present), Created By (if present), Notes, Signature block

#### 1.7 Updated: MainViewModel
- Added `case "CustomerPayments"` in `NavigateTo(string pageName)` switch — allows sidebar navigation

#### 1.8 Updated: MainWindow.xaml
- Added `DataTemplate` for `CustomerPaymentsViewModel` → `CustomerPaymentsPage`
- Added sidebar entry under the Accounting section:
  - Label: "مدفوعات العملاء" via `Nav_CustomerPayments`
  - Gated by `CanViewPayments`
  - People icon (Material Design path)

#### 1.9 Updated: Localization (Strings.cs + Strings.resx)
Added 9 new CP-2 string keys:

| Key | Arabic Value |
|-----|-------------|
| `Nav_CustomerPayments` | مدفوعات العملاء |
| `Customer_ViewPayments` | عرض الدفعات |
| `CustomerPayment_Created` | تم تسجيل الدفعة بنجاح |
| `CustomerPayment_CustomerRequired` | يجب اختيار العميل |
| `CustomerPayments_ContextSubtitle` | دفعات العميل |
| `CustomerPayments_AllSubtitle` | جميع مدفوعات العملاء |
| `CustomerPayments_BackToCustomers` | العودة للعملاء |
| `CustomerPayments_CreateFormTitle` | تسجيل دفعة عميل |
| `CustomerPayments_Party` | العميل |

---

## 2. Files Created / Modified

### Created
| File | Purpose |
|------|---------|
| `src/ElshazlyStore.Desktop/ViewModels/CustomerPaymentsViewModel.cs` | New VM for customer payments page |
| `src/ElshazlyStore.Desktop/Views/Pages/CustomerPaymentsPage.xaml` | New XAML page |
| `src/ElshazlyStore.Desktop/Views/Pages/CustomerPaymentsPage.xaml.cs` | Code-behind |
| `docs/UI CUSTOMER PAYMENTS 2 — CUSTOMER PAYMENTS CORE + RECEIPT — CLOSEOUT.md` | This file |

### Modified
| File | Change |
|------|--------|
| `src/ElshazlyStore.Desktop/Services/INavigationService.cs` | Added `NavigateTo<T>(Action<T>)` overload |
| `src/ElshazlyStore.Desktop/Services/NavigationService.cs` | Implemented `NavigateTo<T>(Action<T>)` overload |
| `src/ElshazlyStore.Desktop/ViewModels/CustomersViewModel.cs` | Added INavigationService injection, CanViewPayments, OpenCustomerPaymentsCommand |
| `src/ElshazlyStore.Desktop/Views/Pages/CustomersPage.xaml` | Replaced placeholder with live payments card; added "عرض الدفعات" column button |
| `src/ElshazlyStore.Desktop/ViewModels/MainViewModel.cs` | Added CustomerPayments navigation case |
| `src/ElshazlyStore.Desktop/Views/MainWindow.xaml` | Added DataTemplate + sidebar entry for CustomerPaymentsPage |
| `src/ElshazlyStore.Desktop/App.xaml.cs` | Registered CustomerPaymentsViewModel as Transient |
| `src/ElshazlyStore.Desktop/Helpers/DocumentPrintHelper.cs` | Added PrintCustomerPaymentReceipt |
| `src/ElshazlyStore.Desktop/Localization/Strings.cs` | Added 9 CP-2 string keys |
| `src/ElshazlyStore.Desktop/Localization/Strings.resx` | Added 9 CP-2 Arabic values |

### Not Modified
- `CustomerDto.cs` — no change; all existing fields used as-is
- `PaymentDto.cs` — no change; existing generic DTO used for customer payments too
- `CreatePaymentRequest.cs` — no change; existing generic request used
- `LightTheme.xaml` / `DarkTheme.xaml` — no change; new page uses existing brush system

---

## 3. Endpoints / Contracts Used

| Endpoint | Method | Usage |
|----------|--------|-------|
| `/api/v1/payments?partyType=Customer&page=...` | `GET` | Fetch customer payment list (paged, optionally filtered by partyId) |
| `/api/v1/payments?partyType=Customer&partyId={id}` | `GET` | Fetch payments for a specific customer |
| `/api/v1/payments` | `POST` | Create new customer payment (partyType=Customer) |
| `/api/v1/customers?page=1&pageSize=8&q=...` | `GET` | Customer typeahead search in create form |

**CreatePaymentRequest fields used:**
- `PartyType = "Customer"`
- `PartyId` — selected customer's GUID
- `Amount` — entered amount
- `Method` — "Cash" / "Visa" / "InstaPay"
- `WalletName` — optional wallet/InstaPay name
- `Reference` — optional reference
- `PaymentDateUtc = DateTime.UtcNow`

**Not used (field not in CreatePaymentRequest):** `Notes` — deferred (see Section 7)

---

## 4. CP-2 Scope Completed

- [x] Customer-linked payment creation flow
- [x] Customer-linked payment list / history (paged DataGrid, searchable)
- [x] Customer typeahead in create form
- [x] Payment amount
- [x] Payment method (Cash / Visa / InstaPay)
- [x] Payment wallet name (optional)
- [x] Payment reference (optional)
- [x] Successful save / true API confirmation before UI success
- [x] NotificationBar success message after save
- [x] List/history refreshes correctly after successful save
- [x] Honest error handling — FormError on save failure, no fake success
- [x] Customer payment receipt printing via shared print system (`PrintCustomerPaymentReceipt`)
- [x] Receipt content: customer name, payment number, amount, method, reference, date, created-by, signature block
- [x] Arabic-first / RTL preserved throughout
- [x] Light/dark mode correct — uses only `DynamicResource` brushes from existing theme
- [x] CanViewPayments gates payments button visibility in both list and details
- [x] Customer context maintained: subtitle shows customer name when pre-filtered
- [x] Back-to-Customers navigation

---

## 5. Customer-Context Integration Approach — Chosen & Why

**Chosen approach: Dedicated CustomerPaymentsPage launched from customer context**

### Why not embedded in the details modal:
- The payment list can grow arbitrarily long — paging works better as a full-page DataGrid
- The create form with typeahead needs screen real estate that a modal doesn't comfortably provide
- SupplierPaymentsPage already established this architecture; mirroring it makes the codebase consistent

### Why not orphaned from customer context:
- Navigation from customer details / actions **always carries the customer pre-filter** via `SetCustomerFilter` + the new `NavigateTo<T>(Action<T>)` overload
- A "context banner" (subtitle in the page header) shows the customer name at all times when pre-filtered
- A "العودة للعملاء" (Back) button is always present in the header — keeps the user oriented
- The user can also access the page directly from the sidebar (Accounting → مدفوعات العملاء) for a global view of all customer payments

### Why extending NavigationService was the right call:
- The existing `NavigateTo<T>()` caches and skips re-navigation if already on the same page
- For customer-context navigation, each click should produce a fresh filtered view for the clicked customer
- Adding a configure-callback overload is a minimal, non-breaking extension that solves this cleanly without coupling ViewModels or introducing shared state services

---

## 6. Conflicts Between Spec and Backend Truth

| Spec Desire | Backend Reality | Resolution |
|-------------|----------------|------------|
| Notes in payment creation | `CreatePaymentRequest` has no `Notes` field | **Deferred** — documented below |
| Customer balance / receivable summary | No `/api/v1/customers/{id}/balance` or equivalent endpoint in openapi.json | **Deferred** — not implemented |
| Last payments summary in customer details | No dedicated endpoint; only the full payment list is queryable | **Deferred** — details shows "View Payments" button leading to the full list |
| "EWallet" as a selectable method | PaymentMethods array in the existing supplier side also excludes EWallet; WalletName covers the field | **Consistent with existing behavior** — not a conflict |

No invented endpoints. No fabricated data. All displayed fields are from actual API responses.

---

## 7. Intentionally NOT Implemented in CP-2

The following are explicitly out of scope and have NOT been implemented:

- **Notes field in payment creation** — `CreatePaymentRequest` has no `Notes` property; displaying the field would silently discard input. Deferred until backend exposes it.
- **Customer balance / receivable summary** — no backend endpoint; would require inventing unsupported behavior. Deferred to a future backend phase.
- **Payment editing / voiding** — the generic payments API in the current openapi.json shows only GET + POST for payments; no PATCH/DELETE. Not implemented.
- **"Last N payments" mini-list in customer details** — would require additional API call and scrolling complexity in the detail modal. Deferred; user navigates to the full payments page instead.
- **EWallet as a ComboBox option** — consistent with existing SupplierPaymentsPage behavior; WalletName field is present for manual entry.
- **Supplier work** — NOT implemented
- **Print Config** — NOT implemented
- **Global header rollout** — NOT implemented
- **Global NotificationBar relocation** — NOT implemented
- **Shared Compo SearchBox rollout** — NOT implemented

---

## 8. CP-1 Visual Quality Assessment

**CP-1 visual quality was preserved and extended, not regressed.**

- The new `CustomerPaymentsPage` uses the same design language as the upgraded CustomersPage from CP-1:
  - Same two-line structured header (title + subtitle)
  - DataGrid wrapped in card (CornerRadius=8, BorderBrush, ClipToBounds)
  - Same button styles (AccentButtonStyle / SecondaryButtonStyle)
  - Same NotificationBar placement and binding pattern
  - Same modal style: CornerRadius=14, DropShadowEffect BlurRadius=28 Opacity=0.22, wide padding
  - Same styled error block (red border, white text)
  - Same save loading state ("جاري الحفظ…")
  - Same DynamicResource brushes throughout — correct in both light and dark mode
- The Customers page itself was extended (new button in list, live button in details) without touching the visual baseline that was already approved in CP-1
- The "عرض الدفعات" card in the details modal is styled using `CardBackgroundBrush` / `BorderBrush` / `CornerRadius=8` matching the identity card styling already in CP-1

---

## 9. Build / Test / Run Results

### Build
```
dotnet build ElshazlyStore.Desktop.csproj --configuration Release
→ Build succeeded. 0 Warning(s). 0 Error(s).
```

### Tests
```
dotnet test --configuration Release
→ Passed: 274 | Failed: 0 | Skipped: 0
```

### Localization audit
```
dotnet test --filter "LocalizationAudit"
→ Passed: 3 | Failed: 0
```

---

## 10. Human Test Script — CP-2

### Prerequisites
- App running against a live backend
- A user with PaymentsRead + PaymentsWrite permissions logged in
- At least one active customer exists (e.g., created during CP-1 testing)

---

### Test A: Navigate from Customer List → Payments

1. Open the app → navigate to **العملاء** (Customers page)
2. Find any active customer in the list
3. Click **"عرض الدفعات"** in the actions column for that customer
4. ✅ Expected: App navigates to **مدفوعات العملاء** page
5. ✅ Expected: Page subtitle shows the customer's name (not "جميع مدفوعات العملاء")
6. ✅ Expected: Back button "العودة للعملاء" is visible in the header

### Test B: Navigate from Customer Details → Payments

1. Open **العملاء** page
2. Click **"تفاصيل"** for any active customer
3. In the details modal, find the payments card and click **"عرض الدفعات"**
4. ✅ Expected: Details modal closes, app navigates to CustomerPaymentsPage
5. ✅ Expected: Page is pre-filtered for that customer

### Test C: Create a New Customer Payment

1. Navigate to customer payments for a specific customer (via Test A or B)
2. Click **"تسجيل دفعة جديدة"**
3. ✅ Expected: Create payment modal opens; customer name is pre-filled in the typeahead
4. Enter Amount: `500`
5. Select Method: `نقدي` (Cash)
6. Leave Reference empty; leave Wallet Name empty
7. Click **"حفظ"**
8. ✅ Expected: Button shows "جاري الحفظ…" while saving
9. ✅ Expected: Modal closes; NotificationBar shows "تم تسجيل الدفعة بنجاح" in green
10. ✅ Expected: The new payment appears in the list with correct Customer Name, Amount, Method

### Test D: Create Payment — Validation

1. Open create payment modal
2. Clear the customer field (backspace the name, don't select a customer)
3. Click **"حفظ"**
4. ✅ Expected: Error block shows "يجب اختيار العميل"
5. Re-select a customer via typeahead (type 2+ chars)
6. Leave Amount at 0
7. Click **"حفظ"**
8. ✅ Expected: Error about amount required (payment amount validation message)

### Test E: Create Payment with Visa + Reference

1. Open create payment modal (with pre-selected customer)
2. Amount: `1200`
3. Method: `فيزا` (Visa)
4. Reference: `TXN-20260401-001`
5. Click **"حفظ"**
6. ✅ Expected: Save succeeds; list shows new payment with Reference visible

### Test F: Print Receipt

1. In the CustomerPaymentsPage, find a payment row
2. Click **"طباعة الإيصال"** for that row
3. ✅ Expected: Print dialog opens
4. ✅ Expected: Document title is "إيصال دفع عميل — [PaymentNumber]"
5. ✅ Expected: Receipt shows: header "إيصال دفع عميل", customer name (العميل:), amount, method, reference (if present), date, created-by (if present), dual copy (أصل + صورة)

### Test G: Back Navigation

1. While on CustomerPaymentsPage (pre-filtered for customer X)
2. Click **"العودة للعملاء"**
3. ✅ Expected: Navigates back to Customers list page
4. ✅ Expected: Customers list is intact and paged normally

### Test H: Sidebar Navigation — All Customer Payments

1. From the sidebar, click **"مدفوعات العملاء"** under the Accounting section
2. ✅ Expected: CustomerPaymentsPage loads with subtitle "جميع مدفوعات العملاء"
3. ✅ Expected: All customer payments across all customers are visible

### Test I: Search Within Customer Payments

1. On CustomerPaymentsPage for a customer with multiple payments
2. Type a partial payment number or customer name in the search box + Enter
3. ✅ Expected: List filters correctly

### Test J: Dark Mode Verification

1. Toggle dark mode (Settings or top-right toggle)
2. Navigate to CustomerPaymentsPage
3. ✅ Expected: Header, DataGrid, buttons, modal are all readable; no white-on-white or black-on-black text
4. Open the create payment modal
5. ✅ Expected: All fields, labels, buttons, error block are readable in dark mode

---

## 11. Cleanup Audit

### Touched Files Reviewed
The following files were modified in CP-2:
- `CustomersPage.xaml`, `CustomersViewModel.cs`
- `INavigationService.cs`, `NavigationService.cs`
- `MainViewModel.cs`, `MainWindow.xaml`, `App.xaml.cs`
- `DocumentPrintHelper.cs`
- `Strings.cs`, `Strings.resx`

### What Was Found
- `Customers_PaymentsSectionPlaceholder` string key (Strings.cs + Strings.resx) is now unreferenced in XAML — previously used for the placeholder card which was replaced by the live payments section

### What Was Removed
Nothing was removed. The `Customers_PaymentsSectionPlaceholder` key was intentionally retained.

### Why `Customers_PaymentsSectionPlaceholder` Was Kept
- The localization audit test requires all keys in `Strings.cs` to have corresponding values in `Strings.resx` (they do)
- Removing it would require removing both the C# accessor AND the resx entry atomically, with verification that nothing in any branch or historical reference uses it
- The string property itself is a valid accessor; it is simply no longer visually rendered
- Removing it provides no runtime benefit and carries a tiny risk of requiring the string in some future context
- Explicitly leaving it is safer than hasty cleanup on a newly-shipped phase

### Build / Test After Cleanup Review
```
dotnet build → 0 errors, 0 warnings
dotnet test  → 274 passed, 0 failed
```

---

## 12. Explicit Non-Implementation Statements

- ✅ **Supplier work was NOT implemented** in this phase
- ✅ **Print Config was NOT implemented** in this phase
- ✅ **Global header rollout was NOT implemented** in this phase
- ✅ **Global NotificationBar relocation was NOT implemented** in this phase
- ✅ **Shared Compo SearchBox rollout was NOT implemented** in this phase

---

## 13. Summary

Phase CP-2 delivers the complete customer payments workflow:
- Customer-linked payment creation with typeahead customer selection
- Full customer payment history with paging and search
- Customer-context navigation (from list, from details) carrying the customer filter automatically
- Professional dual-copy customer payment receipt via the shared print system
- Back-to-Customers navigation
- Sidebar access for the global all-customer-payments view
- All built on the actual generic payments backend contract (`partyType=Customer`)
- No invented endpoints, no fakery, no backend truth violations
- 0 build errors, 274 tests passing
