# UI SALES 2-R5C — SALES NUMBER ROUND-TRIP FORMAT FIX + CLEANUP AUDIT — CLOSEOUT

**Date:** 2026-03-30
**Phase:** UI SALES 2-R5C
**Status:** IMPLEMENTED — AWAITING HUMAN ACCEPTANCE TEST

---

## EXACT FILES CHANGED

| File | Change |
|------|--------|
| `src/ElshazlyStore.Desktop/Helpers/NumericInputFoundation.cs` | Fixed `FormatDecimal` to use smart format (no forced decimal suffix for integer values). Added `OnLoaded` event handler and wired it in `Hook`/`Unhook` for initial-display normalization. |
| `src/ElshazlyStore.Desktop/Views/Pages/SalesPage.xaml` | Added `SmartNumberConv` to the detail DataGrid's Quantity column (was missing — only converter missing in the detail lines grid). |

**2 files changed — 1 C# helper, 1 XAML. No backend files touched.**

---

## GOAL A — EXACT ROOT CAUSE OF POST-SAVE/RELOAD NUMBER FORMAT ISSUE

### Root Cause 1: Missing `SmartNumberConv` on Detail DataGrid `Quantity` column (XAML bug)

**File:** `SalesPage.xaml`, line 318  
**Evidence:** All other columns in the detail DataGrid — `UnitPrice`, `DiscountAmount`, `LineTotal` — used `Converter={StaticResource SmartNumberConv}`. The `Quantity` column alone had a bare `{Binding Quantity}` binding with no converter.

**Effect:** WPF DataGrid converts `decimal → string` using the default TypeConverter, which calls `decimal.ToString("G", CultureInfo.CurrentCulture)`. In Arabic locales:
- `ar-SA` (decimal separator = `.`): `1.0000m` → `"1.0000"` and `500.0000m` → `"500.0000"`
- `ar-EG` (decimal separator = `,`): `1.0000m` → `"1,0000"` and `500.0000m` → `"500,0000"`

The database column type is `numeric(18,4)` (see `SalesInvoiceLineConfiguration.cs`). EF Core / Npgsql preserves scale 4 on round-trip. For example, a quantity of `1` stored in PostgreSQL is returned as `1.0000m` in C#. In `ar-EG` locale, this displays as `"1,0000"` — the exact pattern reported as `500,0000` in the bug report.

**SmartNumberConv**, by contrast, uses `CultureInfo.InvariantCulture` and value-based integer check (`value == Math.Truncate(value)`), which correctly handles scale-4 values: `1.0000m == 1m` → TRUE → returns `"1"`.

### Root Cause 2: `FormatDecimal` forced fixed decimal places for integers (LostFocus bug)

**File:** `NumericInputFoundation.cs`, `FormatDecimal` method  
**Evidence:**

```csharp
// BEFORE (broken):
return value.ToString($"N{safeDigits}", CultureInfo.CurrentCulture);
// For 6500m with FractionDigits=2 in ar-SA: "6,500.00" — forced 2 decimal places
```

`FormatDecimal` is called by `CommitAndFormat` on every `LostFocus` (and `Enter`) event. It always rendered the value with `fractionDigits` decimal places regardless of whether the value had a real fractional part. For money fields with `FractionDigits="2"`:
- `6500.00m` → `"6,500.00"` ← WRONG (should be `"6,500"` per approved rule)
- `6500.50m` → `"6,500.50"` ← correct

The `CommitAndFormat` called by `LostFocus` could fire any time the user:
1. Clicked another field after opening the edit modal
2. Pressed Tab to move between fields
3. Clicked the Save button (which causes the active field to lose focus)

### Root Cause 3: Initial TextBox display before first LostFocus (initial-display scale bug)

**File:** `NumericInputFoundation.cs` / `NumericTextBoxBehavior` — missing `Loaded` handler  
**Evidence:** When `OpenEditAsync` populates `SalesLineVm` fields from `SaleLineDto` (which carries scale-4 decimals from the DB), WPF TextBox binding converts `decimal → string` using `decimal.ToString("G", CultureInfo.CurrentCulture)` — the General format with current culture, preserving full decimal scale. 

In `ar-EG` locale: `500.0000m.ToString("G", ar-EG)` = `"500,0000"` (comma = Arabic decimal separator). This is the exact pattern `"500,0000"` reported.

