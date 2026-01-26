using NTS.Domain.Core.Objects;

namespace NTS.Domain.Core.Aggregates;

public class EnduranceEvent : Aggregate
{
    public EnduranceEvent(
        PopulatedPlace populatedPlace,
        EventSpan eventSpan,
        string? feiShowId,
        string? feiId,
        string? feiEventCode,
        int id
    )
        : base(id)
    {
        PopulatedPlace = populatedPlace;
        EventSpan = eventSpan;
        FeiShowId = feiShowId;
        FeiId = feiId;
        FeiEventCode = feiEventCode;
    }

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
