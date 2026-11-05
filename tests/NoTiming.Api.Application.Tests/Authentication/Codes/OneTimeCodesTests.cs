using NoTiming.Api.Application.Authentication.Codes;

namespace NoTiming.Api.Application.Tests.Authentication.Codes;

public sealed class OneTimeCodesTests
{
    [Fact]
    public void Generate_WhenCalled_ShouldReturnSixDigits()
    {
        var code = OneTimeCodes.Generate();

        Assert.Matches("^[0-9]{6}$", code);
    }
}
