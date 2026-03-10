namespace NTS.Domain.Setup.Aggregates;

public class User : Aggregate
{
    public User(string? email, string? name, IEnumerable<string>? roles = null, int? id = null)
        : base(id)
    {
        Email = Required(nameof(Email), email).Trim();
        Name = string.IsNullOrWhiteSpace(name) ? Email : name.Trim();
        Roles =
            roles
                ?.Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? [];
    }

    public string Email { get; }
    public string Name { get; }
    public IReadOnlyList<string> Roles { get; }

    public override string ToString()
    {
        return $"{Name} ({Email})";
    }
}
