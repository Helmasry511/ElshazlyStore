# UI SALES 2-R5B — SALES/CASHIER FINAL READABILITY + NUMBER FORMAT ROLLOUT + ANONYMOUS WORDING CLEANUP + CLEANUP AUDIT — CLOSEOUT

**Date:** 2026-03-30
**Phase:** UI SALES 2-R5B
**Status:** IMPLEMENTED — AWAITING HUMAN ACCEPTANCE TEST

---

## EXACT FILES CHANGED

| File | Change |
|------|--------|
| `src/ElshazlyStore.Desktop/Views/Pages/SalesPage.xaml` | Detail modal: added `TextElement.Foreground=PrimaryTextBrush` on parent StackPanel; boosted value TextBlocks to FontSize="14" and FontWeight="SemiBold" for key fields (InvoiceNumber, CustomerNameDisplay, WarehouseName, InvoiceDateUtc, CashierUsername, Notes). Edit modal: added `TextElement.Foreground=PrimaryTextBrush` on parent StackPanel; added explicit `Foreground=PrimaryTextBrush` and `FontSize="14"` on SelectedCustomerDisplay TextBlock inside SidebarBackgroundBrush border; added explicit `Foreground=PrimaryTextBrush` on InvoiceTotal TextBlock inside SidebarBackgroundBrush border. |
| `src/ElshazlyStore.Desktop/Views/Pages/POSPage.xaml` | Added `Foreground=PrimaryTextBrush` on LineTotalDisplay TextBlock in cashier line items (was missing — dark-on-dark fix). Upgraded TenderOutcomeLabel/TenderOutcomeAmountDisplay from FontSizeNormal (13px) to 15px for stronger financial readout. Removed dead collapsed Grid.Row="3" with empty TextBlock. |
| `src/ElshazlyStore.Desktop/Localization/Strings.resx` | `POS_CheckoutPostedAnonymousNoPayment`: removed "لا يوجد سجل دفعة محفوظ في الخادم لهذا البيع" (server payment record leak) → clean "تم ترحيل البيع النقدي بنجاح." Smart number format: changed `{0:N2}` → `{0}` in `POS_CheckoutPostedAnonymousChangeDue`, `POS_CheckoutPostedPaymentPartial`, `POS_CheckoutPostedPaymentSavedWithChange`, `Sales_RetailPriceButton`, `Sales_WholesalePriceButton`. Removed dead `POS_NamedPaymentHint` string (unused, contained API path `/api/v1/payments`). |
| `src/ElshazlyStore.Desktop/Localization/Strings.cs` | Removed `POS_NamedPaymentHint` accessor (matching .resx cleanup). |
| `src/ElshazlyStore.Desktop/ViewModels/POSViewModel.cs` | Pre-formatted amounts with `InvoiceNumberFormat.Format()` in `BuildCheckoutMessage()`: `ChangeDueAmount`, `EffectiveTenderedPaymentAmount`, `RemainingAmount`, `InvoiceTotalAmount`. |
| `src/ElshazlyStore.Desktop/ViewModels/SalesViewModel.cs` | Pre-formatted `RetailPrice.Value` and `WholesalePrice.Value` with `InvoiceNumberFormat.Format()` in `RetailPriceLabel`/`WholesalePriceLabel` properties. |

**7 files changed — 2 XAML, 2 C# ViewModels, 2 Localization files, 0 backend files.**

---

## GOAL A — TYPOGRAPHY/READABILITY IMPROVEMENTS

### Sales Admin Detail Modal

**Root cause:** All value TextBlocks (InvoiceNumber, StatusDisplay, CustomerNameDisplay, WarehouseName, InvoiceDateUtc, CashierUsername, Notes) inside the detail modal had no explicit `Foreground`. The modal's parent Border uses `ContentBackgroundBrush` which in dark mode is near-black (#1E1E2E). The WPF default Foreground inherits as Black — creating black-on-dark-background unreadable text.

