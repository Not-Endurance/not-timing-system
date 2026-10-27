using NoTiming.Api.Features.Access;
using NTS.Domain.Access;

namespace NTS.Tests.Integration;

/// <summary>
/// What an endpoint says it needs (ADR-0012, #671). A requirement of the platform names no place, and a capability that is an
/// action of an Event or a Tenant cannot be declared without saying where the request names it. No host.
/// </summary>
public sealed class RequiresAttributeTests
{
    [Theory]
    [InlineData(Capability.SignedIn)]
    [InlineData(Capability.EditCountries)]
    [InlineData(Capability.SetUpAcrossTenants)]
    public void A_capability_of_the_platform_is_declared_without_a_place(Capability capability)
    {
        var requires = new RequiresAttribute(capability);

        Assert.Equal(capability, requires.Capability);
        Assert.Equal(PlaceIn.Platform, requires.Place);
    }

    [Theory]
    [InlineData(Capability.RunEvent)]
    [InlineData(Capability.ConfigureEvent)]
    [InlineData(Capability.AssignConsoleOperators)]
    public void A_capability_of_an_Event_cannot_be_declared_without_saying_where_the_request_names_it(
        Capability capability
    )
    {
        var exception = Assert.Throws<ArgumentException>(() => new RequiresAttribute(capability));

        Assert.Contains("say where the request names its place", exception.Message);
    }
}
