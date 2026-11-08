using System.Security.Cryptography;
using System.Text;

namespace NC.Server.Authentication.Sessions;

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
