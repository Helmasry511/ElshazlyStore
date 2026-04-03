using ElshazlyStore.Domain.Common;

namespace ElshazlyStore.Tests.Domain;

/// <summary>
/// Verifies that error code constants are defined correctly.
/// </summary>
public sealed class ErrorCodesTests
{
    [Fact]
    public void ErrorCodes_AreNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(ErrorCodes.InternalError));
        Assert.False(string.IsNullOrWhiteSpace(ErrorCodes.ValidationFailed));
        Assert.False(string.IsNullOrWhiteSpace(ErrorCodes.NotFound));
        Assert.False(string.IsNullOrWhiteSpace(ErrorCodes.Conflict));
        Assert.False(string.IsNullOrWhiteSpace(ErrorCodes.Unauthorized));
        Assert.False(string.IsNullOrWhiteSpace(ErrorCodes.Forbidden));
    }

    [Fact]
    public void ErrorCodes_AreUpperSnakeCase()
    {
        var codes = new[]
        {
            ErrorCodes.InternalError,
            ErrorCodes.ValidationFailed,
            ErrorCodes.NotFound,
            ErrorCodes.Conflict,
            ErrorCodes.Unauthorized,
            ErrorCodes.Forbidden
        };

        foreach (var code in codes)
        {
            Assert.Matches("^[A-Z][A-Z0-9_]+$", code);
        }
    }
}
