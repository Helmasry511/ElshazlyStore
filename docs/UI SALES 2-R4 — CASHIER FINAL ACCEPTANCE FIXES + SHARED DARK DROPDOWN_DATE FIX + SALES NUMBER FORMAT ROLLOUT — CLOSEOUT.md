# UI SALES 2-R4 — CASHIER FINAL ACCEPTANCE FIXES + SHARED DARK DROPDOWN/DATE FIX + SALES NUMBER FORMAT ROLLOUT — CLOSEOUT

**Date:** 2026-03-29
**Phase:** UI SALES 2-R4
**Status:** IMPLEMENTED — AWAITING HUMAN ACCEPTANCE TEST

---

## ROOT CAUSE OF REMAINING DARK DROPDOWN/DATE ISSUE

### ComboBox (Dropdowns)

**Root cause:** Multiple pages used native WPF `ComboBox` without `Style="{StaticResource ThemedComboBoxStyle}"`. The shared `ThemedComboBoxStyle` in `FormStyles.xaml` provides a full `ControlTemplate` that uses `DynamicResource` brushes for dark-mode-aware backgrounds (`InputBackgroundBrush`), borders (`InputBorderBrush`), and text (`PrimaryTextBrush`). Pages that omitted this style fell back to the WPF system default light theme, causing unreadable white-on-white dropdowns in dark mode.

**Affected pages missing the style:**
- `StockBalancesPage.xaml` — warehouse filter ComboBox
- `StockMovementsPage.xaml` — movement type selector, single warehouse, From/To transfer warehouse ComboBoxes

**StockLedger** was already correct (used `ThemedComboBoxStyle`), which is why it looked good — confirming the shared style is the right fix.

### ComboBoxItem (Drop-down list items)

**Root cause:** The `ComboBoxItem` style in `FormStyles.xaml` used `CashierAccentBackgroundBrush` for the selected-item state — a very dark blue that blended into the dark dropdown background and made selected text invisible. Additionally, items lacked `FontFamily`, `FontSize`, and `CornerRadius` for polished appearance.

**Fix:** Changed `IsSelected` background to `NavItemActiveBrush` (visible dark gray, distinct from dropdown background) and added `AccentBrush` foreground for selected items. Added `CornerRadius="3"`, consistent font family/size, and `Margin="1"` for spacing.

### ContentPresenter (Selected text in collapsed ComboBox)

**Root cause:** The `ContentPresenter` inside the ComboBox template only set `Foreground` on its inner `TextBlock` style, without `FontWeight` or `FontSize`. This made the selected value look small and faint, especially on high-contrast dark backgrounds.

**Fix:** Added `FontWeight="SemiBold"` and `FontSize="14"` to the `TextBlock` style inside `ContentPresenter.Resources`, making the selected value clearly readable in both themes.

### DatePicker ("Show Calendar" text)

**Root cause:** WPF's `DatePickerTextBox` has a hidden `PART_Watermark` `ContentControl` that defaults to showing "Show Calendar" text when the `DatePicker` is empty or unfocused. Our custom template only provided `PART_ContentHost` (`ScrollViewer`) without explicitly defining `PART_Watermark`, so WPF fell back to the default watermark rendering.

**Fix:** Added `<ContentControl x:Name="PART_Watermark" Visibility="Collapsed" IsHitTestVisible="False" />` to the `DatePickerTextBox` template. This satisfies WPF's template contract while keeping the watermark permanently hidden — the control now displays only the actual date text or remains blank.

---

## EXACT FILES CHANGED

