using Not.Domain.Exceptions;

namespace NTS.Domain.Aggregates;

/// <summary>
/// A country's equestrian federation: the organiser that owns Events and the Setup data it prepares (ADR-0012). It comes
/// into being when the first person from a country with an ISO code registers (ADR-0002), and it is stored as a
/// document of its own so that organisations that are not countries can be added later. It sets the rules its Regional
/// competitions use; an Event copies them when it starts. <see cref="LEGACY_ID"/> is the one Tenant every document had
/// before Tenants meant something (ADR-0002): no account belongs to it, and what is stamped with it waits for the
/// migration that gives it the Tenant of Bulgaria.
/// </summary>
public sealed record Tenant
{
    public const string COUNTRY = "country";
    public const string LEGACY_ID = "nts";

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

    public Tenant(string id, string name, string kind, RegionalRules? regionalRules = null)
    {
        Id = Required(nameof(Id), id);
        Name = Required(nameof(Name), name);
        Kind = Required(nameof(Kind), kind);
        RegionalRules = regionalRules ?? RegionalRules.None;
    }

    /// <summary>A key of its kind and the ISO code, such as <c>country-bg</c>. It is text, not a Guid.</summary>
    public string Id { get; }

    public string Name { get; }
    public string Kind { get; }

    /// <summary>The rules of the Regional competitions of the Tenant. A Tenant that has set none has the FEI's.</summary>
    public RegionalRules RegionalRules { get; }

    /// <summary>
    /// The Tenant with other rules. It is another Tenant: an Event that copied the rules of this one when it started
    /// keeps them.
    /// </summary>
    public Tenant WithRules(RegionalRules regionalRules)
    {
        return new Tenant(Id, Name, Kind, regionalRules);
    }

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
