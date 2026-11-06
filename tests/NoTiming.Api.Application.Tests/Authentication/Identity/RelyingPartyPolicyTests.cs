using NoTiming.Api.Application.Authentication.Identity;

namespace NoTiming.Api.Application.Tests.Authentication.Identity;

public sealed class RelyingPartyPolicyTests
{
    const string ProductionRelyingParty = "timing.no-company.eu";

    [Fact]
    public void Problem_WhenOriginIsSubdomainOfRelyingParty_ShouldReturnNull()
    {
        var problem = RelyingPartyPolicy.Problem(
            ProductionRelyingParty,
            "https://app.timing.no-company.eu",
            production: true
        );

        Assert.Null(problem);
    }

    [Fact]
    public void Problem_WhenOriginIsOutsideRelyingParty_ShouldReturnAProblem()
    {
        var problem = RelyingPartyPolicy.Problem(
            ProductionRelyingParty,
            "https://app.other-company.eu",
            production: true
        );

        Assert.NotNull(problem);
    }

    [Fact]
    public void Problem_WhenOriginIsTheRelyingPartyItself_ShouldReturnAProblem()
    {
        var problem = RelyingPartyPolicy.Problem(
            ProductionRelyingParty,
            "https://timing.no-company.eu",
            production: true
        );

        Assert.NotNull(problem);
    }

    [Fact]
    public void Problem_WhenProductionUsesStagingRelyingParty_ShouldReturnAProblem()
    {
        var problem = RelyingPartyPolicy.Problem(
            "staging.timing.no-company.eu",
            "https://app.staging.timing.no-company.eu",
            production: true
        );

        Assert.NotNull(problem);
    }

    [Fact]
    public void Problem_WhenProductionUsesLocalhost_ShouldReturnAProblem()
    {
        var problem = RelyingPartyPolicy.Problem("localhost", "https://app.localhost", production: true);

        Assert.NotNull(problem);
    }
}
