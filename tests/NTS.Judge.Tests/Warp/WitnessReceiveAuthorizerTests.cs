using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Not.Server.Authentication;
using NTS.Nexus.Warp.Features.Witness.Authorization;

namespace NTS.Judge.Tests.Warp;

public class WitnessReceiveAuthorizerTests
{
    [Fact]
    public async Task Authorize_allows_official_for_connected_event()
    {
        var officialAccess = new TestOfficialAccessService(isOfficial: true);
        var authorizer = CreateAuthorizer(officialAccess);

        await authorizer.Authorize(CreatePrincipal("official@example.com", "nts-client-scope"), "17", "17");

        Assert.Equal("official@example.com", officialAccess.Email);
        Assert.Equal(17, officialAccess.EventId);
    }

    [Fact]
    public async Task Authorize_rejects_non_official_user()
    {
        var authorizer = CreateAuthorizer(new TestOfficialAccessService(isOfficial: false));

        var exception = await Assert.ThrowsAsync<HubException>(() =>
            authorizer.Authorize(CreatePrincipal("participant@example.com", "nts-client-scope"), "17", "17")
        );

        Assert.Contains("Only officials", exception.Message);
    }

    [Fact]
    public async Task Authorize_rejects_when_connected_and_requested_event_do_not_match()
    {
        var authorizer = CreateAuthorizer(new TestOfficialAccessService(isOfficial: true));

        var exception = await Assert.ThrowsAsync<HubException>(() =>
            authorizer.Authorize(CreatePrincipal("official@example.com", "nts-client-scope"), "17", "18")
        );

        Assert.Contains("currently connected event", exception.Message);
    }

    [Fact]
    public async Task Authorize_rejects_authenticated_user_without_email_claim()
    {
        var authorizer = CreateAuthorizer(new TestOfficialAccessService(isOfficial: true));

        var exception = await Assert.ThrowsAsync<HubException>(() =>
            authorizer.Authorize(
                new ClaimsPrincipal(
                    new ClaimsIdentity([new Claim("scp", "nts-client-scope")], authenticationType: "TestAuth")
                ),
                "17",
                "17"
            )
        );

        Assert.Contains("email", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Authorize_rejects_unauthenticated_user()
    {
        var authorizer = CreateAuthorizer(new TestOfficialAccessService(isOfficial: true));

        var exception = await Assert.ThrowsAsync<HubException>(() =>
            authorizer.Authorize(new ClaimsPrincipal(new ClaimsIdentity()), "17", "17")
        );

        Assert.Contains("Authentication is required", exception.Message);
    }

    [Fact]
    public async Task Authorize_rejects_user_without_required_scope()
    {
        var authorizer = CreateAuthorizer(new TestOfficialAccessService(isOfficial: true));

        var exception = await Assert.ThrowsAsync<HubException>(() =>
            authorizer.Authorize(CreatePrincipal("official@example.com", "different-scope"), "17", "17")
        );

        Assert.Contains("required Warp access scope", exception.Message);
    }

    [Fact]
    public async Task Authorize_allows_scope_from_long_form_entra_claim_type()
    {
        var officialAccess = new TestOfficialAccessService(isOfficial: true);
        var authorizer = CreateAuthorizer(officialAccess);

        await authorizer.Authorize(
            CreatePrincipal(
                "official@example.com",
                "nts-client-scope",
                "http://schemas.microsoft.com/identity/claims/scope"
            ),
            "17",
            "17"
        );

        Assert.Equal("official@example.com", officialAccess.Email);
        Assert.Equal(17, officialAccess.EventId);
    }

    static WitnessReceiveAuthorizer CreateAuthorizer(TestOfficialAccessService officialAccess)
    {
        var options = Options.Create(new NServerAuthenticationSettings { Scope = "nts-client-scope" });
        return new WitnessReceiveAuthorizer(officialAccess, options);
    }

    static ClaimsPrincipal CreatePrincipal(
        string email,
        string scope,
        string scopeClaimType = "scp"
    )
    {
        return new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.Email, email), new Claim(scopeClaimType, scope)],
                authenticationType: "TestAuth"
            )
        );
    }

    sealed class TestOfficialAccessService : IReceiveSnapshotAccessPolicy
    {
        readonly bool _isOfficial;

        public TestOfficialAccessService(bool isOfficial)
        {
            _isOfficial = isOfficial;
        }

        public string? Email { get; private set; }
        public int EventId { get; private set; }

        public Task<bool> IsOfficial(string email, int eventId)
        {
            Email = email;
            EventId = eventId;
            return Task.FromResult(_isOfficial);
        }
    }
}
