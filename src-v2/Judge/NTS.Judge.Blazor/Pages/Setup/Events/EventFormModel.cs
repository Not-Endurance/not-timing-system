using Not.Blazor.Ports;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Entities;

namespace NTS.Judge.Blazor.Setup.Events;

public class EventFormModel : IFormModel<Event>
{
    public EventFormModel()
    {
        //mock default values for testing
        Place = "Каспичан";
        Country = new Country("BG", "zz", "Bulgaria");
    }

    public int Id { get; private set; }
    public string? Place { get; set; }
    public Country? Country { get; set; }
    public IReadOnlyCollection<Phase> Phases { get; private set; } = [];
    public IReadOnlyCollection<Competition> Competitions { get; private set; } = [];
    public IReadOnlyCollection<Official> Officials { get; private set; } = [];

    public void FromEntity(Event @event)
    {
        Id = @event.Id;
        Place = @event.Place;
        Country = @event.Country;
        Competitions = @event.Competitions;
        Officials = @event.Officials;
    }
}