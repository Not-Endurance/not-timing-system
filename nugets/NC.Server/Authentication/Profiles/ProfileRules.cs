using NC.Server.Authentication.Interface;

namespace NC.Server.Authentication.Profiles;

/// <summary>
/// The app's rules for a profile: every validator the app registered must accept it. An app that registers none accepts
/// any profile. The module adds no validator of its own, so registering the app's validator is never ordering-sensitive.
/// </summary>
internal sealed class ProfileRules<TProfile>
{
    readonly IReadOnlyList<IProfileValidator<TProfile>> _validators;

    public ProfileRules(IEnumerable<IProfileValidator<TProfile>> validators)
    {
        _validators = [.. validators];
    }

    public bool Accepts(TProfile profile)
    {
        return _validators.All(validator => validator.Accepts(profile));
    }
}
