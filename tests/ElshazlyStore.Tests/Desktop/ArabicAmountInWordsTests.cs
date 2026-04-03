using ElshazlyStore.Desktop.Helpers;

namespace ElshazlyStore.Tests.Desktop;

public class ArabicAmountInWordsTests
{
    [Fact]
    public void Zero_Returns_Arabic_Zero()
    {
        Assert.Equal("صفر جنيهًا فقط لا غير", ArabicAmountInWords.Convert(0m));
    }

    [Fact]
    public void One_Returns_Correct()
    {
        Assert.Contains("واحد", ArabicAmountInWords.Convert(1m));
        Assert.Contains("جنيهًا", ArabicAmountInWords.Convert(1m));
        Assert.Contains("فقط لا غير", ArabicAmountInWords.Convert(1m));
    }

    [Fact]
    public void OneThousand()
    {
        var result = ArabicAmountInWords.Convert(1000m);
        Assert.Contains("ألف", result);
        Assert.Contains("جنيهًا", result);
    }

    [Fact]
    public void FourThousand()
    {
        var result = ArabicAmountInWords.Convert(4000m);
        Assert.Contains("أربعة", result);
        Assert.Contains("آلاف", result);
    }

    [Fact]
    public void FourThousandSevenHundredEighty()
    {
        var result = ArabicAmountInWords.Convert(4780m);
        Assert.Contains("أربعة", result);
        Assert.Contains("آلاف", result);
        Assert.Contains("سبعمائة", result);
        Assert.Contains("ثمانون", result);
    }

    [Fact]
    public void FractionalPart_Shows_Piasters()
    {
        var result = ArabicAmountInWords.Convert(100.50m);
        Assert.Contains("مائة", result);
        Assert.Contains("قرشًا", result);
        Assert.Contains("خمسون", result);
    }

    [Fact]
    public void Large_Number_Million()
    {
        var result = ArabicAmountInWords.Convert(1_000_000m);
        Assert.Contains("مليون", result);
    }

    [Fact]
    public void Two_Thousand()
    {
        var result = ArabicAmountInWords.Convert(2000m);
        Assert.Contains("ألفان", result);
    }

    [Fact]
    public void Suffix_Always_Present()
    {
        var result = ArabicAmountInWords.Convert(500m);
        Assert.EndsWith("فقط لا غير", result);
    }
}
