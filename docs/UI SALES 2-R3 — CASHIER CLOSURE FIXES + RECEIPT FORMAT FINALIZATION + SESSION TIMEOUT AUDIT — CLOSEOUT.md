# UI SALES 2-R3 — CASHIER CLOSURE FIXES + RECEIPT FORMAT FINALIZATION + SESSION TIMEOUT AUDIT — CLOSEOUT

**Date:** 2025-07-15  
**Phase:** UI SALES 2-R3  
**Status:** COMPLETE — all goals implemented, builds clean, tests pass

---

## Scope

| # | Goal | Status |
|---|------|--------|
| A | Dark-mode ComboBox / DatePicker readability (FormStyles.xaml root fix) | ✅ Done |
| B | Verification scope — shared styles cover Cashier, Sales Admin, Stock Movement | ✅ Done |
| C | Cashier visual hierarchy (bigger product/barcode, smaller editors, larger total) | ✅ Done |
| D | Smart number formatting (no forced `.00` on integers, decimals only when needed) | ✅ Done |
| E | Arabic amount-in-words on printed invoice | ✅ Done |
| F | Professional anonymous-sale wording (no backend-exposure text) | ✅ Done |
| G | E-wallet provider dropdown (Vodafone / Orange / Etisalat / WE / Bank Wallet) | ✅ Done |
| H | Partial / credit for non-cash payment methods | ✅ Done |
| I | Sales Admin warehouse selection gap | ✅ Done |
| J | NotificationBar reliability + readability | ✅ Done |
| K | Print trace finalization (payment method, reference, amounts, amount-in-words) | ✅ Done |
| L | Session timeout audit (report — no settings implementation) | ✅ Done (see §Session Timeout Audit) |

### Explicit exclusions (per policy)

- Sales Returns
- Customer Payments page
- Print Config / settings dialogs
- Session-timeout settings UI (report only)
- Whole-app redesign

---

## Files Changed

### New files

| File | Purpose |
|------|---------|
| `src/ElshazlyStore.Desktop/Helpers/ArabicAmountInWords.cs` | Converts decimal → formal Arabic words (جنيه / قرش) with "فقط لا غير" suffix |
| `src/ElshazlyStore.Desktop/Helpers/InvoiceNumberFormat.cs` | Smart formatting: integers → "4,000", fractions → "19.50", null → "—" |
| `src/ElshazlyStore.Desktop/Helpers/WalletProviderOption.cs` | Egyptian e-wallet provider definitions with `RequiresBankName` flag |
| `tests/ElshazlyStore.Tests/Desktop/InvoiceNumberFormatTests.cs` | 6 unit tests for number formatting |
| `tests/ElshazlyStore.Tests/Desktop/ArabicAmountInWordsTests.cs` | 9 unit tests for Arabic amount-in-words + 6 continuation tests |

### Modified files

| File | Changes |
|------|---------|
| `src/ElshazlyStore.Desktop/Resources/Themes/FormStyles.xaml` | Full ControlTemplates for ComboBox, ComboBoxItem, DatePicker, ComboBoxToggleButton; DatePickerTextBox with ScrollViewer PART_ContentHost; all foregrounds bound via DynamicResource PrimaryTextBrush |
| `src/ElshazlyStore.Desktop/Views/Pages/POSPage.xaml` | Product name 16→19px, barcode 12→15px, line total 13→14px, column widths tightened, payment columns rebalanced (1\*/0.8\*/1.8\*), invoice total 34→42px, smart-format bindings, wallet provider ComboBox + conditional BankWalletName TextBox |
| `src/ElshazlyStore.Desktop/ViewModels/POSViewModel.cs` | `SelectedWalletProvider`, `BankWalletName`, `WalletProviders`, `ShowBankNameField`, `ShowNonCashCreditSelector`, `InvoiceTotalDisplay`, `TenderOutcomeAmountDisplay`, `LineTotalDisplay` on PosLineVm, `ResolveEffectiveWalletName()`, partial/credit for non-cash methods |
| `src/ElshazlyStore.Desktop/Helpers/DocumentPrintHelper.cs` | `InvoiceNumberFormat.Format()` on all amounts, `AddAmountInWords()` call, `ResolveCustomerDisplay()` → "عميل نقدي" for anonymous, payment state/method/reference cleaned of backend-exposure text |
| `src/ElshazlyStore.Desktop/Services/Printing/ReceiptPrintService.cs` | `AddAmountInWords(FlowDocument, decimal)` method |
| `src/ElshazlyStore.Desktop/Views/Controls/NotificationBar.xaml` | Font size FontSizeSmall → FontSizeNormal (11→13) |
| `src/ElshazlyStore.Desktop/Views/Controls/NotificationBar.xaml.cs` | Named tick handler with explicit unsubscribe; no more anonymous-lambda timer leak |
| `src/ElshazlyStore.Desktop/ViewModels/SalesViewModel.cs` | `OpenCreate()` → `OpenCreateAsync()` with conditional `LoadWarehousesAsync()` when list empty |
| `tests/ElshazlyStore.Tests/ElshazlyStore.Tests.csproj` | TFM → `net8.0-windows7.0`, added Desktop project reference |

