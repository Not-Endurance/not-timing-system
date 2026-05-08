using NTS.Domain.Aggregates;
using NTS.Domain.Core.Objects;

namespace NTS.Domain.Core.Aggregates;

public class EventInformation : Aggregate
{
    public EventInformation(
        Country country,
        string? name,
        string? location,
        EventSpan eventSpan,
        string? feiShowId,
        string? feiId,
        string? feiEventCode,
        int id
    )
        : base(id)
    {
        Country = Required(nameof(Country), country);
        Name = Required(nameof(Name), name);
        Location = Required(nameof(Location), location);
        EventSpan = eventSpan;
        FeiShowId = feiShowId;
        FeiId = feiId;
        FeiEventCode = feiEventCode;
    }

    public Country Country { get; }
    public string Name { get; }
    public string Location { get; }
    public EventSpan EventSpan { get; }
    public string? FeiShowId { get; }
    public string? FeiId { get; }
    public string? FeiEventCode { get; }

    public override string ToString()
    {
        return $"{Name} {Location} {Country} {EventSpan}";
    }
}
