namespace Not.Application.Authentication.User;

public record NUserRegistrationProfile
{
    public NUserRegistrationProfile(
        string? name = null,
        string? givenName = null,
        string? middleName = null,
        string? surname = null,
        string? club = null,
        string? feiId = null,
        string? displayName = null
    )
    {
        GivenName = Normalize(givenName);
        MiddleName = Normalize(middleName);
        Surname = Normalize(surname);
        Club = Normalize(club);
        FeiId = Normalize(feiId);
        DisplayName = Normalize(displayName);
        Name = BuildName(GivenName, MiddleName, Surname) ?? Normalize(name);
    }

    public string? Name { get; }
    public string? DisplayName { get; }
    public string? GivenName { get; }
    public string? MiddleName { get; }
    public string? Surname { get; }
    public string? Club { get; }
    public string? FeiId { get; }

    static string? BuildName(params string?[] parts)
    {
        var nameParts = parts.Where(part => !string.IsNullOrWhiteSpace(part)).ToArray();
        return nameParts.Length == 0 ? null : string.Join(" ", nameParts);
    }

    static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
