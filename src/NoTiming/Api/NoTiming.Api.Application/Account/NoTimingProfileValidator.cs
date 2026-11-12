using NC.Server.Authentication.Interface;
using NoTiming.Application.Contracts.Account;

namespace NoTiming.Api.Application.Account;

/// <summary>NoTiming's rule for a profile: a person has a country. The club and the federation id are optional.</summary>
public sealed class NoTimingProfileValidator : IProfileValidator<NoTimingProfile>
{
    public bool Accepts(NoTimingProfile profile)
    {
        return !string.IsNullOrWhiteSpace(profile.CountryIsoCode);
    }
}
