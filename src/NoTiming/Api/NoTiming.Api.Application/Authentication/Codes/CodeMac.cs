using System.Security.Cryptography;
using System.Text;

namespace NoTiming.Api.Application.Authentication.Codes;

/// <summary>
/// The stored form of a code: HMAC-SHA256 over the normalized address and the code, keyed with the server secret. The
/// code itself is never stored.
/// </summary>
public static class CodeMac
{
    public static byte[] Compute(ReadOnlySpan<byte> secret, string email, string code)
    {
        var message = Encoding.UTF8.GetBytes($"{email}\n{code}");
        return HMACSHA256.HashData(secret, message);
    }
}