| File | Change |
|------|--------|
| `src/ElshazlyStore.Desktop/Resources/Themes/FormStyles.xaml` | ComboBox ContentPresenter: added `FontWeight="SemiBold"`, `FontSize="14"`. ComboBoxItem: changed selected bg to `NavItemActiveBrush`, added `AccentBrush` foreground, `CornerRadius="3"`, font props. DatePickerTextBox: added `PART_Watermark` with `Visibility="Collapsed"` |
| `src/ElshazlyStore.Desktop/Helpers/AdditionalConverters.cs` | Added `SmartNumberConverter` (IValueConverter wrapping `InvoiceNumberFormat.Format`) |
| `src/ElshazlyStore.Desktop/Resources/Themes/SharedStyles.xaml` | Registered `SmartNumberConv` converter |
| `src/ElshazlyStore.Desktop/Views/Pages/StockBalancesPage.xaml` | Added `Style="{StaticResource ThemedComboBoxStyle}"` to warehouse ComboBox |
| `src/ElshazlyStore.Desktop/Views/Pages/StockMovementsPage.xaml` | Added `Style="{StaticResource ThemedComboBoxStyle}"` to movement type, single warehouse, From/To warehouse ComboBoxes; removed `IsEditable`/`IsTextSearch*` from transfer ComboBoxes (conflicted with custom template) |
| `src/ElshazlyStore.Desktop/Views/Pages/SalesPage.xaml` | Replaced all `StringFormat=N2` with `Converter={StaticResource SmartNumberConv}` on TotalAmount, UnitPrice, DiscountAmount, LineTotal columns/displays; increased edit-modal variant display font to `14`+`SemiBold`; increased edit-modal LineTotal font to `14`; increased invoice total font to `16` |
| `src/ElshazlyStore.Desktop/Views/Controls/NotificationBar.xaml` | Increased message font from `FontSizeNormal` (13) to `14` + `FontWeight="SemiBold"` |
| `src/ElshazlyStore.Desktop/Views/Controls/NotificationBar.xaml.cs` | Fixed `Dismiss()`: replaced `Message = string.Empty` (breaks OneWay binding) with `SetCurrentValue(MessageProperty, string.Empty)` (preserves binding); added timer cleanup in `Dismiss()` |
| `src/ElshazlyStore.Desktop/ViewModels/POSViewModel.cs` | `ShowNotification`: added identity-check to force `PropertyChanged` when same message is shown consecutively |
| `src/ElshazlyStore.Desktop/ViewModels/SalesViewModel.cs` | Added `ShowNotification` helper with same identity-check; replaced all direct `NotificationMessage`/`NotificationType` assignments with `ShowNotification()` |
| `src/ElshazlyStore.Desktop/Localization/Strings.resx` | Rewrote 5 user-facing anonymous-sale strings to remove server/operational/backend-implementation phrasing |
| `src/ElshazlyStore.Desktop/Helpers/DocumentPrintHelper.cs` | Updated internal comment on anonymous print trace (no user-facing change here — the user-facing change is via the localization string) |

---

## TARGETED SCREENS COVERED

| Screen | Dropdown Fix | DatePicker Fix | Number Format | Typography | Notification |
|--------|:---:|:---:|:---:|:---:|:---:|
| **Cashier (POSPage)** | ✅ (already had ThemedComboBoxStyle, now with improved ContentPresenter) | ✅ | ✅ (already using InvoiceNumberFormat in VM) | ✅ (shared ComboBox readability) | ✅ |
| **Sales Admin (SalesPage)** | ✅ (already had ThemedComboBoxStyle) | ✅ | ✅ (all N2 → SmartNumberConv) | ✅ (variant `14`+SemiBold, LineTotal `14`+Bold, total `16`) | ✅ |
| **Stock Balances** | ✅ (added ThemedComboBoxStyle) | N/A | N/A (quantities use N0, correct) | N/A | N/A |
| **Stock Ledger** | ✅ (was already correct) | ✅ | N/A (quantities, correct) | N/A | N/A |
| **Stock Movements** | ✅ (added ThemedComboBoxStyle to 4 ComboBoxes) | N/A | N/A | N/A | N/A |

---

## DATEPICKER FIX EXPLANATION

WPF's built-in `DatePickerTextBox` has a `PART_Watermark` template part that renders a "Show Calendar" default watermark. Our custom template omitted this part, causing WPF to use its own default rendering which injected the visible "Show Calendar" text.

**Solution:** Added `PART_Watermark` as a permanently collapsed `ContentControl` in the `DatePickerTextBox` ControlTemplate. This satisfies WPF's template contract (it finds the named part it expects) while completely suppressing the watermark display. The date field now shows either the selected date text or remains visually empty — no spurious "Show Calendar" text in either theme.

---

## NUMBER FORMAT ROLLOUT SCOPE