**Fix:** Added `TextElement.Foreground="{DynamicResource PrimaryTextBrush}"` on the detail modal's parent `StackPanel`. This propagates `PrimaryTextBrush` (#E6ECFF dark / #4C4F69 light) to all child TextBlocks that don't set their own explicit Foreground. Labels with `FormLabelStyle` (which sets `Foreground=SecondaryTextBrush` via Style setter) are unaffected — Style setters have higher precedence than inherited values.

Additionally boosted key value TextBlocks:

| Field | Before | After |
|-------|--------|-------|
| InvoiceNumber | FontFamily only | FontSize="14" FontWeight="SemiBold" |
| CustomerNameDisplay | FontFamily only | FontSize="14" |
| WarehouseName | FontFamily only | FontSize="14" |
| InvoiceDateUtc | FontFamily only | FontSize="14" |
| CashierUsername | FontFamily only | FontSize="14" |
| Notes | FontFamily only | FontSize="13" |
| StatusDisplay | FontWeight="SemiBold" (unchanged) | (already strong) |
| TotalAmount | FontWeight="Bold" (unchanged) | (already strong) |

### Sales Admin Edit Modal

**Root cause — SelectedCustomerDisplay:** TextBlock inside a Border with `Background=SidebarBackgroundBrush` (#181825 dark) had no explicit `Foreground`. Same dark-on-dark pattern already fixed for FormInvoiceNumberDisplay and FormStatus in R5A-R2.

**Root cause — InvoiceTotal:** TextBlock inside a Border with `Background=SidebarBackgroundBrush` (#181825 dark) had no explicit `Foreground`. FontSize="16" and FontWeight="Bold" were already set, but invisible text regardless of typography weight.

**Fix:** Added `TextElement.Foreground="{DynamicResource PrimaryTextBrush}"` on the edit modal's parent StackPanel (covers all value TextBlocks in the modal), plus explicit `Foreground="{DynamicResource PrimaryTextBrush}"` directly on SelectedCustomerDisplay and InvoiceTotal TextBlocks (belt-and-suspenders for the SidebarBackgroundBrush containers). Added `FontSize="14"` on SelectedCustomerDisplay for visual consistency.

| Field | Before | After |
|-------|--------|-------|
| SelectedCustomerDisplay | FontFamily only, no Foreground | FontSize="14", Foreground=PrimaryTextBrush |
| InvoiceTotal | FontSize="16" Bold, no Foreground | + Foreground=PrimaryTextBrush |
| All edit modal values | Inherited Black Foreground | Inherited PrimaryTextBrush via parent |

### Cashier (POSPage)

**Root cause — LineTotalDisplay:** TextBlock in cashier line items had FontSize="14" FontWeight="Bold" but no explicit `Foreground`. In dark mode, inherits Black from WPF default — unreadable.

**Root cause — TenderOutcomeLabel/Amount:** Already had `PrimaryTextBrush` foreground but used `FontSizeNormal` (13px) — adequate but visually weak for the most important financial feedback display (change due / remaining / exact tender).

**Fix:**

| Element | Before | After |
|---------|--------|-------|
| LineTotalDisplay | No Foreground | `Foreground=PrimaryTextBrush` |
| TenderOutcomeLabel | FontSizeNormal (13px) | FontSize="15" |
| TenderOutcomeAmountDisplay | FontSizeNormal (13px) | FontSize="15" |

---

## GOAL B — SMART NUMBER FORMAT ROLLOUT

### Rule (unchanged from R3/R4)
- Integer amounts → no decimal suffix: `4000` → `4,000`
- Fractional amounts → 2 decimal places: `19.50` → `19.50`
- Thousands separator always applied
- Exact arithmetic preserved internally (only display formatting)

### Rolled out in R5B

**Notification messages:**

| String | Before | After |
|--------|--------|-------|
| `POS_CheckoutPostedAnonymousChangeDue` | `{0:N2}` (always 2 decimals) | `{0}` (pre-formatted via `InvoiceNumberFormat.Format()`) |
| `POS_CheckoutPostedPaymentPartial` | `{0:N2}`, `{1:N2}` | `{0}`, `{1}` (pre-formatted) |
| `POS_CheckoutPostedPaymentSavedWithChange` | `{0:N2}`, `{1:N2}` | `{0}`, `{1}` (pre-formatted) |

**Price helper buttons:**

| String | Before | After |
|--------|--------|-------|
| `Sales_RetailPriceButton` | `تجزئة {0:N2}` | `تجزئة {0}` (pre-formatted via `InvoiceNumberFormat.Format()`) |
| `Sales_WholesalePriceButton` | `جملة {0:N2}` | `جملة {0}` (pre-formatted via `InvoiceNumberFormat.Format()`) |

### Already correct (unchanged)
- Sales Admin grid/detail/edit: All use `SmartNumberConv` converter (from R4) ✅
- Cashier: `InvoiceTotalDisplay`, `TenderOutcomeAmountDisplay`, `LineTotalDisplay` (via `InvoiceNumberFormat.Format()` in POSViewModel, from R3) ✅
- Print: All print amounts (via `InvoiceNumberFormat.Format()` in DocumentPrintHelper, from R3) ✅

### No parsing regressions
- `.resx` format strings changed from `{0:N2}` to `{0}` — this does not affect parsing
- Pre-formatting happens in the ViewModel before `string.Format()` — the formatted string is display-only, no round-trip parsing occurs
- All editable numeric fields (TextBoxes with `NumericTextBoxBehavior`) remain bound to raw `decimal` properties — unaffected

---

## GOAL C — ANONYMOUS WORDING CLEANUP

### Strings changed

| String Key | Old (problematic) | New (professional) | Issue |
|-----------|----|----|------|
| `POS_CheckoutPostedAnonymousNoPayment` | تم ترحيل البيع بدون عميل بنجاح. لا يوجد سجل دفعة محفوظ في الخادم لهذا البيع. | تم ترحيل البيع النقدي بنجاح. | Exposed "no payment record saved on server" to user — weakens trust, leaks backend implementation |

### Strings already clean (verified, not changed)

| String Key | Current Value | Status |
|-----------|-------|--------|
| `POS_PersistenceHintAnonymous` | بيع نقدي — يُرحَّل مباشرة ويُسجَّل في حركة المبيعات. | ✅ Professional (cleaned in R4) |
| `POS_CheckoutPostedAnonymousChangeDue` | تم ترحيل البيع النقدي بنجاح. الباقي للعميل: {0}. | ✅ Professional (cleaned in R4, format updated in R5B) |
| `POS_ModeAnonymousTitle` | بيع فوري بدون عميل | ✅ Professional (cleaned in R4) |
| `POS_ModeAnonymousDescription` | بيع نقدي فوري — يُرحَّل ويُسجَّل مباشرة في حركة المبيعات. | ✅ Professional (cleaned in R4) |
| `POS_PrintAnonymousOperationalNote` | بيع نقدي — بيانات السداد المرفقة للتوثيق الداخلي. | ✅ Professional (cleaned in R4) |
| `POS_AnonymousRemainingNotAllowed` | لا يمكن إتمام بيع نقدي بمبلغ أقل من إجمالي الفاتورة. | ✅ Professional (cleaned in R4) |
| `Sales_AnonymousCustomer` | بدون عميل | ✅ Professional |
| `Sales_AnonymousAction` | بيع بدون عميل | ✅ Professional |
| `POS_AnonymousPaymentHint` | البيع النقدي يُرحَّل مباشرة عند إتمام العملية. | ✅ Professional (cleaned in R4) |

### Printed invoice display (verified, not changed)
- `DocumentPrintHelper.ResolveCustomerDisplay()`: Returns "عميل نقدي" (Cash customer) for anonymous sales ✅
- `DocumentPrintHelper.ResolvePaymentStateLabel()`: Returns "مسدد" / "مسدد جزئيًا" / "نقدي" for anonymous ✅
- No server/payment-record implementation-level text appears on printed invoices ✅

### Business/accounting truth NOT altered
- `IsOperationalOnly` flag in `SalePaymentTraceDto` remains unchanged
- `BuildPrintTraceOverride()` still sets `IsOperationalOnly = true` for anonymous sales
- `ResolvePaymentStateLabel()` still uses `IsOperationalOnly` internally for routing
- No backend behavior, DTO shape, or API call changed
- No payment persistence state invented or faked

---

## GOAL D — SESSION/LOGOUT BEHAVIOR CONFIRMATION (REPORT ONLY)

### Source verification (2026-03-30)

1. **No client-side idle timer exists.**
   - Searched entire Desktop codebase for `idle`, `timer.*logout`, `InactivityTimer`, `DispatcherTimer.*session`, `SessionTimeout` — zero matches.
   - No `DispatcherTimer`, `System.Timers.Timer`, or `System.Threading.Timer` is used for session/idle tracking anywhere in the Desktop app.

2. **Logout is token/refresh-driven.**
   - `TokenRefreshHandler` (DelegatingHandler) intercepts 401 responses, attempts refresh via `/api/v1/auth/refresh`, and raises `SessionExpired` event on failure.
   - `SessionService` subscribes to `TokenRefreshHandler.SessionExpired` and redirects to login screen.
   - Manual logout calls `SessionService.LogoutAsync()` → POSTs to `/api/v1/auth/logout` → clears local session → navigates to login.

3. **Future simplest option (if approved later): manual logout only.**
   - The current architecture already supports manual logout via the sidebar button.
   - Adding an idle timeout would require a new `DispatcherTimer` + mouse/keyboard activity tracking, which is more complex than "manual logout only."
   - If approved, no additional API changes are needed — the existing `/api/v1/auth/logout` endpoint is sufficient.

**No auth/session implementation changes were made in this phase.**

---

## CLEANUP AUDIT

### Touched files reviewed

| File | Dead code found? | Action |
|------|:-:|--------|
| `SalesPage.xaml` | No | No dead XAML resources, styles, templates, or collapsed elements found |
| `POSPage.xaml` | **Yes** | Removed dead `<Grid Grid.Row="3" Visibility="Collapsed"><TextBlock /></Grid>` — permanently collapsed element containing empty TextBlock, no bindings, no code-behind references |
| `POSViewModel.cs` | No | All private fields, properties, methods, commands are actively used; no commented-out code |
| `SalesViewModel.cs` | No | All code actively used; no commented-out code |
| `Strings.resx` | **Yes** | Removed `POS_NamedPaymentHint` — unused string with API path leakage (`/api/v1/payments`); never referenced in any ViewModel or XAML |
| `Strings.cs` | **Yes** | Removed `POS_NamedPaymentHint` accessor (matching .resx cleanup) |

### Removal safety verification

| Removed item | Verification method | Safe? |
|-------------|---------------------|:-----:|
| `Grid.Row="3" Collapsed` in POSPage.xaml | `Visibility="Collapsed"` hard-coded (not data-bound), empty TextBlock, grep shows no x:Name or code-behind reference | ✅ |
| `POS_NamedPaymentHint` in Strings.resx | Grep entire codebase: 0 references in ViewModels, 0 references in XAML, only the auto-generated Strings.cs accessor existed | ✅ |
| `POS_NamedPaymentHint` in Strings.cs | Accessor removed after .resx entry removed; no compilation reference exists | ✅ |

### Intentionally left (not removed)

| Item | Location | Reason left |
|------|----------|-------------|
| `POS_AnonymousPaymentHint` | Strings.resx | Referenced in POSPage.xaml Search results imply it could be used; also the string content is professional and clean |
| Implicit `DatePickerTextBox` style | FormStyles.xaml | Defense-in-depth fallback for DatePicker (documented in R5A closeout) — not touched in this phase |

### Build/test verification after cleanup
```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Passed! - Failed: 0, Passed: 274, Skipped: 0, Total: 274
```

---

## BUILD AND TEST RESULT

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:07.77

Passed!  - Failed: 0, Passed: 274, Skipped: 0, Total: 274, Duration: 18s
```

---

## HUMAN TEST CHECKLIST

### Sales Admin — Detail Modal
- [ ] Open any sale detail → verify all value fields (invoice number, status, customer, warehouse, date, cashier, total, notes) are readable in dark mode
- [ ] Verify value fields appear at a comfortable, slightly stronger font size
- [ ] Verify FormLabel text remains subtle/secondary (SecondaryTextBrush)
- [ ] Verify line items data grid is still readable

### Sales Admin — Edit Modal
- [ ] Open create or edit modal → verify all fields are readable in dark mode
- [ ] Verify selected customer name displays with clear foreground inside the customer border
- [ ] Verify invoice total at bottom displays with clear foreground and bold weight
- [ ] Verify line items: variant name (FontSize="14" SemiBold), line total (FontSize="14" Bold) are readable
- [ ] Verify retail/wholesale price buttons show smart formatting (e.g., "تجزئة 4,000" not "تجزئة 4,000.00")
- [ ] Add product → verify variant display, quantity, unit price, discount, line total all appear correctly
- [ ] Edit quantity/price → verify binding still works, LineTotal recalculates

### Cashier
- [ ] Verify line item total (LineTotalDisplay) is readable in dark mode
- [ ] Scan/add items → verify product name (19px Bold), barcode (15px SemiBold), variant (11px), total (14px Bold) are all visible
- [ ] Enter tendered amount → verify change/remaining label and amount display at 15px font size are visually clear
- [ ] Complete anonymous sale (exact amount) → verify notification says "تم ترحيل البيع النقدي بنجاح." (no server/payment wording)
- [ ] Complete anonymous sale (overpay, change due) → verify notification shows smart-formatted change amount (e.g., "الباقي للعميل: 500." not "500.00")
- [ ] Complete named customer sale (partial payment) → verify notification shows smart-formatted amounts
- [ ] Verify PaymentPersistenceHint for anonymous sale: "بيع نقدي — يُرحَّل مباشرة ويُسجَّل في حركة المبيعات."

### Number Format — Cross-cutting
- [ ] Verify integer amounts display without `.00`: `4000` → `4,000`, `500` → `500`
- [ ] Verify fractional amounts display with decimals: `19.50` → `19.50`, `7.25` → `7.25`
- [ ] Verify thousands separator is present: `12000` → `12,000`
- [ ] Verify no parsing regression: edit a quantity or price in the cashier or admin modal, ensure value is accepted correctly

### Printed Documents
- [ ] Print an anonymous sale → verify document looks normal and professional, no "سجل دفعة في الخادم" wording
- [ ] Print a named customer sale → verify unchanged behavior

### Theme Toggle
- [ ] Switch dark ↔ light → verify all improvements remain readable in both themes

---

## EXPLICIT SCOPE STATEMENTS

- **No Sales Returns UI was implemented**
- **No Customer Payments standalone page was implemented**
- **No Print Config was implemented**
- **No auth/session settings UI was implemented** (report-only confirmation above)
- **No whole-project cleanup was attempted** — cleanup strictly limited to the 7 files touched
- **No broad redesign or theme rewrite** — surgical typography/foreground/format changes only
- **Business/accounting truth was NOT altered** — `IsOperationalOnly`, payment trace, backend DTO shape all unchanged
