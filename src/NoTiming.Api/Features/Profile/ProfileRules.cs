using Not.Identity;

namespace NoTiming.Api.Features.Profile;

/// <summary>What the profile of an account has to hold, said once for every route that tells it.</summary>
internal static class ProfileRules
{
    /// <summary>The rule of the profile screen: a first name, a surname and a country that the profile names.</summary>
    public static bool IsComplete(NIdentityUser user)
    {
        return user.TextOf("GivenName") != null
            && user.TextOf("Surname") != null
            && user.TextOf("CountryRegion") != null;
    }
}