### Rule
- Integer amounts → no decimal suffix: `4000` → `4,000`
- Fractional amounts → 2 decimal places: `19.50` → `19.50`
- Thousands separator always applied
- Exact arithmetic preserved internally (only display formatting)

### Already correct (from R3)
- Cashier: InvoiceTotalDisplay, TenderOutcomeAmountDisplay, LineTotalDisplay (via `InvoiceNumberFormat.Format()` in POSViewModel)
- Print: All print amounts (via `InvoiceNumberFormat.Format()` in DocumentPrintHelper)

### Rolled out in R4
- **Sales Admin list grid:** `TotalAmount` column (was `StringFormat=N2`)
- **Sales Admin detail modal:** `TotalAmount` display (was `StringFormat=N2`)
- **Sales Admin detail modal lines:** `UnitPrice`, `DiscountAmount`, `LineTotal` columns (were `StringFormat=N2`)
- **Sales Admin edit modal:** `LineTotal` per line (was `StringFormat=N2`), `InvoiceTotal` display (was `StringFormat=N2`)

### Implementation
Created `SmartNumberConverter` (`IValueConverter`) that delegates to `InvoiceNumberFormat.Format()`. Registered as `SmartNumberConv` in `SharedStyles.xaml`. Used in XAML bindings: `{Binding TotalAmount, Converter={StaticResource SmartNumberConv}}`.

---

## ANONYMOUS SALE WORDING CHANGE

### Removed phrases (user-facing)
All instances of these patterns were rewritten:
- "ليست سجل دفعة محفوظاً في الخادم" → removed
- "تشغيلية فقط" → removed from user-facing text
- "لا يوجد سجل دفعة محفوظ في الخادم" → removed
- "هذا المسار لا يحفظ رصيداً مستحقاً في الخادم" → removed
- "ما لم يوجد سجل دفعة محفوظ" → removed

### New professional wording

| String Key | Old (problematic) | New (professional) |
|-----------|----|----|
| `POS_PersistenceHintAnonymous` | هذا البيع يظل بدون سجل دفعة محفوظ في الخادم... | بيع نقدي — يُرحَّل مباشرة ويُسجَّل في حركة المبيعات. |
| `POS_CheckoutPostedAnonymousChangeDue` | ...لا يوجد سجل دفعة محفوظ في الخادم لهذا البيع. | تم ترحيل البيع النقدي بنجاح. الباقي للعميل: {0:N2}. |
| `POS_ModeAnonymousDescription` | هذا المسار يرحل البيع فقط. أي طريقة دفع... تشغيلياً... | بيع نقدي فوري — يُرحَّل ويُسجَّل مباشرة في حركة المبيعات. |
| `POS_PrintAnonymousOperationalNote` | بيانات السداد هنا تشغيلية فقط... ليست سجل دفعة محفوظاً في الخادم. | بيع نقدي — بيانات السداد المرفقة للتوثيق الداخلي. |
| `POS_AnonymousPaymentHint` | عند البيع بدون عميل: يتم ترحيل الفاتورة فقط، ولا يتم إنشاء سجل... | البيع النقدي يُرحَّل مباشرة عند إتمام العملية. |
| `POS_AnonymousRemainingNotAllowed` | ...لأن هذا المسار لا يحفظ رصيداً مستحقاً في الخادم. | لا يمكن إتمام بيع نقدي بمبلغ أقل من إجمالي الفاتورة. |

### Backend truth preserved
- `IsOperationalOnly` flag still exists in `SalePaymentTraceDto` and is still set in `BuildPrintTraceOverride()`
- The `DocumentPrintHelper.ResolvePaymentStateLabel()` still checks `IsOperationalOnly` internally to decide the payment label
- No backend behavior changed

---

## NOTIFICATIONBAR RELIABILITY FIX

### Root cause
`NotificationBar.Dismiss()` directly assigned `Message = string.Empty` on the `DependencyProperty`. In WPF, directly setting a `DependencyProperty` value on a control **replaces** the `OneWay` data binding from the ViewModel. After auto-dismiss:

1. Timer fires → `Dismiss()` → `Message = string.Empty` (breaks binding)
2. ViewModel later calls `NotificationMessage = "success message"` → PropertyChanged fires, but binding is severed → control never updates
3. User sees: notification shows once, then never again

