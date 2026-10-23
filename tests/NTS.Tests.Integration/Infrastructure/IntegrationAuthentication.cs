using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace NTS.Tests.Integration.Infrastructure;

internal sealed record IntegrationUser
{
    public IntegrationUser(
        string email,
        string userIdentifier,
        string name,
        string? givenName = null,
        string? middleName = null,
        string? surname = null,
        string? countryRegion = null,
        string? club = null,
        string? feiId = null,
        string? displayName = null
    )
    {
        Email = email;
        UserIdentifier = userIdentifier;
        Name = name;
        DisplayName = displayName;
        GivenName = givenName;
        MiddleName = middleName;
        Surname = surname;
        CountryRegion = countryRegion;
        Club = club;
        FeiId = feiId;
    }

    public string Email { get; }
    public string UserIdentifier { get; }
    public string Name { get; }
    public string? DisplayName { get; }
    public string? GivenName { get; }
    public string? MiddleName { get; }
    public string? Surname { get; }
    public string? CountryRegion { get; }
    public string? Club { get; }
    public string? FeiId { get; }
}

internal sealed class IntegrationAuthenticationStateProvider : AuthenticationStateProvider
{
    AuthenticationState _state;

    /// <param name="user">A null user models an anonymous visitor: no session at all.</param>
    public IntegrationAuthenticationStateProvider(IntegrationUser? user)
    {
        _state = CreateState(user);
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(_state);
    }

    /// <summary>
    /// Models the point where a sign-in round trip completes: the app has already booted as an
    /// anonymous visitor and only now learns who the user is, exactly as MSAL reports it after the
    /// login callback.
    /// </summary>
    public void SignIn(IntegrationUser user)
    {
        _state = CreateState(user);
        NotifyAuthenticationStateChanged(Task.FromResult(_state));
    }

    static AuthenticationState CreateState(IntegrationUser? user)
    {
        return new AuthenticationState(new ClaimsPrincipal(CreateIdentity(user)));
    }

    static ClaimsIdentity CreateIdentity(IntegrationUser? user)
    {
        // An identity without an authentication type is unauthenticated, which is exactly what an
        // anonymous Witness visitor looks like to the client.
        return user == null
            ? new ClaimsIdentity()
            : new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim("oid", user.UserIdentifier),
                    new Claim("name", user.DisplayName ?? user.Name),
                ],
                "IntegrationTest"
            );
    }
}
