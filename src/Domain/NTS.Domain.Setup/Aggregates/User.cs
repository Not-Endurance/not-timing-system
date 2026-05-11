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
        string? feiId = null
    )
        : base(id)
    {
        Email = Required(nameof(Email), email).Trim();
        Name = string.IsNullOrWhiteSpace(name) ? Email : name.Trim();
        GivenName = Normalize(givenName);
        MiddleName = Normalize(middleName);
        Surname = Normalize(surname);
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

    static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
