using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace NoTiming.Api.Features.Account;

/// <summary>
/// What ties the details of a registration to the browser that typed them (#601). The details wait in the code
/// challenge of the address, and whoever asks for a code for an address first leaves theirs there: without more, the
/// owner of a new address could have the account they open made from a stranger's names and country, and their home
/// Tenant set from it for good. So each registration carries a secret that the browser keeps in a cookie, the challenge
/// keeps its hash, and the details are used only when the code comes back with the same secret. A code that comes back
/// without it still proves the address and opens an account, and the person fills in the rest themselves.
/// </summary>
internal static class RegistrationTokens
{
    const int TOKEN_BYTES = 32;
    const int TOKEN_LENGTH = 43;
    static readonly TimeSpan LIFETIME = TimeSpan.FromMinutes(15);
    public const string COOKIE = "__Host-NoTiming-Registration";

    public static string New()
    {
        return Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TOKEN_BYTES));
    }

    /// <summary>Whether a cookie holds a token of ours; a browser that holds anything else is given a new one.</summary>
    public static bool IsWellFormed(string? token)
    {
        return token is { Length: TOKEN_LENGTH } && token.All(x => char.IsAsciiLetterOrDigit(x) || x is '-' or '_');
    }

    /// <summary>What the challenge keeps of a token: nothing that a reader of the database could present as it.</summary>
    public static string Hash(string token)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    public static bool Matches(string? token, string? hash)
    {
        return token != null
            && hash != null
            && CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(Hash(token)),
                Encoding.UTF8.GetBytes(hash)
            );
    }

    /// <summary>
    /// Host-only, HttpOnly, Secure and SameSite=Strict: only the page of this site that registered sends it back.
    /// It lasts as long as the code does, with room to spare.
    /// </summary>
    public static CookieOptions Options()
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            MaxAge = LIFETIME,
            IsEssential = true,
        };
    }
}
