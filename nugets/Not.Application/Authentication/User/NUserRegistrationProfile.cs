namespace Not.Application.Authentication.User;

public record NUserRegistrationProfile
{
    public NUserRegistrationProfile(
        string? name = null,
        string? givenName = null,
        string? middleName = null,
        string? surname = null,
        string? club = null,
        string? feiId = null
    )
    {
        Name = name;
        GivenName = givenName;
        MiddleName = middleName;
        Surname = surname;
        Club = club;
        FeiId = feiId;
    }

    public string? Name { get; }
    public string? GivenName { get; }
    public string? MiddleName { get; }
    public string? Surname { get; }
    public string? Club { get; }
    public string? FeiId { get; }
}