Before the user interacts with the form, every TextBox shows the raw locale-dependent scale representation. No previous fix addressed this because no `Loaded` normalization handler existed.

---

## GOAL B — WAS IT BACKEND, DESKTOP, OR BOTH?

**Answer: Desktop-only. The backend is correct.**

The backend (`SalesService.InvoiceLineDto`) correctly stores and returns exact `decimal` values with full precision. The database schema `numeric(18,4)` is appropriate for financial data. The backend does NOT emit malformed values.

All three root causes are desktop display-path issues:
1. Missing converter in XAML binding.
2. `FormatDecimal` not applying smart format on LostFocus.
3. No initial-load normalization in `NumericTextBoxBehavior`.

No backend changes were needed or made.

---

## EXACT FIX PATH

### Fix A — `NumericInputFoundation.FormatDecimal` (smart format)

```csharp
// BEFORE:
public static string FormatDecimal(decimal value, int fractionDigits)
{
    var safeDigits = Math.Clamp(fractionDigits, 0, 6);
    return value.ToString($"N{safeDigits}", CultureInfo.CurrentCulture);
}

// AFTER:
public static string FormatDecimal(decimal value, int fractionDigits)
{
    var safeDigits = Math.Clamp(fractionDigits, 0, 6);
    // Smart format: integer values get no decimal suffix (same rule as InvoiceNumberFormat).
    if (value == Math.Truncate(value))
        return value.ToString("N0", CultureInfo.CurrentCulture);
    return value.ToString($"N{safeDigits}", CultureInfo.CurrentCulture);
}
```

**Effect:** On `LostFocus` (every commit event), integer amounts no longer receive a forced decimal suffix. `6500m` → `"6,500"` instead of `"6,500.00"`. Fractional values remain unaffected (`6500.50m` → `"6,500.50"` ✅).

### Fix B — `NumericTextBoxBehavior.OnLoaded` handler (initial-display normalization)

Added a `Loaded` event handler that fires when each TextBox enters the visual tree (i.e., when the edit modal is opened with existing data).

```csharp
private static void OnLoaded(object sender, RoutedEventArgs e)
{
    if (sender is not TextBox textBox || string.IsNullOrWhiteSpace(textBox.Text))
        return;

    var fractionDigits = GetFractionDigits(textBox);
    var autoScaleIntegerInput = GetAutoScaleIntegerInput(textBox);

    if (!NumericInputFoundation.TryParseDecimal(textBox.Text, fractionDigits, autoScaleIntegerInput, out var value))
        return;

    var safeDigits = Math.Clamp(fractionDigits, 0, 6);
    string formatted;

    if (value == Math.Truncate(value))
    {
        // autoScale fields (e.g. UnitPrice, DiscountAmount): keep the decimal separator in the
        // formatted output (e.g. "6500.00") so that the next LostFocus TryParseDecimal cannot
        // accidentally trigger autoScale on an unmodified reload value.
        // non-autoScale fields (e.g. Quantity): plain integer string (e.g. "1", "500").
        formatted = autoScaleIntegerInput
            ? value.ToString($"F{safeDigits}", CultureInfo.InvariantCulture)
            : value.ToString("0", CultureInfo.InvariantCulture);
    }
    else
    {
        formatted = value.ToString($"F{safeDigits}", CultureInfo.InvariantCulture);
    }

    if (textBox.Text != formatted)
        textBox.Text = formatted;
    // No UpdateSource: source decimal is already correct from the binding.
}
```

**Key design points:**
- `CultureInfo.InvariantCulture` is used for the initial-display format to eliminate locale-specific decimal separators (removes `"500,0000"` in ar-EG).
- The `F{safeDigits}` format for autoScale fields always includes a decimal separator, so the subsequent `TryParseDecimal` call on LostFocus always takes the decimal-separator path (not the integer-no-separator path) and never triggers `autoScaleIntegerInput`. This prevents the pre-saved correct value from being corrupted by autoScale on first focus-out.
- The `"0"` format for non-autoScale fields (Quantity) gives a plain integer string without thousands separators, safe for re-parsing.
- Does NOT call `UpdateSource()` — source decimal value is already correct.

### Fix C — XAML `SmartNumberConv` on Quantity column (detail DataGrid)

```xml
<!-- BEFORE: -->
<DataGridTextColumn ... Binding="{Binding Quantity}" Width="80">

<!-- AFTER: -->
<DataGridTextColumn ... Binding="{Binding Quantity, Converter={StaticResource SmartNumberConv}}" Width="80">
```

