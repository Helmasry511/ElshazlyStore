# UI SALES 2-R5A-R1 — CASHIER REMAINING SHARED CONTROL FIX (CUSTOMER + INVOICE MODE + DATEPICKER) — CLOSEOUT

**Date:** 2026-03-30
**Phase:** UI SALES 2-R5A-R1
**Status:** IMPLEMENTED — AWAITING HUMAN ACCEPTANCE TEST

---

## REMAINING ROOT CAUSES

### 1. Cashier Customer Field — Dark Mode Unreadable

**Root cause:** The `SelectedCustomerDisplay` TextBlock inside the selected-customer Border (`Background="{DynamicResource SidebarBackgroundBrush}"`) had no explicit `Foreground` set. In dark mode, `SidebarBackgroundBrush` = `#181825` (near-black). TextBlock inherits the WPF default Foreground (Black) → black text on near-black background = invisible.

### 2. Cashier Invoice Mode Field — Dark Mode Unreadable + Alignment

**Root cause:** The `SaleModeIndicator` TextBlock inside the mode indicator Border (`Background="{DynamicResource SidebarBackgroundBrush}"`) had no explicit `Foreground` set. Same dark-on-dark problem. Additionally, no `TextAlignment` was specified — text floated to the natural start position without centering, creating a visually unbalanced/LTR-ish appearance inside the small field.

### 3. DatePicker — "Show Calendar" Watermark Risk

**Root cause:** The inlined `DatePickerTextBox` ControlTemplate (inside `ThemedDatePickerStyle`) and the implicit `DatePickerTextBox` fallback style both defined `PART_Watermark` as `Visibility="Collapsed"`, but did NOT define `VisualStateGroups` for `WatermarkStates` or `FocusStates`. WPF's `DatePickerTextBox` class calls `VisualStateManager.GoToState("Watermarked")` when the text is empty. Without explicit empty VisualState definitions in the template, the GoToState call becomes implementation-dependent — if WPF ever resolves states from a parent or fallback template, the watermark ("Show Calendar") could reappear. By explicitly defining all expected VisualStates as empty (no-op), GoToState transitions are guaranteed to do nothing, permanently suppressing the watermark.

---

## EXACT FILES CHANGED

| File | Change |
|------|--------|
| `src/ElshazlyStore.Desktop/Views/Pages/POSPage.xaml` | **Customer TextBlock:** added `Foreground="{DynamicResource PrimaryTextBrush}"`. **SaleModeIndicator TextBlock:** added `Foreground="{DynamicResource PrimaryTextBrush}"` + `TextAlignment="Center"`. |
| `src/ElshazlyStore.Desktop/Resources/Themes/FormStyles.xaml` | **ThemedDatePickerStyle PART_TextBox inline template:** added `VisualStateManager.VisualStateGroups` with empty `WatermarkStates` (`Unwatermarked`, `Watermarked`) and `FocusStates` (`Focused`, `Unfocused`). **Implicit DatePickerTextBox style template:** same VisualStateGroups added. |

**Two files changed — both XAML only. No C#/business-logic/backend files touched.**

---

## EXACT CUSTOMER FIELD FIX