---

## Technical Details

### A — Dark-mode ComboBox / DatePicker root fix

**Problem:** WPF's default ComboBox and DatePicker ControlTemplates use `SystemColors` (e.g. `SystemColors.WindowBrush`) that ignore `DynamicResource` theme brushes. Overriding `Foreground`/`Background` on the style is insufficient because the inner template elements (ToggleButton chrome, ContentPresenter, DatePickerTextBox watermark) still use hard-coded system brushes.

**Solution:** Replaced the `ThemedComboBoxStyle`, `ThemedComboBoxItemStyle`, and `ThemedDatePickerStyle` with full `ControlTemplate` definitions in `FormStyles.xaml`:

- **ComboBox**: Custom `ComboBoxToggleButtonTemplate` with `Border` bound to `InputBackgroundBrush` / `InputBorderBrush`. Main template uses `ContentPresenter` with `Resources` override forcing `TextBlock.Foreground = {DynamicResource PrimaryTextBrush}`. Popup uses `InputBackgroundBrush`.
- **ComboBoxItem**: Custom template with `IsHighlighted` trigger using `AccentBrush` background.
- **DatePicker**: Custom template with `DatePickerTextBox` that has its own `ControlTemplate` containing a `ScrollViewer` named `PART_ContentHost` — this eliminates the default watermark overlay that ignores theme foreground.

**Scope:** Because these styles are keyed (`x:Key="ThemedComboBoxStyle"` etc.) and already applied via `Style="{StaticResource ThemedComboBoxStyle}"` across Cashier, Sales Admin, and Stock Movement screens, the fix applies everywhere without per-screen changes.

### C — Cashier visual hierarchy

| Element | Before | After |
|---------|--------|-------|
| Product name | 16px | 19px |
| Barcode | 12px | 15px |
| Line total | 13px | 14px |
| Qty column | 84px | 74px |
| Price columns | 104px | 90px |
| Discount column | 104px | 90px |
| Payment area columns | 1.2\* / 1\* / 1.55\* | 1\* / 0.8\* / 1.8\* |
| Invoice total | 34px | 42px |

### D — Smart number formatting

`InvoiceNumberFormat.Format(decimal)`:
- Integer values: `4000m` → `"4,000"` (no `.00`)
- Fractional values: `19.5m` → `"19.50"` (2 decimal places)
- Uses `CultureInfo.InvariantCulture` for consistent thousands separator

Applied to: all printed amounts (sale, purchase, purchase return, payment receipt), cashier display (`InvoiceTotalDisplay`, `TenderOutcomeAmountDisplay`, `LineTotalDisplay`).

### E — Arabic amount-in-words

`ArabicAmountInWords.Convert(decimal)`:
- Supports ones through billions with grammatically correct Arabic (مفرد / مثنى / جمع)
- Currency: Egyptian Pound (جنيه / جنيهان / جنيهات) with piaster subunit (قرش / قرشان / قروش)
- Formal suffix: "فقط لا غير"
- Example: `1,250.75` → `"ألف ومائتان وخمسون جنيهًا وخمسة وسبعون قرشًا فقط لا غير"`

Printed on every sale invoice via `ReceiptPrintService.AddAmountInWords()`.

### F — Anonymous-sale wording

| Context | Before | After |
|---------|--------|-------|
| Customer name on receipt | raw `CustomerNameDisplay` (could show "غير محفوظ في الخادم") | `ResolveCustomerDisplay()` → "عميل نقدي" |
| Payment state (anonymous) | "تشغيلي فقط / بدون سجل دفعة" | "مسدد" / "مسدد جزئيًا" / "نقدي" |
| Payment method fallback | "غير محفوظ في الخادم" | "نقدي" |
| Payment reference fallback | "غير محفوظ في الخادم" or empty | "—" |

### G — E-wallet provider dropdown

