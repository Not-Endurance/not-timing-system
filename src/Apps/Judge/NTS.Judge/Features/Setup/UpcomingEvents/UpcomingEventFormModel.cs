using Not.Krud.Models;
using NTS.Domain.Aggregates;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Features.Setup.UpcomingEvents;

public record UpcomingEventFormModel : KrudFormModel<UpcomingEvent>
{
    public UpcomingEventFormModel()
    {
#if DEBUG
        Name = "Test";
        Location = "Каспичан";
#endif
    }

    public string? Name { get; set; }
    public string? Location { get; set; }
    public Country? Country { get; set; }
    public string? FeiShowId { get; set; }
    public string? FeiId { get; set; }
    public string? FeiEventCode { get; set; }
    public IReadOnlyCollection<Competition> Competitions { get; private set; } = [];
    public IReadOnlyCollection<Official> Officials { get; private set; } = [];
    public IReadOnlyCollection<Combination> Combinations { get; private set; } = [];
    public IReadOnlyCollection<Loop> Loops { get; private set; } = [];

    protected override UpcomingEvent MapTo()
    {
        return new UpcomingEvent(
            Name,
            Location,
            Country,
            FeiShowId,
            FeiId,
            FeiEventCode,
            Competitions,
            Officials,
            Loops,
            Combinations,
            Id
        );
    }

    public override void MapFrom(UpcomingEvent upcomingEvent)
    {
        Id = upcomingEvent.Id;
        Name = upcomingEvent.Name;
        Location = upcomingEvent.Location;
        Country = upcomingEvent.Country;
        FeiShowId = upcomingEvent.ShowFeiId;
        FeiId = upcomingEvent.FeiId;
        FeiEventCode = upcomingEvent.FeiEventCode;
        Competitions = upcomingEvent.Competitions;
        Officials = upcomingEvent.Officials;
        Combinations = upcomingEvent.Combinations;
        Loops = upcomingEvent.Loops;
    }
}
