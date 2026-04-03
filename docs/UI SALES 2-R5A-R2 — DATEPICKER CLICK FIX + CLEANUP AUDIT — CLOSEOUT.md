# UI SALES 2-R5A-R2 — DATEPICKER CLICK FIX + SALES ADMIN HEADER DARK-MODE FIX + CLEANUP AUDIT — CLOSEOUT

**Date:** 2026-03-30
**Phase:** UI SALES 2-R5A-R2
**Status:** IMPLEMENTED — AWAITING HUMAN ACCEPTANCE TEST

---

## ROOT CAUSE — DatePicker Calendar Popup Not Opening

**Root cause:** The `ThemedDatePickerStyle` ControlTemplate (in `FormStyles.xaml`) was **missing `PART_Popup`** entirely. WPF's `DatePicker` control requires three named template parts:

| Part | Required Type | Status Before Fix |
|------|--------------|-------------------|
| `PART_TextBox` | `DatePickerTextBox` | ✅ Present |
| `PART_Button` | `Button` | ✅ Present |
| `PART_Popup` | `Popup` | ❌ **MISSING** |

Without `PART_Popup`, the `DatePicker` control has no `Popup` element to host the `Calendar`. When `PART_Button` is clicked, the control internally sets `IsDropDownOpen = true`, but with no Popup wired, nothing happens visually. The calendar icon was visible and the button was clickable, but the click produced no effect.

Additionally, the template was missing `PART_Calendar` — the named `Calendar` element that WPF's `DatePicker` binds to for date selection within the popup.

**This is a pure template-part wiring deficiency, not a behavioral or code-behind issue.**

---

## ROOT CAUSE — Sales Admin Modal Invoice Number & Status Dark-Mode

**Root cause:** Both the `FormInvoiceNumberDisplay` and `FormStatus` TextBlocks inside the Sales Admin create/edit modal used `Border` containers with `Background="{DynamicResource SidebarBackgroundBrush}"` but had **no explicit `Foreground`** set on the TextBlock. In dark mode, `SidebarBackgroundBrush` = `#181825` (near-black). TextBlock inherits the WPF default `Foreground` (Black) → black text on near-black background = invisible.

This is the same pattern already fixed in R5A-R1 for the Cashier customer and invoice mode fields.

---

## EXACT FILES CHANGED

| File | Change |
|------|--------|
| `src/ElshazlyStore.Desktop/Resources/Themes/FormStyles.xaml` | Added `PART_Popup` (Popup) and `PART_Calendar` (Calendar) to the `ThemedDatePickerStyle` ControlTemplate |
| `src/ElshazlyStore.Desktop/Views/Pages/SalesPage.xaml` | Added `Foreground="{DynamicResource PrimaryTextBrush}"` to `FormInvoiceNumberDisplay` and `FormStatus` TextBlocks |

**Two files changed — both XAML only. No C#/business-logic/backend files touched.**

---

## EXACT DATEPICKER FIX

Added the following elements inside the `ThemedDatePickerStyle` ControlTemplate Grid, after `PART_Button`:

```xml
<Popup x:Name="PART_Popup"
       Placement="Bottom"
       PlacementTarget="{Binding ElementName=Bd}"
       StaysOpen="False"
       AllowsTransparency="True">
    <Border Background="{DynamicResource InputBackgroundBrush}"
            BorderBrush="{DynamicResource InputBorderBrush}"
            BorderThickness="1"
            CornerRadius="{StaticResource SmallCornerRadius}"
            Padding="4">
        <Calendar x:Name="PART_Calendar"
                  Foreground="{DynamicResource PrimaryTextBrush}"
                  Background="{DynamicResource InputBackgroundBrush}"
                  BorderThickness="0" />
    </Border>
</Popup>
```

### Key design decisions:
- **`PART_Popup`**: Named exactly as WPF's `DatePicker` expects (`PART_Popup`). WPF internally binds `IsOpen` to `IsDropDownOpen` via OnApplyTemplate.
- **`PART_Calendar`**: Named exactly as `DatePicker` expects. WPF internally wires `SelectedDate`, `DisplayDate`, `DisplayDateStart`, `DisplayDateEnd`, and `BlackoutDates` from the `DatePicker` to this `Calendar`.
- **`PlacementTarget="{Binding ElementName=Bd}"`**: Popup anchors to the outer Border for correct positioning below the control.
- **`StaysOpen="False"`**: Popup closes automatically when clicking outside — standard dropdown behavior.
- **`AllowsTransparency="True"`**: Required for Popup to render correctly with rounded corners and no chrome.
- **Dark-mode styling**: `InputBackgroundBrush` background + `InputBorderBrush` border + `PrimaryTextBrush` foreground — consistent with the ComboBox dropdown popup styling.

### What was NOT changed:
- `PART_TextBox` — untouched, manual text entry preserved
- `PART_Button` — untouched, calendar icon and hover behavior preserved
- Border/trigger structure — untouched
- Implicit `DatePickerTextBox` style — untouched (defense-in-depth fallback remains)

---

## EXACT SALES ADMIN HEADER FIELD FIX

### Invoice Number Field (FormInvoiceNumberDisplay)

