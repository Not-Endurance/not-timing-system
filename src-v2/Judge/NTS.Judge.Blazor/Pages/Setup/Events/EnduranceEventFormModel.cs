using Not.Blazor.Ports;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Entities;

namespace NTS.Judge.Blazor.Setup.Events;

public class EnduranceEventFormModel : IFormModel<EnduranceEvent>
{
    public EnduranceEventFormModel()
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

    public void FromEntity(EnduranceEvent enduranceEvent)
    {
        Id = enduranceEvent.Id;
        Place = enduranceEvent.Place;
        Country = enduranceEvent.Country;
        Competitions = enduranceEvent.Competitions;
        Officials = enduranceEvent.Officials;
    }
}