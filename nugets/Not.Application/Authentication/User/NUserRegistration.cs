namespace Not.Application.Authentication.User;

public record NUserRegistration
{
    public NUserRegistration(
        string email,
        string? name = null,
        string? givenName = null,
        string? surname = null,
        string? countryRegion = null,
        string? middleName = null,
        string? club = null,
        string? feiId = null
    )
    {
        Email = email;
        Name = name;
        GivenName = givenName;
        Surname = surname;
        CountryRegion = countryRegion;
        MiddleName = middleName;
        Club = club;
        FeiId = feiId;
    }

    public string Email { get; }
    public string? Name { get; }
    public string? GivenName { get; }
    public string? Surname { get; }
    public string? CountryRegion { get; }
    public string? MiddleName { get; }
    public string? Club { get; }
    public string? FeiId { get; }
}