| Property | Before | After |
|----------|--------|-------|
| `Foreground` | (none — inherited WPF default Black) | `{DynamicResource PrimaryTextBrush}` (#E6ECFF dark / #4C4F69 light) |

The TextBlock showing `SelectedCustomerDisplay` inside the selected-customer Border (`SidebarBackgroundBrush` background) now has explicit light foreground in dark mode.

---

## EXACT INVOICE MODE FIELD FIX

| Property | Before | After |
|----------|--------|-------|
| `Foreground` | (none — inherited WPF default Black) | `{DynamicResource PrimaryTextBrush}` (#E6ECFF dark / #4C4F69 light) |
| `TextAlignment` | (none — default Start, which floats text to one side) | `Center` — text centered within the mode indicator field |

The TextBlock showing `SaleModeIndicator` inside the mode indicator Border (`SidebarBackgroundBrush` background) now has explicit light foreground in dark mode and centered text alignment.

**Note:** Only the header-area SaleModeIndicator (Grid.Column="3") was affected. The checkout-panel SaleModeIndicator already had `Foreground="{DynamicResource SecondaryTextBrush}"` and was not changed.

---

## EXACT DATEPICKER FIX

### Inlined DatePickerTextBox template (inside ThemedDatePickerStyle)

Added `VisualStateManager.VisualStateGroups` to the Grid root:

```xml
<VisualStateManager.VisualStateGroups>
    <VisualStateGroup x:Name="WatermarkStates">
        <VisualState x:Name="Unwatermarked" />
        <VisualState x:Name="Watermarked" />
    </VisualStateGroup>
    <VisualStateGroup x:Name="FocusStates">
        <VisualState x:Name="Focused" />
        <VisualState x:Name="Unfocused" />
    </VisualStateGroup>
</VisualStateManager.VisualStateGroups>
```

**Effect:** When WPF calls `VisualStateManager.GoToState("Watermarked")`, the state transitions to an explicitly empty state — no Storyboard, no property changes. The `PART_Watermark` remains `Visibility="Collapsed"` permanently. "Show Calendar" text can never appear.

### Implicit DatePickerTextBox style (fallback)

Same VisualStateGroups added for defense-in-depth. If any DatePickerTextBox ever falls back to the implicit style, it gets the same watermark suppression.

---

## SCREENS VERIFIED

| Screen | Customer Fix | Invoice Mode Fix | DatePicker Fix |
|--------|:---:|:---:|:---:|
| **Cashier (POSPage)** | ✅ `SelectedCustomerDisplay` has `PrimaryTextBrush` foreground | ✅ `SaleModeIndicator` has `PrimaryTextBrush` foreground + `Center` alignment | ✅ `InvoiceDateUtc` DatePicker uses `ThemedDatePickerStyle` with VisualStateGroups |
| **Sales Admin (SalesPage)** | N/A (customer field uses different structure) | N/A (no mode indicator) | ✅ `FormInvoiceDate` DatePicker uses `ThemedDatePickerStyle` with VisualStateGroups |

---

## BUILD AND TEST RESULT

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Passed!  - Failed: 0, Passed: 274, Skipped: 0, Total: 274
```

---

## HUMAN TEST CHECKLIST

### Cashier — Customer Field
- [ ] **Dark mode:** Select a customer → customer name is clearly readable (light text on dark sidebar background)
- [ ] **Light mode:** Customer name is clearly readable (dark text on light sidebar background)
- [ ] **RTL:** Customer name flows right-to-left correctly

### Cashier — Invoice Mode Field
- [ ] **Dark mode:** Sale mode indicator text is clearly readable (light text on dark sidebar background)
- [ ] **Light mode:** Sale mode indicator text is clearly readable
- [ ] **Centering:** Mode text is horizontally centered within the field
- [ ] **RTL:** Arabic mode text renders correctly in RTL direction

### Cashier — DatePicker
- [ ] **No "Show Calendar":** DatePicker does NOT show "Show Calendar" text when empty or unfocused
- [ ] **Dark mode:** Date text is light and readable on dark background
- [ ] **Calendar icon:** Calendar icon visible and clickable
- [ ] **Calendar opens:** Clicking icon or field opens calendar popup

### Sales Admin — DatePicker (create/edit modal)
- [ ] **No "Show Calendar":** DatePicker does NOT show "Show Calendar" text
- [ ] **Dark mode:** Date text is readable
- [ ] **Calendar opens:** Calendar popup works correctly

---

## SAFETY DECLARATIONS

- **No receipt logic was changed** — `DocumentPrintHelper.cs` untouched
- **No number formatting was changed** — `InvoiceNumberFormat`, `SmartNumberConverter` untouched
- **No notification behavior was changed** — `NotificationBar` untouched
- **No session/auth logic was changed** — `ThemeService.cs`, `UserPreferencesService.cs`, auth services untouched
- **No pricing/discount logic was changed** — ViewModels untouched
- **No backend/API code was changed** — only WPF XAML presentation layer
- **No inventory screens were directly modified** — only shared `FormStyles.xaml` (DatePickerTextBox VisualStateGroups), which is a no-op addition that cannot regress existing behavior

---

## CHANGE SCOPE SUMMARY

This phase modified **exactly two files**:
1. `src/ElshazlyStore.Desktop/Views/Pages/POSPage.xaml` — two TextBlock property additions (Foreground, TextAlignment)
2. `src/ElshazlyStore.Desktop/Resources/Themes/FormStyles.xaml` — VisualStateGroups added to two DatePickerTextBox templates

All changes are additive property/state definitions. No existing properties were modified or removed. No C# code was changed.
