namespace NC.Application.Authentication.User;

public record NUserRegistration
{
    public NUserRegistration(
        string email,
        string? name = null,
        string? givenName = null,
        string? surname = null,
        string? middleName = null,
        string? displayName = null
    )
    {
        Email = email;
        GivenName = Normalize(givenName);
        Surname = Normalize(surname);
        MiddleName = Normalize(middleName);
        DisplayName = Normalize(displayName);
        Name = BuildName(GivenName, MiddleName, Surname) ?? Normalize(name);
    }

    public string Email { get; }
    public string? Name { get; }
    public string? DisplayName { get; }
    public string? GivenName { get; }
    public string? Surname { get; }
    public string? MiddleName { get; }

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
