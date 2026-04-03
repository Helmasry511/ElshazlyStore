using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ElshazlyStore.Desktop.Helpers;

/// <summary>
/// Reusable numeric parsing and TextBox behavior for decimal entry.
/// Supports Arabic digits and optional auto-scaling for integer-only money entry.
/// </summary>
public static class NumericInputFoundation
{
    public static bool TryParseDecimal(
        string? text,
        int fractionDigits,
        bool autoScaleIntegerInput,
        out decimal value)
    {
        value = 0m;

        if (string.IsNullOrWhiteSpace(text))
            return true;

        var normalized = NormalizeDigits(text).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return true;

        var isNegative = false;
        if (normalized.StartsWith('-'))
        {
            isNegative = true;
            normalized = normalized[1..];
        }
        else if (normalized.StartsWith('+'))
        {
            normalized = normalized[1..];
        }

        if (normalized.Length == 0)
            return false;

        var lastDot = normalized.LastIndexOf('.');
        var lastComma = normalized.LastIndexOf(',');
        var separatorIndex = Math.Max(lastDot, lastComma);

        decimal parsed;
        if (separatorIndex < 0)
        {
            if (!IsDigitsOnly(normalized)
                || !decimal.TryParse(normalized, NumberStyles.None, CultureInfo.InvariantCulture, out parsed))
            {
                return false;
            }

            parsed = autoScaleIntegerInput && fractionDigits > 0
                ? parsed / Pow10(fractionDigits)
                : parsed;
        }
        else
        {
            var integerPart = RemoveSeparators(normalized[..separatorIndex]);
            var fractionPart = RemoveSeparators(normalized[(separatorIndex + 1)..]);

            if (integerPart.Length == 0)
                integerPart = "0";

            if (!IsDigitsOnly(integerPart) || (fractionPart.Length > 0 && !IsDigitsOnly(fractionPart)))
                return false;

            var invariant = fractionPart.Length == 0
                ? integerPart
                : $"{integerPart}.{fractionPart}";

            if (!decimal.TryParse(invariant, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out parsed))
                return false;
        }

        if (isNegative)
            parsed = -parsed;

        var safeDigits = Math.Clamp(fractionDigits, 0, 6);
        value = decimal.Round(parsed, safeDigits, MidpointRounding.AwayFromZero);
        return true;
    }

    public static string FormatDecimal(decimal value, int fractionDigits)
    {
        var safeDigits = Math.Clamp(fractionDigits, 0, 6);
        // Smart format: integer values get no decimal suffix (same rule as InvoiceNumberFormat).
        if (value == Math.Truncate(value))
            return value.ToString("N0", CultureInfo.CurrentCulture);
        return value.ToString($"N{safeDigits}", CultureInfo.CurrentCulture);
    }

    public static string NormalizeDigits(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var sb = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            sb.Append(ch switch
            {
                >= '\u0660' and <= '\u0669' => (char)('0' + (ch - '\u0660')), // Arabic-Indic
                >= '\u06F0' and <= '\u06F9' => (char)('0' + (ch - '\u06F0')), // Eastern Arabic-Indic
                '\u066B' => '.', // Arabic decimal separator
                '\u066C' => ',', // Arabic thousands separator
                _ => ch
            });
        }

        return sb.ToString();
    }

    private static bool IsDigitsOnly(string value)
        => value.All(char.IsDigit);

    private static string RemoveSeparators(string value)
        => value.Replace(".", string.Empty).Replace(",", string.Empty);

    private static decimal Pow10(int exponent)
    {
        var safeExponent = Math.Clamp(exponent, 0, 6);
        decimal result = 1m;
        for (var i = 0; i < safeExponent; i++)
            result *= 10m;

        return result;
    }
}