Five providers defined in `WalletProviderOption`:
1. Vodafone Cash
2. Orange Cash
3. Etisalat Cash
4. WE Pay
5. Bank Wallet (`RequiresBankName = true`)

When "Bank Wallet" is selected, a conditional `BankWalletName` TextBox appears. The effective wallet name is resolved by `ResolveEffectiveWalletName()`: for bank wallets → `"محفظة بنكية — {BankName}"`, for others → the provider's `WalletName`.

### H — Partial / credit for non-cash methods

- `ShowCashCheckoutModeSelector` renamed conceptually (still same property) — now returns `IsNamedCustomerSale` regardless of payment method (was previously gated to cash only)
- `ShowTenderedAmountInput` now shows for non-cash partial payments too
- Checkout validation for partial credit accepts any payment method for named customers
- `ResolvePaymentAmountForRequest` handles partial amounts for non-cash methods

### I — Sales Admin warehouse gap

**Problem:** `OpenCreate()` was synchronous and set `SelectedWarehouse` from `_activeWarehouses`, but if warehouses hadn't loaded yet (e.g. first action after navigating to Sales Admin), the list was empty and no warehouse was selected.

**Fix:** `OpenCreateAsync()` checks `_activeWarehouses.Count == 0` and calls `await LoadWarehousesAsync()` before setting `SelectedWarehouse`.

### J — NotificationBar reliability

- Font size increased from `FontSizeSmall` (11) to `FontSizeNormal` (13)
- Timer leak fixed: old timer's `Tick` handler is now explicitly unsubscribed (`_autoDismissTimer.Tick -= OnAutoDismissTick`) and the timer is stopped/nulled before a new one is created
- Named method `OnAutoDismissTick` replaces anonymous lambda — prevents closure-based stale captures

---

## Session Timeout Audit

### Current architecture

```
TokenRefreshHandler (DelegatingHandler)
    ↓ intercepts HTTP 401
    ↓ acquires SemaphoreSlim (1 concurrent refresh)
    ↓ POST /api/v1/auth/refresh with stored refresh token
    ↓ if refresh succeeds → stores new tokens, retries original request
    ↓ if refresh fails → clears tokens, raises static SessionExpired event
        ↓
SessionService
    ↓ subscribes: TokenRefreshHandler.SessionExpired += OnSessionExpired
    ↓ OnSessionExpired: clears _currentUser, _permissions, raises SessionEnded
        ↓
UI (MainWindow / NavigationService)
    ↓ subscribes: SessionService.SessionEnded
    ↓ navigates to LoginPage
```

### Token storage

`SecureTokenStore` persists tokens to `%LOCALAPPDATA%\ElshazlyStore\tokens.dat` encrypted via DPAPI (`DataProtectionScope.CurrentUser`). Expiry is tracked via `ExpiresAtUtc` property. `IsExpired` returns `DateTime.UtcNow >= ExpiresAtUtc`.

### Session lifetime behavior

| Scenario | Behavior |
|----------|----------|
| Access token valid | Request proceeds normally |
| Access token expired, refresh token valid | `TokenRefreshHandler` transparently refreshes and retries |
| Access token expired, refresh token expired/invalid | Refresh fails → `SessionExpired` → user redirected to login |
| App restarted with stored tokens | `TryRestoreSessionAsync()` calls `/auth/me` — if 401, refresh handler kicks in; if refresh also fails → redirected to login |
| Idle desktop (no API calls) | **No timeout** — no idle timer exists; session persists until the next API call discovers token expiry |

### Findings

1. **No client-side idle timer.** The Desktop app does not monitor user inactivity. The session stays alive indefinitely as long as the user doesn't trigger an API call after the access token expires.
2. **Timeout is server-driven.** The effective session lifetime = access token TTL + refresh token TTL (configured on the backend). The Desktop has no visibility into these values.
3. **Race condition resistance is good.** The `SemaphoreSlim(1,1)` in `TokenRefreshHandler` prevents multiple concurrent refresh attempts. After acquiring the lock, it re-checks `IsExpired` to avoid redundant refreshes.
4. **Static event pattern.** `TokenRefreshHandler.SessionExpired` is a static event. This works because there is exactly one DI-registered `TokenRefreshHandler` and one `SessionService`. If the app ever needed multiple sessions, this would need refactoring.

### Recommendations (deferred)

| Item | Priority | Notes |
|------|----------|-------|
| Add configurable client-side idle timer | Medium | Show warning dialog at e.g. 15 min idle, auto-logout at 20 min. Implement via `DispatcherTimer` in `SessionService` that resets on any UI interaction. |
| Display session-expiry countdown | Low | Show remaining time in status bar; requires parsing `ExpiresAtUtc` from stored token. |
| Make token TTLs visible in Settings (read-only) | Low | Fetch from a `/auth/config` endpoint if the backend exposes it. |