### Fix (two-part)

**Part A — NotificationBar.xaml.cs:**
Changed `Dismiss()` to use `SetCurrentValue(MessageProperty, string.Empty)` instead of direct assignment. `SetCurrentValue` modifies the value **without** overriding the binding — it's the correct WPF pattern for setting DP values from within a control without breaking external bindings. Also added timer cleanup in `Dismiss()` to prevent stale timer references.

**Part B — POSViewModel.cs + SalesViewModel.cs:**
When the ViewModel shows the same notification message consecutively (e.g., two successful sales in a row → same "تم ترحيل البيع بنجاح" message), CommunityToolkit's `[ObservableProperty]` won't fire `PropertyChanged` if the value hasn't actually changed. Fix: in `ShowNotification()`, if the new message equals the current message, briefly set to empty first to force the property changed event.

### Typography improvement
`NotificationBar.xaml` message `TextBlock`: increased from `FontSizeNormal` (13px) to `14px` with `FontWeight="SemiBold"` for better readability.

---

## TYPOGRAPHY ADJUSTMENTS

### Cashier (POSPage)
- **All ComboBox selected text** (warehouse, customer, payment method, cash-settlement, wallet provider): now shows at `14px` + `SemiBold` via the shared `ContentPresenter` fix in `FormStyles.xaml`
- **Payment mode title**: already `15px` + `Bold` (unchanged, acceptable)
- **Line items**: product name already `19px` + `Bold`, barcode `15px` + `SemiBold`, line total `14px` + `Bold` (unchanged, acceptable)

### Sales Admin Modal (SalesPage)
- **Variant display per line**: increased from `FontSizeSmall` (11px) to `14px` + `SemiBold`
- **Line total per line**: increased from `FontSizeNormal` (13px) to `14px` + `Bold`, switched from `StringFormat=N2` to `SmartNumberConv`
- **Invoice total**: increased to `16px` + `Bold`
- **All dropdown selected text**: now `14px` + `SemiBold` via shared fix

---

## SESSION-TIMEOUT CONFIRMATION SUMMARY

### What currently causes logout
- **Token expiry**: JWT access token expires → API returns 401 → `HttpClient` interceptor attempts token refresh → if refresh fails → `SessionService` fires `SessionExpired` event → app redirects to login
- Server controls the token lifetime

### What does NOT currently exist
- No client-side idle timer
- No automatic desktop-side timeout based on inactivity
- No configurable timeout settings UI

### Safest later options
1. **Configurable idle timer in Settings page**: client-side `DispatcherTimer` tracking last user input, with user-configurable threshold (e.g. 15/30/60 min). Fires logout on expiry.
2. **No auto-logout, manual only**: rely on server token expiry and user clicking "تسجيل خروج". Simplest, no new code needed.
3. **Hybrid**: optional idle timer + server token expiry as backstop.

No new findings differ from the previous audit.

---

## DEFERRED ITEMS

| Item | Status | Notes |
|------|--------|-------|
| Sales Returns UI | Deferred | Not in scope for this phase |
| Customer Payments standalone page | Deferred | Not in scope |
| Print Config UI | Deferred | Not in scope |
| Attachments | Deferred | Not in scope |
| Session-timeout settings UI | Deferred | Confirmed current behavior is token-driven only |
| Whole-app flicker/animation rollout | Deferred | Only specific screens touched |
| Invoice-level discount / tax | Deferred | No backend support in this phase |

---

## BUILD / TEST / RUN RESULT

```
Solution build:   5 projects — 0 errors, 0 warnings
Desktop build:    0 errors, 0 warnings
Tests:           274 passed, 0 failed, 0 skipped
```

---

## HUMAN TEST SCRIPT

### Prerequisites
- Start API server (`run server.bat`)
- Start Desktop app (`run front.bat`)
- App starts in dark mode by default

### Test 1 — Dark Mode Dropdowns (ALL SCREENS)

