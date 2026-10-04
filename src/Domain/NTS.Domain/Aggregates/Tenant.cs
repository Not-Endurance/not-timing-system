using Not.Domain.Exceptions;

namespace NTS.Domain.Aggregates;

/// <summary>
/// A country's equestrian federation: the organiser that owns Events and the Setup data it prepares (ADR-0012). It comes
/// into being when the first person from a country with an ISO code registers (ADR-0002), and it is stored as a
/// document of its own so that organisations that are not countries can be added later.
/// </summary>
public sealed record Tenant
{
    public const string COUNTRY = "country";

    public static Tenant ForCountry(Country country)
    {
        return new Tenant($"{COUNTRY}-{country.IsoCode.ToLowerInvariant()}", country.Name, COUNTRY);
    }

    /// <summary>
    /// The Tenant of a person from the country of their profile, found the way the profile screen finds it (by name, ISO
    /// code or NF code; the first country that matches). None when the profile has no country or it matches no country:
    /// such a person stays unassigned until they pick one.
    /// </summary>
    public static Tenant? DerivedFrom(string? profileCountry, IEnumerable<Country> countries)
    {
        var country = countries.FirstOrDefault(x => x.Matches(profileCountry));
        return country == null ? null : ForCountry(country);
    }

    public Tenant(string id, string name, string kind)
    {
        Id = Required(nameof(Id), id);
        Name = Required(nameof(Name), name);
        Kind = Required(nameof(Kind), kind);
    }

    /// <summary>A key of its kind and the ISO code, such as <c>country-bg</c>. It is text, not a Guid.</summary>
    public string Id { get; }

    public string Name { get; }
    public string Kind { get; }

    static string Required(string field, string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new DomainPropertyException(
                field,
                string.Format(Field_1_is_required_on_2_string, field, nameof(Tenant))
            )
            : value;
    }
}
