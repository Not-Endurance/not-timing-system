namespace NC.Server.Authentication.SignIn;

public sealed class RegistrationDetails
{
    public string GivenName { get; init; } = string.Empty;

    public string Surname { get; init; } = string.Empty;

    public string CountryRegion { get; init; } = string.Empty;
}
