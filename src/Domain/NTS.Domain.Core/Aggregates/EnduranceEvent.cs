using Newtonsoft.Json;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Objects;

namespace NTS.Domain.Core.Aggregates;

public class EnduranceEvent : Aggregate
{
    [JsonConstructor]
    EnduranceEvent(
        int id,
        PopulatedPlace populatedPlace,
        EventSpan eventSpan,
        string? feiShowId,
        string? feiId,
        string? feiEventCode
    )
        : base(id)
    {
        PopulatedPlace = populatedPlace;
        EventSpan = eventSpan;
        FeiShowId = feiShowId;
        FeiId = feiId;
        FeiEventCode = feiEventCode;
    }

    public EnduranceEvent(
        int id,
        Country country,
        string city,
        string place,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        string? feiShowId,
        string? feiId,
        string? feiEventCode
    )
        : this(
            id,
            new PopulatedPlace(country, city, place),
            new EventSpan(startDate, endDate),
            feiShowId,
            feiId,
            feiEventCode
        ) { }

    public PopulatedPlace PopulatedPlace { get; set; }
    public EventSpan EventSpan { get; }
    public string? FeiShowId { get; }
    public string? FeiId { get; }
    public string? FeiEventCode { get; }

    public override string ToString()
    {
        return $"{PopulatedPlace} {EventSpan}";
    }
}
