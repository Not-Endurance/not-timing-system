namespace NC.Server.Authentication.Codes;

/// <summary>The details a registration keeps until its code is entered, or until the challenge expires.</summary>
internal sealed class RegistrationDetailsDocument
{
    public string GivenName { get; set; } = string.Empty;

    public string Surname { get; set; } = string.Empty;

    public string CountryRegion { get; set; } = string.Empty;
}
