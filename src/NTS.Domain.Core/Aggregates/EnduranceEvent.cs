using Newtonsoft.Json;
using Not.Domain.Base;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Objects;

namespace NTS.Domain.Core.Aggregates;

public class EnduranceEvent : AggregateRoot
{
    [JsonConstructor]
    EnduranceEvent(int id, PopulatedPlace populatedPlace, EventSpan eventSpan, string? feiShowId)
        : base(id)
    {
        PopulatedPlace = populatedPlace;
        EventSpan = eventSpan;
        FeiShowId = feiShowId;
    }

    public EnduranceEvent(
        int id,
        Country country,
        string city,
        string place,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        string? feiShowId
    )
        : this(id, new PopulatedPlace(country, city, place), new EventSpan(startDate, endDate), feiShowId) { }

    public PopulatedPlace PopulatedPlace { get; set; }
    public EventSpan EventSpan { get; }
    public string? FeiShowId { get; }

    public override string ToString()
    {
        return $"{PopulatedPlace} {EventSpan}";
    }
}
