using Newtonsoft.Json;
using Not.Domain.Base;
using NTS.Domain.Core.Objects;

namespace NTS.Domain.Core.Aggregates;

public class EnduranceEvent : AggregateRoot, IAggregateRoot
{
    [JsonConstructor]
    EnduranceEvent(
        int id,
        PopulatedPlace populatedPlace,
        EventSpan eventSpan,
        string? feiId,
        string? feiCode,
        string? showFeiId
    )
        : base(id)
    {
        PopulatedPlace = populatedPlace;
        EventSpan = eventSpan;
        FeiId = feiId;
        FeiCode = feiCode;
        ShowFeiId = showFeiId;
    }

    public EnduranceEvent(
        int id,
        Country country,
        string city,
        string place,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        string? feiId,
        string? feiCode,
        string? showFeiId
    )
        : this(
            id,
            new PopulatedPlace(country, city, place),
            new EventSpan(startDate, endDate),
            feiId,
            feiCode,
            showFeiId
        ) { }

    public PopulatedPlace PopulatedPlace { get; set; }
    public EventSpan EventSpan { get; }
    public string? FeiId { get; }
    public string? FeiCode { get; }
    public string? ShowFeiId { get; }

    public override string ToString()
    {
        return $"{PopulatedPlace} {EventSpan}";
    }
}
