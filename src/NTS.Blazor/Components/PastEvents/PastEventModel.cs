using Not.Krud.Abstractions;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;

namespace NTS.Blazor.Components.PastEvents;

public class PastEventModel : IKrudModel<EventInformation>, IKrudFormModel
{
    public int? Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Location { get; private set; } = string.Empty;
    public EventSpan EventSpan { get; private set; } = default!;
    public Country Country { get; private set; } = default!;
    public string? FeiShowId { get; private set; }
    public string? FeiId { get; private set; }
    public string? FeiEventCode { get; private set; }

    public void MapFrom(EventInformation entity)
    {
        Id = entity.Id;
        Name = entity.Name;
        Location = entity.Location;
        EventSpan = entity.EventSpan;
        Country = entity.Country;
        FeiShowId = entity.FeiShowId;
        FeiId = entity.FeiId;
        FeiEventCode = entity.FeiEventCode;
    }

    public EventInformation MapToEntity()
    {
        return new EventInformation(
            Country,
            Name,
            Location,
            EventSpan,
            FeiShowId,
            FeiId,
            FeiEventCode,
            Id.GetValueOrDefault()
        );
    }
}
