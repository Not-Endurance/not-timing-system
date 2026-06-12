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
        int id,
        bool isActive = true
    )
        : base(id)
    {
        Country = Required(nameof(Country), country);
        Name = Required(nameof(Name), name);
        Location = Required(nameof(Location), location);
        EventSpan = eventSpan;
        FeiShowId = feiShowId;
        IsActive = isActive;
    }

    public Country Country { get; }
    public string Name { get; }
    public string Location { get; }
    public EventSpan EventSpan { get; }
    public string? FeiShowId { get; }
    public bool IsActive { get; }

    public override string ToString()
    {
        return $"{Name} {Location} {Country} {EventSpan}";
    }
}