1. **Cashier (نقاط البيع)**:
   - Open cashier screen
   - Click Warehouse dropdown → verify dark dropdown background, light readable text, hover highlights visible, selected item has accent color
   - Click Payment Method dropdown → same checks
   - Click Cash-Settlement dropdown → same checks
   - Verify selected text in all dropdowns is clearly readable: bold, ~14px, light color on dark background

2. **Sales Admin (المبيعات)**:
   - Open sales list
   - Click "إنشاء فاتورة جديدة"
   - In modal: click Warehouse dropdown → verify dark theme dropdown rendering
   - Verify selected text in ComboBox is readable

3. **Stock Balances (أرصدة المخزون)**:
   - Open stock balances
   - Click Warehouse filter dropdown → verify it now uses dark theme (was broken before)
   - Select a warehouse → verify selected text is readable

4. **Stock Ledger (دفتر المخزون)**:
   - Open stock ledger
   - Click Warehouse dropdown → verify dark theme (was already good — regression check)
   - Click DatePicker → verify no "Show Calendar" text visible, date field looks clean

5. **Stock Movements (حركات المخزون)**:
   - Open stock movements
   - Click Movement Type dropdown → verify dark theme (was broken before)
   - Add a line → click Warehouse dropdown → verify dark theme

### Test 2 — DatePicker (No "Show Calendar" text)

1. In **Cashier**: check Date field — should show date or be blank, NOT "Show Calendar"
2. In **Sales Admin modal**: check Date field — same
3. In **Stock Ledger**: check From/To date fields — same
4. Toggle to **light mode** → repeat checks → toggle back to dark mode

### Test 3 — Number Formatting

1. **Sales Admin list grid**: check Total column — integers like 4000 should show as "4,000" NOT "4,000.00"
2. **Sales Admin detail modal**: open a sale → check Total display — same rule
3. **Sales Admin detail lines**: check Unit Price, Discount, LineTotal columns — integers without decimals, fractions with 2 places
4. **Sales Admin edit modal**: create/edit → add a line with integer price (e.g. 500) → verify LineTotal shows "500" not "500.00"
5. **Sales Admin edit modal**: verify invoice total follows same rule
6. **Cashier**: verify existing formatting still works (InvoiceTotalDisplay already used Format())

### Test 4 — Anonymous Sale Wording

1. In **Cashier**: select no customer (anonymous mode)
   - Check "وضع السداد" section → should NOT contain "تشغيلية" or "خادم" or "سجل دفعة محفوظ"
   - Check "PaymentPersistenceHint" text → should say "بيع نقدي — يُرحَّل مباشرة ويُسجَّل في حركة المبيعات."
2. Complete an anonymous sale → check notification → should NOT mention "سجل دفعة محفوظ في الخادم"
3. Print the anonymous sale invoice → check printed text → should NOT contain "تشغيلية فقط" or "الخادم"

### Test 5 — NotificationBar Reliability

1. **Cashier**: scan a barcode → see notification → wait for auto-dismiss
2. Scan another barcode → notification MUST appear again
3. Clear basket → notification appears → scan another barcode → notification MUST appear
4. Complete a sale → notification → start new sale → scan barcode → notification MUST appear
5. Repeat 3-4 times rapidly → notification must show every time
6. **Sales Admin**: save a sale → notification → save another → notification MUST appear both times
7. Post a sale → notification → post another → notification MUST appear

### Test 6 — Typography Readability

1. **Cashier dropdowns**: selected warehouse, selected payment method, selected cash-settlement → text should be ~14px, semi-bold, clearly readable
2. **Sales Admin modal**: product variant display per line → should be ~14px, semi-bold
3. **Sales Admin modal**: line total → should be ~14px, bold
4. **Sales Admin modal**: invoice total → should be ~16px, bold
5. **NotificationBar** (any screen): message text → should be ~14px, semi-bold

### Test 7 — Light Mode Regression

1. Toggle to light mode
2. Repeat Test 1 (dropdowns) → verify all still look correct in light mode
3. Repeat Test 2 (DatePicker) → verify clean date display
4. Toggle back to dark mode

### Test 8 — No Regressions

1. Verify all pages still load without errors
2. Verify print still works for sales, purchases, returns
3. Verify login/logout works normally
4. Verify session expiry behavior unchanged (no new idle timer)

---

**STOP.** Awaiting agent report + human test result.
