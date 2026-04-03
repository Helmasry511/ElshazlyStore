using ElshazlyStore.Desktop.Helpers;

namespace ElshazlyStore.Tests.Desktop;

public class InvoiceNumberFormatTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(4000, "4,000")]
    [InlineData(4780, "4,780")]
    [InlineData(1234567, "1,234,567")]
    [InlineData(19.50, "19.50")]
    [InlineData(100.99, "100.99")]
    [InlineData(0.50, "0.50")]
    [InlineData(1000000, "1,000,000")]
    public void Format_Produces_Expected_Output(decimal input, string expected)
    {
        Assert.Equal(expected, InvoiceNumberFormat.Format(input));
    }

    [Fact]
    public void Format_Integer_Has_No_Decimals()
    {
        var result = InvoiceNumberFormat.Format(5000m);
        Assert.DoesNotContain(".", result);
    }

    [Fact]
    public void Format_Fractional_Has_Two_Decimals()
    {
        var result = InvoiceNumberFormat.Format(5000.5m);
        Assert.Contains(".", result);
        Assert.EndsWith("50", result);
    }

    [Fact]
    public void FormatOrDash_Null_Returns_Dash()
    {
        Assert.Equal("—", InvoiceNumberFormat.FormatOrDash(null));
    }

    [Fact]
    public void FormatOrDash_Value_Returns_Formatted()
    {
        Assert.Equal("1,000", InvoiceNumberFormat.FormatOrDash(1000m));
    }
}
