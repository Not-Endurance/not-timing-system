using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace NTS.Tests.Integration.Infrastructure;

internal sealed record IntegrationUser
{
    public IntegrationUser(string email, string userIdentifier, string name)
    {
        Email = email;
        UserIdentifier = userIdentifier;
        Name = name;
    }

    public string Email { get; }
    public string UserIdentifier { get; }
    public string Name { get; }
}

internal sealed class IntegrationAuthenticationStateProvider : AuthenticationStateProvider
{
    readonly AuthenticationState _state;

    public IntegrationAuthenticationStateProvider(IntegrationUser user)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("oid", user.UserIdentifier),
                new Claim("name", user.Name),
            ],
            "IntegrationTest"
        );
        _state = new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(_state);
    }
}
