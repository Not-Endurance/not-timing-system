namespace NTS.Domain.Setup.Aggregates;

public class User : Aggregate
{
    public User(
        string? email,
        string? name,
        IEnumerable<string>? roles = null,
        int? id = null,
        string? givenName = null,
        string? middleName = null,
        string? surname = null,
        string? countryRegion = null,
        string? club = null,
        string? feiId = null,
        string? displayName = null
    )
        : base(id)
    {
        Email = Required(nameof(Email), email).Trim();
        GivenName = Normalize(givenName);
        MiddleName = Normalize(middleName);
        Surname = Normalize(surname);
        Name = BuildName(GivenName, MiddleName, Surname) ?? Normalize(name) ?? Email;
        DisplayName = Normalize(displayName);
        CountryRegion = Normalize(countryRegion);
        Club = Normalize(club);
        FeiId = Normalize(feiId);
        Roles =
            roles
                ?.Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? [];
    }

    public string Email { get; }
    public string Name { get; }
    public string? DisplayName { get; }
    public string? GivenName { get; }
    public string? MiddleName { get; }
    public string? Surname { get; }
    public string? CountryRegion { get; }
    public string? Club { get; }
    public string? FeiId { get; }
    public IReadOnlyList<string> Roles { get; }

    public override string ToString()
    {
        return $"{Name} ({Email})";
    }

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
