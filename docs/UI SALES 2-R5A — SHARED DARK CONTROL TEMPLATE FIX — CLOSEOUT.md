# UI SALES 2-R5A — SHARED DARK CONTROL TEMPLATE FIX (COMBOBOX + DATEPICKER + SELECTED CONTENT ALIGNMENT) — CLOSEOUT

**Date:** 2026-03-29
**Phase:** UI SALES 2-R5A
**Status:** IMPLEMENTED — AWAITING HUMAN ACCEPTANCE TEST

---

## SHARED STYLE / TEMPLATE ROOT CAUSE

### ComboBox — Selected Content Off-Center / LTR-ish

**Root cause:** The `ContentPresenter` inside `ThemedComboBoxStyle` (in `FormStyles.xaml`) used `HorizontalAlignment="Right"`. All affected pages set `FlowDirection="RightToLeft"` at the root. WPF mirrors `HorizontalAlignment` in RTL: `Right` becomes visually Left. This caused the selected text to cluster on the LEFT side of the ComboBox — right next to the dropdown arrow — making it look LTR-ish, off-center, and visually wrong for Arabic layout.

Additionally, `Margin="{TemplateBinding Padding}"` applied symmetric padding (10px both sides), leaving insufficient clearance on the arrow side (20px arrow column vs 10px margin).

The TextBlock inside `ContentPresenter.Resources` lacked `TextAlignment`, `HorizontalAlignment`, and `TextTrimming` — so text never centered within the content area.

### ComboBoxItem — Selected Foreground Not Propagated

**Root cause:** The `ContentPresenter` inside the `ComboBoxItem` template used `ContentPresenter.Resources` with a hardcoded `TextBlock` Style setting `Foreground` to `PrimaryTextBrush`. This implicit style (property precedence level 6) overrode the inherited `Foreground` from the `ComboBoxItem`'s `IsSelected` trigger (property inheritance, level 11). Result: when an item was selected in the dropdown, the foreground stayed `PrimaryTextBrush` instead of changing to `AccentBrush`.

### DatePicker — "Show Calendar" Text Visible

**Root cause:** The implicit `DatePickerTextBox` style (no `x:Key`, defined in `FormStyles.xaml`) was NOT reliably applied to the `PART_TextBox` inside the `ThemedDatePickerStyle` ControlTemplate. WPF's style resolution for controls inside ControlTemplates can fail to pick up implicit styles from merged ResourceDictionaries — a known .NET WPF behavior. This caused `PART_TextBox` to fall back to the WPF default `DatePickerTextBox` template, which includes a visible `PART_Watermark` element showing "Show Calendar".

Additionally, `PART_Button` (the dropdown button) had no Content and no custom template — rendering as an invisible, unstyled button with no calendar icon.

---

## EXACT FILES CHANGED

| File | Change |
|------|--------|
| `src/ElshazlyStore.Desktop/Resources/Themes/FormStyles.xaml` | **ComboBox ContentPresenter:** `HorizontalAlignment` changed from `Right` → `Stretch`; `Margin` changed from `{TemplateBinding Padding}` → `10,8,28,8` (28px clearance on arrow side, mirrors correctly in RTL); TextBlock style gains `TextAlignment="Center"`, `HorizontalAlignment="Stretch"`, `TextTrimming="CharacterEllipsis"`. **DatePicker PART_TextBox:** inlined `DatePickerTextBox.Template` directly on the element (PART_ContentHost + PART_Watermark collapsed); added `FontFamily`, `FontSize="14"`, `FontWeight="SemiBold"` for typography consistency with ComboBox. **DatePicker PART_Button:** added custom `Button.Template` with calendar icon Path geometry (`M 2 1 V 3 M 10 1 V 3 M 0 4 H 12 M 0 2 H 12 V 12 H 0 Z`), hover opacity trigger, `Cursor="Hand"`. **ComboBoxItem ContentPresenter:** replaced `ContentPresenter.Resources` TextBlock Foreground hack with `TextElement.Foreground="{DynamicResource PrimaryTextBrush}"` on the ContentPresenter, and added `TextElement.Foreground` setter on `IsSelected` trigger targeting the Border so the AccentBrush flows through to TextBlock children. |

**Single file changed:** `FormStyles.xaml` — all fixes are in the shared style/template layer.

---

## EXACT COMBOBOX FIX DETAILS

### ContentPresenter (selected text in closed ComboBox)

