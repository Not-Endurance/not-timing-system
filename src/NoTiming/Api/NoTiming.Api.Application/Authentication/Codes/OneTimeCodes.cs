using System.Security.Cryptography;

namespace NoTiming.Api.Application.Authentication.Codes;

public static class OneTimeCodes
{
    /// <summary>Six digits from the system generator. Leading zeros are kept.</summary>
    public static string Generate()
    {
        return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    }
}
