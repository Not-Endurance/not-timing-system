using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using NC.Application.HTTP;
using NC.Serialization.JSON;

namespace NC.Blazor.Client.Authentication.Services;

/// <summary>
/// The signed-in state of the browser, asked of the server. The session cookie is the only credential, so nothing about
/// the signed-in user is kept in browser storage. A 401 from the profile route is the anonymous state.
/// </summary>
public sealed class SessionAuthenticationStateProvider : AuthenticationStateProvider
{
    const string PROFILE_ENDPOINT = "me/profile";

    readonly NHttpClient _client;
    Task<AuthenticationState>? _state;

    public SessionAuthenticationStateProvider(NHttpClient client)
    {
        _client = client;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return _state ??= LoadAsync();
    }

    /// <summary>Asks the server again and tells the components that the signed-in state may have changed.</summary>
    public void Refresh()
    {
        _state = LoadAsync();
        NotifyAuthenticationStateChanged(_state);
    }

    async Task<AuthenticationState> LoadAsync()
    {
        try
        {
            var json = (await _client.GetJson(PROFILE_ENDPOINT)).Data!;
            var profile = json.FromJson<SessionProfile>();
            return new AuthenticationState(new ClaimsPrincipal(profile.ToIdentity()));
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            return Anonymous();
        }
    }

    static AuthenticationState Anonymous()
    {
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }
}

/// <summary>The identity the server returns for the signed-in user.</summary>
public sealed class SessionProfile
{
    public int Id { get; set; }

    public string? Email { get; set; }

    public string? Name { get; set; }

    public string? GivenName { get; set; }

    public string? MiddleName { get; set; }

    public string? Surname { get; set; }

    public string[] Roles { get; set; } = [];

    public ClaimsIdentity ToIdentity()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Id.ToString()),
            new(ClaimTypes.Name, Name ?? Email ?? string.Empty),
        };

        if (Email != null)
        {
            claims.Add(new Claim(ClaimTypes.Email, Email));
        }

        claims.AddRange(Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        return new ClaimsIdentity(claims, "NoTimingSession");
    }
}
