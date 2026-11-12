using NC.Domain.Exceptions;
using NoTiming.Domain.Aggregates;

namespace NoTiming.Domain.Core.Objects;

public static class EventTimeZone
{
    public static string ForCountry(Country country)
    {
        return country.IsoCode.ToUpperInvariant() switch
        {
            "BG" or "BGR" => "Europe/Sofia",
            "TR" or "TUR" => "Europe/Istanbul",
            _ => throw new DomainException($"No time zone is configured for country {country.IsoCode}"),
        };
    }
}
