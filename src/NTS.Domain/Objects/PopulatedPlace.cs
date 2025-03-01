using Not.Domain.Base;

namespace NTS.Domain.Objects;

public record PopulatedPlace : DomainObject
{
    public PopulatedPlace(Country country, string city, string? location)
    {
        Country = country;
        City = city;
        Location = location;
    }

    public Country Country { get; }
    public string City { get; }
    public string? Location { get; }

    public override string ToString()
    {
        var sb = new StringBuilder();
        if (Location != null)
        {
            sb.Append($"{Location} ");
        }
        if (City != null)
        {
            sb.Append($"{City} ");
        }
        var country = Country.ToString();
        sb.Append(country);
        return sb.ToString();
    }
}
