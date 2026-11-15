namespace NC.Server.Authentication.SignIn;

/// <summary>The details a new user registers with. The module requires the two names; the app checks its profile.</summary>
public sealed class RegistrationDetails<TProfile>
    where TProfile : class, new()
{
    public string GivenName { get; init; } = string.Empty;

    public string Surname { get; init; } = string.Empty;

    public TProfile Profile { get; init; } = new();
}
