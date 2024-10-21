using Newtonsoft.Json;
using NTS.Domain.Core.Objects;

namespace NTS.Domain.Core.Entities;

public class EnduranceEvent : DomainEntity
{
    [JsonConstructor]
    private EnduranceEvent(
        int id,
        PopulatedPlace populatedPlace,
        EventSpan eventSpan,
        string? feiId,
        string? feiCode,
        string? showFeiId) : base(id)
    {
        PopulatedPlace = populatedPlace;
        EventSpan = eventSpan;
        FeiId = feiId;
        FeiCode = feiCode;
        ShowFeiId = showFeiId;
    }
    public EnduranceEvent(
        Country country,
        string city,
        string place,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        string? feiId,
        string? feiCode,
        string? showFeiId) : this(
            GenerateId(),
            new PopulatedPlace(country, city, place),
            new EventSpan(startDate, endDate),
            feiId,
            feiCode,
            showFeiId)
    {
    }

    public PopulatedPlace PopulatedPlace { get; private set; }
    public EventSpan EventSpan { get; private set; }
    public string? FeiId { get; private set; }
    public string? FeiCode { get; private set; }
    public string? ShowFeiId { get; private set; }

    public override string ToString()
    {
        return $"{PopulatedPlace} {EventSpan}";
    }
}
