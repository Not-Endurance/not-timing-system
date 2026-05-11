using NTS.Application.Contracts;

namespace NTS.Tests.Unit.Application;

public class ClientEnvironmentDisplayTests
{
    [Theory]
    [InlineData(null, "NTS Judge v1.2.0 - Prod")]
    [InlineData("", "NTS Judge v1.2.0 - Prod")]
    [InlineData("Production", "NTS Judge v1.2.0 - Prod")]
    [InlineData("production", "NTS Judge v1.2.0 - Prod")]
    [InlineData("Staging", "NTS Judge v1.2.0 [Staging] - Staging")]
    [InlineData("Development", "NTS Judge v1.2.0 [Development] - Development")]
    public void FormatTitle_FormatsEnvironmentIndicator(string? environment, string expected)
    {
        var result = NtsClientDisplayFormatter.FormatTitle(ApplicationConstants.Apps.JUDGE, environment);

        Assert.Equal(expected, result);
    }
}