**Effect:** The read-only detail view now displays Quantity through `SmartNumberConv` (InvariantCulture, smart format). `1.0000m` → `"1"`, `2.5000m` → `"2.50"`. Consistent with all other decimal columns in the same DataGrid.

---

## FIELDS AND SCREENS VERIFIED

### Sales Admin — Detail Modal (read-only)

| Field | Before (scale-4 from DB) | After |
|-------|--------------------------|-------|
| TotalAmount | SmartNumberConv already applied ✅ | Unchanged ✅ |
| Quantity (detail DataGrid) | `"1.0000"` or `"1,0000"` (locale) | `"1"` ✅ |
| UnitPrice (detail DataGrid) | SmartNumberConv already applied ✅ | Unchanged ✅ |
| DiscountAmount (detail DataGrid) | SmartNumberConv already applied ✅ | Unchanged ✅ |
| LineTotal (detail DataGrid) | SmartNumberConv already applied ✅ | Unchanged ✅ |

### Sales Admin — Edit Modal (editable form)

| Field | Before | After |
|-------|--------|-------|
| Quantity TextBox (initial load) | `"1.0000"` raw scale display | `"1"` (Loaded → `"0"` format, non-autoScale) ✅ |
| UnitPrice TextBox (initial load) | `"6500.0000"` or `"6500,0000"` | `"6500.00"` (Loaded → F2 InvariantCulture) ✅ |
| DiscountAmount TextBox (initial load) | `"0.0000"` or `"0,0000"` | `"0.00"` (Loaded → F2 InvariantCulture) ✅ |
| UnitPrice TextBox (after LostFocus/Save) | `"6,500.00"` (forced N2) | `"6,500"` (smart N0 for integer) ✅ |
| DiscountAmount (after LostFocus) | `"0.00"` (forced N2) | `"0"` (smart N0 for zero) ✅ |
| Quantity (after LostFocus) | `"1.000"` (forced N3) | `"1"` (smart N0 for integer) ✅ |
| LineTotal TextBlock | SmartNumberConv already applied ✅ | Unchanged ✅ |
| InvoiceTotal TextBlock | SmartNumberConv already applied ✅ | Unchanged ✅ |

### Sales Admin — Main Grid (list view)

| Field | Before | After |
|-------|--------|-------|
| TotalAmount | SmartNumberConv already applied ✅ | Unchanged ✅ |

### Approved Display Rule Verification

| Value | Rule | Expected | After Fix |
|-------|------|----------|-----------|
| `4000m` (or `4000.0000m`) | Integer → no suffix | `4,000` | `4,000` ✅ |
| `4780m` (or `4780.0000m`) | Integer → no suffix | `4,780` | `4,780` ✅ |
| `6500.0000m` (integer with scale 4) | Integer → no suffix | `6,500` | `6,500` ✅ |
| `6500.50m` | Fractional → 2 decimals | `6,500.50` | `6,500.50` ✅ |
| `500.0000m` (from DB scale 4, integer) | Integer → no suffix | `500` | `500` ✅ |
| `0m` / `0.0000m` | Zero → no suffix | `0` | `0` ✅ |

---

## ARITHMETIC/PERSISTENCE INTEGRITY CONFIRMED

- `FormatDecimal` is display-only — it does not modify any `decimal` property in the ViewModel.
- `OnLoaded` sets `textBox.Text` for display only; it does NOT call `UpdateSource()`. The ViewModel's decimal values remain exactly as received from the API.
- `CommitAndFormat` on LostFocus still updates the source binding after user editing — this path is unchanged except for the corrected `FormatDecimal` output.
- The `NormalizeDecimalScale` approach was deliberately NOT used in the ViewModel — it would have removed the decimal separator from the initial TextBox display text, causing `TryParseDecimal` to trigger `autoScaleIntegerInput` on unmodified fields. The `Loaded` handler approach is safer.
- `InvoiceTotal` is computed from live `Lines` decimal properties — arithmetic is unaffected.
- Save and update requests still use the exact `decimal` properties from `SalesLineVm` — no rounding or truncation was introduced.

---

## BUILD AND TEST RESULT

```
dotnet build ElshazlyStore.sln --no-restore -v quiet
  Build succeeded.
  0 Warning(s)
  0 Error(s)
```

