namespace NTS.Witness.Contracts.API;

public record RegisterUserPaload
{
    public RegisterUserPaload(
        string email,
        string? name = null,
        string? givenName = null,
        string? surname = null,
        string? countryRegion = null
    )
    {
        Email = email;
        Name = name;
        GivenName = givenName;
        Surname = surname;
        CountryRegion = countryRegion;
    }

    public string Email { get; }
    public string? Name { get; }
    public string? GivenName { get; }
    public string? Surname { get; }
    public string? CountryRegion { get; }
}