public static class NumericTextBoxBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(NumericTextBoxBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty FractionDigitsProperty = DependencyProperty.RegisterAttached(
        "FractionDigits",
        typeof(int),
        typeof(NumericTextBoxBehavior),
        new PropertyMetadata(2));

    public static readonly DependencyProperty AutoScaleIntegerInputProperty = DependencyProperty.RegisterAttached(
        "AutoScaleIntegerInput",
        typeof(bool),
        typeof(NumericTextBoxBehavior),
        new PropertyMetadata(false));

    public static readonly DependencyProperty AllowNegativeProperty = DependencyProperty.RegisterAttached(
        "AllowNegative",
        typeof(bool),
        typeof(NumericTextBoxBehavior),
        new PropertyMetadata(false));

    private static readonly DependencyProperty IsHookedProperty = DependencyProperty.RegisterAttached(
        "IsHooked",
        typeof(bool),
        typeof(NumericTextBoxBehavior),
        new PropertyMetadata(false));

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetFractionDigits(DependencyObject element, int value) => element.SetValue(FractionDigitsProperty, value);
    public static int GetFractionDigits(DependencyObject element) => (int)element.GetValue(FractionDigitsProperty);

    public static void SetAutoScaleIntegerInput(DependencyObject element, bool value) => element.SetValue(AutoScaleIntegerInputProperty, value);
    public static bool GetAutoScaleIntegerInput(DependencyObject element) => (bool)element.GetValue(AutoScaleIntegerInputProperty);

    public static void SetAllowNegative(DependencyObject element, bool value) => element.SetValue(AllowNegativeProperty, value);
    public static bool GetAllowNegative(DependencyObject element) => (bool)element.GetValue(AllowNegativeProperty);

    private static void SetIsHooked(DependencyObject element, bool value) => element.SetValue(IsHookedProperty, value);
    private static bool GetIsHooked(DependencyObject element) => (bool)element.GetValue(IsHookedProperty);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox)
            return;

        var shouldEnable = (bool)e.NewValue;
        if (shouldEnable)
            Hook(textBox);
        else
            Unhook(textBox);
    }

    private static void Hook(TextBox textBox)
    {
        if (GetIsHooked(textBox))
            return;

        textBox.PreviewTextInput += OnPreviewTextInput;
        textBox.PreviewKeyDown += OnPreviewKeyDown;
        textBox.LostFocus += OnLostFocus;
        textBox.Loaded += OnLoaded;
        DataObject.AddPastingHandler(textBox, OnPaste);
        SetIsHooked(textBox, true);
    }

    private static void Unhook(TextBox textBox)
    {
        if (!GetIsHooked(textBox))
            return;

        textBox.PreviewTextInput -= OnPreviewTextInput;
        textBox.PreviewKeyDown -= OnPreviewKeyDown;
        textBox.LostFocus -= OnLostFocus;
        textBox.Loaded -= OnLoaded;
        DataObject.RemovePastingHandler(textBox, OnPaste);
        SetIsHooked(textBox, false);
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        if (!IsAllowedTextInput(textBox, e.Text))
            e.Handled = true;
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        if (e.Key == Key.Space)
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            CommitAndFormat(textBox);
        }
    }

    private static void OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
            CommitAndFormat(textBox);
    }

    /// <summary>
    /// Formats the TextBox text on initial load to strip locale-specific decimal separators
    /// and database-scale trailing zeros, without updating the binding source.
    /// Uses InvariantCulture F-format for autoScale fields to preserve the decimal separator,
    /// preventing accidental autoScale interpretation on the next LostFocus.
    /// </summary>
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
            // Integer value: for autoScale fields, keep the decimal separator so that
            // the subsequent LostFocus TryParseDecimal never triggers autoScale on an
            // unchanged reload value. For non-autoScale fields, show a clean integer.
            formatted = autoScaleIntegerInput
                ? value.ToString($"F{safeDigits}", CultureInfo.InvariantCulture)  // e.g. "6500.00" — decimal point kept to prevent autoScale on next LostFocus
                : value.ToString("0", CultureInfo.InvariantCulture);  // e.g. "1", "500" — plain integer, no thousands separator
        }
        else
        {
            formatted = value.ToString($"F{safeDigits}", CultureInfo.InvariantCulture);  // e.g. "6500.50"
        }

        if (textBox.Text != formatted)
            textBox.Text = formatted;
        // No UpdateSource call: source decimal value is already correct from the binding.
    }

    private static void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        if (!e.SourceDataObject.GetDataPresent(DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        var pasted = e.SourceDataObject.GetData(DataFormats.Text) as string;
        if (string.IsNullOrWhiteSpace(pasted) || !IsAllowedTextInput(textBox, pasted))
        {
            e.CancelCommand();
        }
    }

    private static bool IsAllowedTextInput(TextBox textBox, string input)
    {
        var allowNegative = GetAllowNegative(textBox);

        foreach (var ch in input)
        {
            if (char.IsDigit(ch))
                continue;

            if (ch is '.' or ',' or '\u066B' or '\u066C')
                continue;

            if ((ch == '-' || ch == '+') && allowNegative)
                continue;

            return false;
        }

        return true;
    }

    private static void CommitAndFormat(TextBox textBox)
    {
        var fractionDigits = GetFractionDigits(textBox);
        var autoScaleIntegerInput = GetAutoScaleIntegerInput(textBox);
        var allowNegative = GetAllowNegative(textBox);

        if (!NumericInputFoundation.TryParseDecimal(textBox.Text, fractionDigits, autoScaleIntegerInput, out var value))
            return;

        if (!allowNegative && value < 0)
            value = 0m;

        textBox.Text = NumericInputFoundation.FormatDecimal(value, fractionDigits);

        var expression = BindingOperations.GetBindingExpression(textBox, TextBox.TextProperty);
        expression?.UpdateSource();
    }
}