| Property | Before | After |
|----------|--------|-------|
| `HorizontalAlignment` | `Right` (mirrored to Left in RTL → wrong) | `Stretch` (fills content area, correct in both RTL and LTR) |
| `Margin` | `{TemplateBinding Padding}` = `10,8` symmetric | `10,8,28,8` — 28px on the trailing edge clears the 20px arrow + 8px gap; mirrors correctly in RTL |
| TextBlock `TextAlignment` | (none) | `Center` — text centered within available width |
| TextBlock `HorizontalAlignment` | (none) | `Stretch` — TextBlock fills ContentPresenter width for centering to work |
| TextBlock `TextTrimming` | (none) | `CharacterEllipsis` — prevents overflow into arrow area |
| TextBlock `Foreground` | `PrimaryTextBrush` ✓ | (unchanged) |
| TextBlock `FontWeight` | `SemiBold` ✓ | (unchanged) |
| TextBlock `FontSize` | `14` ✓ | (unchanged) |

### ComboBoxItem (dropdown list items)

| Property | Before | After |
|----------|--------|-------|
| ContentPresenter Foreground | `ContentPresenter.Resources` TextBlock Style with hardcoded `PrimaryTextBrush` (blocks trigger inheritance) | `TextElement.Foreground="{DynamicResource PrimaryTextBrush}"` on ContentPresenter (inheritable, responds to triggers) |
| IsSelected trigger | Sets `Foreground` on ComboBoxItem only (doesn't reach TextBlock due to Resources style override) | Additionally sets `TextElement.Foreground` on Bd Border → properly propagates AccentBrush to TextBlock children |

### ToggleButton (arrow/chrome) — unchanged
Arrow and background remain correct. RTL mirroring places the arrow on the left side visually, which is standard for RTL dropdowns.

### Dropdown Popup — unchanged
Background `InputBackgroundBrush` (#1A2333 dark / #FFFFFF light) + item foreground `PrimaryTextBrush` (#E6ECFF dark / #4C4F69 light) — readable contrast in both themes.

---

## EXACT DATEPICKER FIX DETAILS

### PART_TextBox (DatePickerTextBox)

| Property | Before | After |
|----------|--------|-------|
| Template | Relied on implicit `DatePickerTextBox` style (unreliable inside ControlTemplate) | **Inlined** `DatePickerTextBox.Template` directly on element — 100% guaranteed to apply |
| PART_Watermark | Present in implicit style only | Present in inlined template: `Visibility="Collapsed"`, `IsHitTestVisible="False"` — permanently suppresses "Show Calendar" |
| PART_ContentHost | Present in implicit style only | Present in inlined template: `ScrollViewer` with hidden scrollbars, transparent background |
| FontFamily | (inherited, could be incorrect) | Explicit `{StaticResource PrimaryFont}` (Segoe UI) |
| FontSize | (inherited, could be 13) | Explicit `14` — matches ComboBox selected text |
| FontWeight | (inherited, normal) | Explicit `SemiBold` — matches ComboBox selected text |

### PART_Button (calendar dropdown button)

| Property | Before | After |
|----------|--------|-------|
| Content | (none — invisible button) | Calendar icon via inline `Button.Template` with `Path` geometry |
| Template | WPF default Button chrome | Custom: transparent `Border` → `Path` calendar icon, `Opacity=0.7` on hover |
| Cursor | (default) | `Hand` |
| Icon | (none) | `M 2 1 V 3 M 10 1 V 3 M 0 4 H 12 M 0 2 H 12 V 12 H 0 Z` — simple calendar shape, stroked with `SecondaryTextBrush` |

### Implicit DatePickerTextBox style — kept as fallback
The keyless `Style TargetType="DatePickerTextBox"` remains in `FormStyles.xaml` as a defense-in-depth fallback. The inlined template on PART_TextBox takes precedence and is the primary fix.

---

## SCREENS VERIFIED

| Screen | ComboBox Fix | DatePicker Fix | RTL Correct | Typography |
|--------|:---:|:---:|:---:|:---:|
| **Cashier (POSPage)** | ✅ Warehouse, CashCheckoutMode, PaymentMethod, WalletProvider — all use `ThemedComboBoxStyle` | ✅ InvoiceDate uses `ThemedDatePickerStyle` | ✅ Page root `FlowDirection="RightToLeft"` | ✅ Centered, SemiBold, 14px |
| **Sales Admin (SalesPage)** | ✅ Warehouse in edit modal uses `ThemedComboBoxStyle` | ✅ FormInvoiceDate uses `ThemedDatePickerStyle` | ✅ Page root `FlowDirection="RightToLeft"` | ✅ Centered, SemiBold, 14px |
| **Stock Movements** | ✅ MovementType, Warehouse, FromWarehouse, ToWarehouse — all use `ThemedComboBoxStyle` | N/A (no DatePicker on page) | ✅ Page root `FlowDirection="RightToLeft"` | ✅ Centered, SemiBold, 14px |
| **Stock Balances** | ✅ Warehouse filter uses `ThemedComboBoxStyle` | N/A (no DatePicker on page) | ✅ Page root `FlowDirection="RightToLeft"` | ✅ Centered, SemiBold, 14px |

All ComboBoxes across all affected screens use `Style="{StaticResource ThemedComboBoxStyle}"` — no unstyled ComboBoxes found.
All DatePickers use `Style="{StaticResource ThemedDatePickerStyle}"` — no raw DatePickers found.

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

### ComboBox — Closed State (selected text)
- [ ] **Dark mode:** Selected text in all ComboBoxes is light (#E6ECFF) and clearly readable on dark input background (#1A2333)
- [ ] **Light mode:** Selected text is dark (#4C4F69) and readable on white input background
- [ ] **Centering:** Selected text is horizontally centered in all ComboBoxes across all screens
- [ ] **RTL:** Arabic text flows right-to-left correctly within the centered layout
- [ ] **Bold:** Selected text is SemiBold weight (noticeably stronger than regular text)
- [ ] **Size:** Selected text is 14px (comfortable, not oversized)
- [ ] **Arrow clearance:** Text does not overlap or crowd the dropdown arrow

### ComboBox — Dropdown List (open popup)
- [ ] **Dark mode:** Dropdown has dark background (#1A2333) with light text (#E6ECFF)
- [ ] **Hover:** Hovered items have distinct background (#313244)
- [ ] **Selected:** Selected item shows AccentBrush foreground (#89B4FA) on NavItemActive background (#45475A)
- [ ] **Readability:** All items clearly readable in both themes

### DatePicker
- [ ] **No "Show Calendar":** DatePicker does NOT show "Show Calendar" text when empty or unfocused
- [ ] **Calendar icon:** Small calendar icon visible in the dropdown button area
- [ ] **Dark mode:** Date text is light and readable on dark background
- [ ] **Light mode:** Date text is dark and readable on light background
- [ ] **Typography:** Date text is 14px SemiBold, matching ComboBox selected text

### Per-Screen Verification
- [ ] **Cashier:** Warehouse → centered, readable, RTL-correct
- [ ] **Cashier:** Customer area → unaffected, still works
- [ ] **Cashier:** Cash settlement dropdown → centered, readable
- [ ] **Cashier:** Payment method → centered, readable
- [ ] **Cashier:** Wallet provider → centered, readable
- [ ] **Cashier:** Invoice date (DatePicker) → no "Show Calendar", readable
- [ ] **Sales Admin modal:** Date → no "Show Calendar", readable
- [ ] **Sales Admin modal:** Warehouse → centered, readable
- [ ] **Stock Movements:** Movement type → centered, readable
- [ ] **Stock Movements:** Warehouse(s) → centered, readable
- [ ] **Stock Balances:** Warehouse filter → centered, readable

---

## SAFETY DECLARATIONS

- **No pricing logic was changed** — no files outside `FormStyles.xaml` were modified
- **No receipt logic was changed** — `DocumentPrintHelper.cs` untouched
- **No session/auth logic was changed** — `ThemeService.cs`, `UserPreferencesService.cs`, auth services untouched
- **No Sales Returns UI was implemented**
- **No Customer Payments standalone page was implemented**
- **No Print Config was implemented**
- **No business logic of any kind was changed** — only XAML control templates in a single shared style file

---

## CHANGE SCOPE SUMMARY

This phase modified **exactly one file**: `src/ElshazlyStore.Desktop/Resources/Themes/FormStyles.xaml`.

All fixes are in the shared style/template layer:
1. `ThemedComboBoxStyle` → ContentPresenter alignment/centering
2. `ComboBoxItem` implicit style → foreground inheritance for selected state
3. `ThemedDatePickerStyle` → inlined DatePickerTextBox template + calendar icon button

No page-specific hacks. No per-screen overrides. The fixes propagate automatically to every screen that references `ThemedComboBoxStyle` and `ThemedDatePickerStyle`.