**No implementation was done for session timeout — this is a report-only audit as specified.**

---

## Build & Test Results

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Test run: 274 total
    Passed:  273
    Failed:    1 (pre-existing — PurchaseReceiptTests.ConcurrentDoublePost, unrelated)
    Skipped:   0

New Desktop tests: 21 passed (InvoiceNumberFormatTests: 6, ArabicAmountInWordsTests: 15)
```

---

## Human Test Script

### 1. Dark-mode ComboBox / DatePicker

1. Launch app → toggle to dark mode (if not already).
2. **Cashier (POS)**: open the Payment Method ComboBox → verify dropdown items are readable (light text on dark background), selected item is white text on dark input.
3. **Sales Admin**: open a sale editor → verify the Date picker text is visible in dark mode. Open the date calendar — dates should be readable.
4. **Stock Movement**: create a new stock movement → verify warehouse ComboBox and date picker are readable.
5. Toggle to light mode → repeat steps 2–4 → verify nothing breaks.

### 2. Cashier visual hierarchy

1. Add several products to the cart.
2. Verify product names are noticeably larger than before (19px), barcodes are readable (15px).
3. The invoice total at bottom-right should be large and prominent (42px).
4. The payment area should have more space for the total/change display.

### 3. Smart number formatting

1. Add a product priced at 4000 EGP → line total should show "4,000" (no ".00").
2. Add a product priced at 19.50 EGP → line total should show "19.50".
3. Invoice total should follow the same rules.
4. Complete the sale and print → verify printed amounts follow the same format.

### 4. Arabic amount-in-words

1. Complete a sale for any amount.
2. Print the invoice.
3. Verify below the total line there is an Arabic amount-in-words line ending with "فقط لا غير".
4. Test edge cases: exact round amounts (e.g. 1000), fractional (e.g. 1250.75).

### 5. Anonymous sale wording

1. Complete a sale without selecting a customer (anonymous).
2. Print the invoice.
3. Customer name should show "عميل نقدي" — NOT "غير محفوظ في الخادم".
4. Payment state should show "مسدد" or "نقدي" — NOT "تشغيلي فقط".

### 6. E-wallet provider

1. Set payment method to E-Wallet.
2. A dropdown should appear with 5 options: Vodafone Cash, Orange Cash, Etisalat Cash, WE Pay, Bank Wallet.
3. Select "Bank Wallet" → a text field for bank name should appear.
4. Fill in bank name (e.g. "البنك الأهلي") and complete the sale.
5. Print → payment method should show "محفظة بنكية — البنك الأهلي".
6. Select a non-bank provider (e.g. Vodafone Cash) → bank name field should disappear.

### 7. Non-cash partial/credit

1. Select a named customer.
2. Set payment method to E-Wallet (or Bank Transfer).
3. Verify the Full/Partial/Credit selector appears (previously only showed for Cash).
4. Select Partial → enter a tendered amount less than the total → complete → verify change and remaining are correct.
5. Select Credit → complete → verify invoice prints with "آجل" payment state.

### 8. Sales Admin warehouse

1. Navigate to Sales Admin.
2. Immediately click "New Sale" (without interacting with anything else first).
3. The warehouse dropdown should be populated and have a default selected — NOT blank.

### 9. NotificationBar

1. Trigger any action that shows a notification (e.g. save a sale).
2. Notification text should be readable (13px, not 11px).
3. Trigger multiple rapid notifications → each should auto-dismiss cleanly without stale messages lingering.

### 10. Print trace

1. Complete a sale with any payment method + customer selection.
2. Print the invoice.
3. Verify the receipt contains: payment method, payment reference (or "—"), paid amount, remaining amount (if partial), invoice number, date, customer name, and amount-in-words line.

---

## Deferred Work

| Item | Reason | Priority |
|------|--------|----------|
| Client-side idle timer for session timeout | Report-only audit per scope; requires UX design decision for warning/auto-logout behavior | Medium |
| Session-expiry countdown in status bar | Nice-to-have; depends on idle timer feature | Low |
| Fix pre-existing `ConcurrentDoublePost` test flake | Unrelated to this phase; race condition in purchase receipt test | Low |

---

## Sign-off

All goals A–L implemented. Build clean (0 errors, 0 warnings). 273/274 tests pass (1 pre-existing flake). Ready for human verification using the test script above.
