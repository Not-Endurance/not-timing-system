namespace NC.Server.Authentication.Account;

/// <summary>A change to the signed-in user's profile. The module requires the two names; the app checks the rest of the profile.</summary>
public sealed class ProfileUpdateModel<TProfile>
    where TProfile : class, new()
{
    public string GivenName { get; init; } = string.Empty;

    public string? MiddleName { get; init; }

    public string Surname { get; init; } = string.Empty;

    public TProfile Profile { get; init; } = new();
}
