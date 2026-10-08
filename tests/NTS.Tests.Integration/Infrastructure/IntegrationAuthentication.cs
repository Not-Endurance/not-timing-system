namespace NTS.Tests.Integration.Infrastructure;

internal sealed record IntegrationUser
{
    public IntegrationUser(
        string email,
        string userIdentifier,
        string name,
        string? givenName = null,
        string? middleName = null,
        string? surname = null,
        string? countryRegion = null,
        string? club = null,
        string? feiId = null,
        string? displayName = null
    )
    {
        Email = email;
        UserIdentifier = userIdentifier;
        Name = name;
        DisplayName = displayName;
        GivenName = givenName;
        MiddleName = middleName;
        Surname = surname;
        CountryRegion = countryRegion;
        Club = club;
        FeiId = feiId;
    }

    public string Email { get; }
    public string UserIdentifier { get; }
    public string Name { get; }
    public string? DisplayName { get; }
    public string? GivenName { get; }
    public string? MiddleName { get; }
    public string? Surname { get; }
    public string? CountryRegion { get; }
    public string? Club { get; }
    public string? FeiId { get; }
}
