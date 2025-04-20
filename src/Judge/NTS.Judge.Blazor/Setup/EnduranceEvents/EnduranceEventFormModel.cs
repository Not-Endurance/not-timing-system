using Not.Blazor.CRUD.Forms.Ports;
using NTS.Domain.Aggregates;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Blazor.Setup.EnduranceEvents;

public class EnduranceEventFormModel : IFormModel<UpcomingEvent>
{
    public EnduranceEventFormModel()
    {
#if DEBUG
        Name = "Test";
        Place = "Каспичан";
#endif
    }

    public int? Id { get; set; }
    public string Name { get; set; }
    public string? Place { get; set; }
    public Country? Country { get; set; }
    public string? FeiShowId { get; set; }
    public IReadOnlyCollection<Competition> Competitions { get; private set; } = [];
    public IReadOnlyCollection<Official> Officials { get; private set; } = [];

    public void FromEntity(UpcomingEvent upcomingEvent)
    {
        Id = upcomingEvent.Id;
        Name = upcomingEvent.Name;
        Place = upcomingEvent.Place;
        Country = upcomingEvent.Country;
        FeiShowId = upcomingEvent.ShowFeiId;
        Competitions = upcomingEvent.Competitions;
        Officials = upcomingEvent.Officials;
    }
}
