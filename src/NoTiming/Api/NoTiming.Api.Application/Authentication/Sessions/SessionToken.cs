using System.Security.Cryptography;
using System.Text;

namespace NoTiming.Api.Application.Authentication.Sessions;

public sealed class IssuedSessionToken
{
    /// <summary>The secret that goes into the session cookie. It is never stored.</summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>What the server stores and looks up: the SHA-256 of the token.</summary>
    public string TokenHash { get; init; } = string.Empty;
}

public static class SessionToken
{
    /// <summary>256 bits from the system generator, in lower case hex, so the token is safe in a cookie value.</summary>
    public static IssuedSessionToken Issue()
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        return new IssuedSessionToken { Token = token, TokenHash = HashOf(token) };
    }

    public static string HashOf(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
    }
}