| Property | Before | After |
|----------|--------|-------|
| `Foreground` | (none — inherited WPF default Black) | `{DynamicResource PrimaryTextBrush}` (#E6ECFF dark / #4C4F69 light) |

### Status Field (FormStatus)

| Property | Before | After |
|----------|--------|-------|
| `Foreground` | (none — inherited WPF default Black) | `{DynamicResource PrimaryTextBrush}` (#E6ECFF dark / #4C4F69 light) |

Both fields are read-only display TextBlocks inside Borders with `SidebarBackgroundBrush` background. The fix adds explicit light foreground that provides readable contrast in both dark and light modes.

---

## SCREENS VERIFIED

| Screen | DatePicker Fix | Header Field Fix |
|--------|:---:|:---:|
| **Cashier (POSPage)** | ✅ `InvoiceDateUtc` DatePicker uses `ThemedDatePickerStyle` — now has `PART_Popup` + `PART_Calendar` | N/A |
| **Sales Admin (SalesPage)** | ✅ `FormInvoiceDate` DatePicker uses `ThemedDatePickerStyle` — now has `PART_Popup` + `PART_Calendar` | ✅ Invoice number + status TextBlocks have `PrimaryTextBrush` foreground |

### Verification scope:
- **Cashier DatePicker**: Calendar popup will open when calendar icon/button is clicked
- **Sales Admin DatePicker**: Calendar popup will open when calendar icon/button is clicked
- **Manual date typing**: Still works — `PART_TextBox` (DatePickerTextBox) is untouched
- **Dark-mode readability**: Not regressed — popup uses `InputBackgroundBrush` + `PrimaryTextBrush`; header fields now have explicit `PrimaryTextBrush`
- **"Show Calendar" watermark**: Still suppressed — VisualStateGroups and collapsed PART_Watermark remain intact

---

## BUILD AND TEST RESULT

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Passed!  - Failed: 0, Passed: 274, Skipped: 0, Total: 274
```

---

## Cleanup Audit

### 1. Unused code/resources detected
No dead or unused code was found in the touched files:
- `FormStyles.xaml`: All styles (`ThemedTextBoxStyle`, `ThemedComboBoxStyle`, `ComboBoxToggleButtonTemplate`, `ThemedDatePickerStyle`, implicit `DatePickerTextBox` style, implicit `ComboBoxItem` style) are actively referenced by screens.
- `SalesPage.xaml`: No unused bindings, converters, or resources detected in the modal section.

### 2. What was removed
Nothing was removed in this phase.

### 3. Why no removal was performed
All code in the touched files is actively used:
- The implicit `DatePickerTextBox` style (keyless) is intentional defense-in-depth — if any `DatePickerTextBox` ever escapes the inlined template (e.g., a new screen without `ThemedDatePickerStyle`), it still gets watermark suppression and themed foreground.
- All named styles have `StaticResource` references from page XAML files.

### 4. What was intentionally left in place
- **Implicit `DatePickerTextBox` style**: Defense-in-depth fallback. The inlined template on `PART_TextBox` takes precedence for `ThemedDatePickerStyle` users, but the implicit style catches any unthemed `DatePickerTextBox` instances.

### 5. Build/test verification after cleanup
N/A — no cleanup removals were performed. Build and tests pass with the additions only.

---

## SAFETY DECLARATIONS

- **No pricing/business logic was changed** — only XAML templates and foreground properties
- **No receipt logic was changed** — `DocumentPrintHelper.cs` untouched
- **No number formatting was changed** — no converters or format strings modified
- **No NotificationBar behavior was changed**
- **No payment flow was changed**
- **No session/auth behavior was changed**
- **No inventory screens were modified** — the shared `ThemedDatePickerStyle` fix automatically benefits any screen that uses it, but no inventory-specific files were edited

---

## CHANGE SCOPE SUMMARY

This phase modified **exactly two files**:
1. `src/ElshazlyStore.Desktop/Resources/Themes/FormStyles.xaml` — added `PART_Popup` + `PART_Calendar` to `ThemedDatePickerStyle`
2. `src/ElshazlyStore.Desktop/Views/Pages/SalesPage.xaml` — added `Foreground` to invoice number and status TextBlocks

All fixes are XAML-only. No C# code was touched. No business logic, receipt logic, number formatting, notifications, or session behavior was modified.

---

## HUMAN TEST CHECKLIST

### DatePicker — Calendar Popup
- [ ] **Cashier**: Click the calendar icon next to the date field → calendar popup opens
- [ ] **Sales Admin modal**: Click the calendar icon next to the date field → calendar popup opens
- [ ] **Select date**: Click a date in the calendar → date is selected and popup closes
- [ ] **Manual typing**: Type a date directly in the text field → date is accepted
- [ ] **No "Show Calendar"**: DatePicker does NOT show "Show Calendar" text
- [ ] **Dark mode**: Calendar popup has dark background with readable text
- [ ] **Light mode**: Calendar popup has light background with readable text

### Sales Admin Modal — Header Fields
- [ ] **Invoice number (dark mode)**: Text is light (#E6ECFF) and clearly readable on dark sidebar background
- [ ] **Status (dark mode)**: Text is light (#E6ECFF) and clearly readable on dark sidebar background
- [ ] **Invoice number (light mode)**: Text is dark (#4C4F69) and readable on light background
- [ ] **Status (light mode)**: Text is dark (#4C4F69) and readable on light background
