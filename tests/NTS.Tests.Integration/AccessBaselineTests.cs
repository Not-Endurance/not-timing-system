using NoTiming.Api.Features.Access;

namespace NTS.Tests.Integration;

/// <summary>
/// The decision at the front of every request (#602, ADR-0012, the rest-api skill), as a pure function of what is
/// known before any endpoint runs: the method, whether the one list of public endpoints names the endpoint, whether
/// someone is signed in, and the header a browser cannot send to another origin without being allowed. The tests need
/// no host and no database: they compose the rules and say what each combination does.
/// </summary>
public sealed class AccessBaselineTests
{
    const string HEADER_VALUE = "NoTiming";

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public void Nobody_who_is_not_signed_in_reaches_a_protected_endpoint_whatever_the_method_and_the_header(
        string method
    )
    {
        Assert.Equal(AccessDecision.NotSignedIn, Decide(method, "protected", signedIn: false, header: null));
        Assert.Equal(AccessDecision.NotSignedIn, Decide(method, "protected", signedIn: false, header: HEADER_VALUE));
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public void A_signed_in_caller_reads_a_protected_endpoint_without_the_header(string method)
    {
        Assert.Equal(AccessDecision.Allow, Decide(method, "protected", signedIn: true, header: null));
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    [InlineData("post")]
    [InlineData("Patch")]
    public void A_signed_in_caller_changes_state_through_a_protected_endpoint_only_with_the_header(string method)
    {
        Assert.Equal(AccessDecision.WriteHeaderRequired, Decide(method, "protected", signedIn: true, header: null));
        Assert.Equal(AccessDecision.WriteHeaderRequired, Decide(method, "protected", signedIn: true, header: ""));
        Assert.Equal(
            AccessDecision.WriteHeaderRequired,
            Decide(method, "protected", signedIn: true, header: "XMLHttpRequest")
        );
        Assert.Equal(AccessDecision.Allow, Decide(method, "protected", signedIn: true, header: HEADER_VALUE));
    }

    [Theory]
    [InlineData("public-read")]
    [InlineData("sign-in")]
    public void A_public_endpoint_is_open_to_everybody_with_or_without_the_header_and_whatever_the_method(string access)
    {
        foreach (var method in new[] { "GET", "HEAD", "POST", "PATCH", "PUT", "DELETE" })
        {
            Assert.Equal(AccessDecision.Allow, Decide(method, access, signedIn: false, header: null));
            Assert.Equal(AccessDecision.Allow, Decide(method, access, signedIn: true, header: null));
        }
    }

    [Fact]
    public void An_endpoint_the_list_does_not_name_is_protected()
    {
        Assert.Equal(EndpointAccess.Protected, PublicEndpoints.AccessOf("GET", "api/events/{id}"));
        Assert.Equal(EndpointAccess.Protected, PublicEndpoints.AccessOf("POST", "api/me/profile"));
        Assert.Equal(EndpointAccess.Protected, PublicEndpoints.AccessOf("GET", null));
    }

    [Theory]
    [InlineData("GET", "healthz", "public-read")]
    [InlineData("GET", "/healthz", "public-read")]
    [InlineData("POST", "healthz", "protected")] // the list says which methods are public, not only which paths
    [InlineData("POST", "api/sessions", "sign-in")]
    [InlineData("GET", "api/sessions", "protected")]
    [InlineData("DELETE", "api/sessions/current", "sign-in")]
    [InlineData("get", "sign-in", "sign-in")]
    public void The_list_names_a_method_and_a_path_together(string method, string pattern, string expected)
    {
        Assert.Equal(Parse(expected), PublicEndpoints.AccessOf(method, pattern));
    }

    [Fact]
    public void The_hub_and_its_negotiation_are_public_whatever_the_method_as_the_hub_has_no_method_a_client_can_call()
    {
        foreach (var method in new[] { "GET", "POST", "DELETE" })
        {
            Assert.Equal(EndpointAccess.PublicRead, PublicEndpoints.AccessOf(method, "live-hub"));
            Assert.Equal(EndpointAccess.PublicRead, PublicEndpoints.AccessOf(method, "live-hub/negotiate"));
        }
    }

    static AccessDecision Decide(string method, string access, bool signedIn, string? header)
    {
        return AccessBaseline.Decide(method, Parse(access), signedIn, header);
    }

    static EndpointAccess Parse(string access)
    {
        return access switch
        {
            "public-read" => EndpointAccess.PublicRead,
            "sign-in" => EndpointAccess.SignIn,
            _ => EndpointAccess.Protected,
        };
    }
}
