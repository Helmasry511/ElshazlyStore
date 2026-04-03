namespace ElshazlyStore.Desktop.Helpers;

/// <summary>
/// Converts a decimal amount into formal Arabic words suitable for invoice use.
/// Supports Egyptian Pound (جنيه) with optional piaster (قرش) subunits.
/// </summary>
public static class ArabicAmountInWords
{
    private static readonly string[] Ones =
    [
        "", "واحد", "اثنان", "ثلاثة", "أربعة", "خمسة",
        "ستة", "سبعة", "ثمانية", "تسعة", "عشرة",
        "أحد عشر", "اثنا عشر", "ثلاثة عشر", "أربعة عشر", "خمسة عشر",
        "ستة عشر", "سبعة عشر", "ثمانية عشر", "تسعة عشر"
    ];

    private static readonly string[] Tens =
    [
        "", "عشرة", "عشرون", "ثلاثون", "أربعون", "خمسون",
        "ستون", "سبعون", "ثمانون", "تسعون"
    ];

    private static readonly string[] Hundreds =
    [
        "", "مائة", "مئتان", "ثلاثمائة", "أربعمائة", "خمسمائة",
        "ستمائة", "سبعمائة", "ثمانمائة", "تسعمائة"
    ];

    /// <summary>
    /// Converts an amount to Arabic words with formal invoice suffix.
    /// </summary>
    public static string Convert(decimal amount)
    {
        if (amount < 0m)
            amount = Math.Abs(amount);

        if (amount == 0m)
            return "صفر جنيهًا فقط لا غير";

        var wholePart = (long)Math.Truncate(amount);
        var fractionPart = (int)Math.Round((amount - wholePart) * 100m);

        var result = string.Empty;

        if (wholePart > 0)
        {
            result = ConvertWholeNumber(wholePart) + " جنيهًا";
        }

        if (fractionPart > 0 && wholePart > 0)
        {
            result += " و" + ConvertWholeNumber(fractionPart) + " قرشًا";
        }
        else if (fractionPart > 0 && wholePart == 0)
        {
            result = ConvertWholeNumber(fractionPart) + " قرشًا";
        }

        result += " فقط لا غير";

        return result;
    }

    private static string ConvertWholeNumber(long number)
    {
        if (number == 0)
            return "صفر";

        if (number < 0)
            number = Math.Abs(number);

        var parts = new List<string>();

        // Billions
        if (number >= 1_000_000_000)
        {
            var billions = number / 1_000_000_000;
            number %= 1_000_000_000;
            parts.Add(ConvertGroup(billions, "مليار", "ملياران", "مليارات"));
        }

        // Millions
        if (number >= 1_000_000)
        {
            var millions = number / 1_000_000;
            number %= 1_000_000;
            parts.Add(ConvertGroup(millions, "مليون", "مليونان", "ملايين"));
        }

        // Thousands
        if (number >= 1_000)
        {
            var thousands = number / 1_000;
            number %= 1_000;
            parts.Add(ConvertGroup(thousands, "ألف", "ألفان", "آلاف"));
        }

        // Remainder under 1000
        if (number > 0)
        {
            parts.Add(ConvertUnder1000(number));
        }

        return string.Join(" و", parts);
    }

    private static string ConvertGroup(long count, string singular, string dual, string plural)
    {
        if (count == 1)
            return singular;

        if (count == 2)
            return dual;

        if (count >= 3 && count <= 10)
            return ConvertUnder1000(count) + " " + plural;

        // 11+
        return ConvertUnder1000(count) + " " + singular;
    }

    private static string ConvertUnder1000(long number)
    {
        if (number <= 0)
            return string.Empty;

        if (number < 20)
            return Ones[number];

        if (number < 100)
        {
            var ten = number / 10;
            var one = number % 10;
            if (one == 0)
                return Tens[ten];
            return Ones[one] + " و" + Tens[ten];
        }

        // 100-999
        var hundred = number / 100;
        var remainder = number % 100;

        if (remainder == 0)
            return Hundreds[hundred];

        return Hundreds[hundred] + " و" + ConvertUnder1000(remainder);
    }
}
