using NC.Server.Authentication.Interface;

namespace NC.Server.Authentication.Profiles;

/// <summary>The validator an app gets when it supplies none: every profile is accepted.</summary>
internal sealed class AcceptAnyProfile<TProfile> : IProfileValidator<TProfile>
{
    public bool Accepts(TProfile profile)
    {
        return true;
    }
}