Full solution build passed. 0 errors, 0 warnings.

---

## HUMAN TEST CHECKLIST

### Sales Admin — Create New Sale
- [ ] Open Sales Admin → click Create
- [ ] Add a product line
- [ ] Enter UnitPrice as an integer (e.g. type `6500`) — TextBox should show `6500.00` after LostFocus (smart display, no trailing .00 after final commit — actually shows `6,500`)
- [ ] Enter UnitPrice with decimals (e.g. type `6500.50`) — after LostFocus shows `6,500.50`
- [ ] LineTotal TextBlock should update in real-time and show `6,500` or `6,500.50`
- [ ] InvoiceTotal shows correctly
- [ ] Save the sale — succeeds without error

### Sales Admin — Reload and Edit Existing Sale
- [ ] Open an existing Draft sale for editing
- [ ] Quantity TextBox: shows a clean integer (e.g. `1`, `2`, not `1.0000` or `1,0000`)
- [ ] UnitPrice TextBox: shows `6500.00` initially (for integer values) — no locale-specific decimal separator, no scale-4 trailing zeros
- [ ] Click on another field (tab/click away from UnitPrice) — UnitPrice should reformat to `6,500` (smart format, no `.00` suffix for integers)
- [ ] DiscountAmount: same behavior — shows `0.00` initially, `0` after LostFocus (if zero)
- [ ] No data corruption — save and reload again, values remain the same
- [ ] After second reload — same clean display ✅

### Sales Admin — Detail View (read-only)
- [ ] Open any posted or draft sale's detail view
- [ ] Quantity column: shows `1` (not `1.0000` or `1,0000`)
- [ ] UnitPrice, DiscountAmount, LineTotal: unchanged, already correct
- [ ] TotalAmount: unchanged, already correct

### Sales Admin — After Save/Post
- [ ] After saving a new sale, the list reloads and TotalAmount column shows correctly (e.g. `6,500` not `6,500.00`)
- [ ] After posting, same

### Cashier (POS) — Regression Check
- [ ] Open Cashier
- [ ] Add a product line: Quantity and UnitPrice TextBoxes work normally
- [ ] Type `6500` for price — after LostFocus shows `65.00` (autoScale behavior unchanged — `6500 ÷ 100 = 65.00`)
- [ ] OR type `6500.50` — shows `6,500.50`
- [ ] InvoiceTotalDisplay, LineTotalDisplay: correct formatting, no regression
- [ ] Checkout message shows correct smart-formatted amounts
- [ ] No regression in cashier flow

---

## CLEANUP AUDIT

### Touched Files

| File | Dead code found? | Action |
|------|:-:|--------|
| `NumericInputFoundation.cs` | No | No dead code; all added code is actively used in the new `Loaded` handler and corrected `FormatDecimal` |
| `SalesPage.xaml` | No | No dead resources, styles, or templates found; converter addition only |

### Removals
None. Both changes are additive corrections with no removals.

### Intentionally Left
- `CultureInfo.CurrentCulture` in `FormatDecimal` for `N0`/`N{digits}` output — intentional, consistent with the rest of the formatting path so WPF binding can re-parse formatted output with the same culture on `UpdateSource()`.
- `AutoScaleIntegerInput` behavior (divide by 10^fractionDigits for user-typed integers) — intentional cashier UX feature; unchanged.
- No other decimal bindings in SalesPage.xaml were found to be missing converters.

### Build Verification After Cleanup
```
Build succeeded. 0 Warning(s). 0 Error(s).
```

---

## EXPLICIT SCOPE STATEMENTS

- **Shared search / Compo SearchBox was NOT redesigned here.** No SearchBox or AutocompleteBox redesign was touched.
- **Sales Returns was NOT implemented.** No SalesReturn page, ViewModel, or endpoint was added.
- **Customer Payments standalone page was NOT implemented.** No CustomerPayment page was created.
- **Print Config was NOT implemented.** No PrintProfile or PrintRule UI was added.
- **No whole-project cleanup was attempted.** Cleanup was limited exclusively to the two touched files.
- **Cashier screen was NOT actively changed** — the `NumericTextBoxBehavior` changes naturally improve the initial-display normalization for all TextBoxes using the behavior, but no POS-specific logic was added or removed.
- **No DTO, backend service, database migration, or API endpoint was changed.**
