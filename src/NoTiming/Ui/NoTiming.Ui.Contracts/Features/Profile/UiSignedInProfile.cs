using NC.Application.Authentication.User;
using NoTiming.Application.Contracts.Account;

namespace NoTiming.Ui.Contracts.Features.Profile;

/// <summary>The signed-in person: the identity the session gives, and the NoTiming profile fields.</summary>
public sealed record UiSignedInProfile
{
    public UiSignedInProfile(NUserModel user, NoTimingProfile profile)
    {
        User = user;
        Profile = profile;
    }

    public NUserModel User { get; }

    public NoTimingProfile Profile { get; }
}
