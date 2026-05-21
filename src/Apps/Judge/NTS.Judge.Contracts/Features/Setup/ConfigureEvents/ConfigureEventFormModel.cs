using Not.Krud.Models;
using NTS.Domain.Aggregates;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NTS.Judge.Contracts.Features.Setup.ConfigureEvents;

public record ConfigureEventFormModel : KrudFormModel<ConfigureEvent>
{
    public ConfigureEventFormModel()
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
    public IReadOnlyCollection<Competition> Competitions { get; private set; } = [];
    public IReadOnlyCollection<Official> Officials { get; private set; } = [];
    public IReadOnlyCollection<Combination> Combinations { get; private set; } = [];
    public IReadOnlyCollection<Loop> Loops { get; private set; } = [];

    protected override ConfigureEvent MapTo()
    {
        return new ConfigureEvent(Name, Location, Country, FeiShowId, Competitions, Officials, Loops, Combinations, Id);
    }

    public override void MapFrom(ConfigureEvent configureEvent)
    {
        Id = configureEvent.Id;
        Name = configureEvent.Name;
        Location = configureEvent.Location;
        Country = configureEvent.Country;
        FeiShowId = configureEvent.FeiShowId;
        Competitions = configureEvent.Competitions;
        Officials = configureEvent.Officials;
        Combinations = configureEvent.Combinations;
        Loops = configureEvent.Loops;
    }
}
