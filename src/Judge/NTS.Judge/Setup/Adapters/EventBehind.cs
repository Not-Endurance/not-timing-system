using Not.Application.Behinds;
using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Safe;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Setup.EnduranceEvents;
using NTS.Judge.Core.Behinds;

namespace NTS.Judge.Setup.Adapters;

public class EventBehind : ObservableBehind, IEnduranceEventBehind
{
    readonly IRepository<EnduranceEvent> _events;
    readonly EventParentContext _context;
    readonly ICrudParent<Competition> _competitionParent;
    readonly ICrudParent<Official> _officialParent;

    public EventBehind(
        IRepository<EnduranceEvent> events,
        EventParentContext context,
        ICrudParent<Competition> compeitionParent,
        ICrudParent<Official> officialParent
    )
    {
        _events = events;
        _context = context;
        _competitionParent = compeitionParent;
        _officialParent = officialParent;
    }

    public EnduranceEventFormModel? Model { get; private set; }

    protected override async Task<bool> PerformInitialization(params IEnumerable<object> _)
    {
        var enduranceEvent = await _events.Read(0);
        if (enduranceEvent == null)
        {
            return false;
        }
        _context.SetParent(enduranceEvent);
        Model = new EnduranceEventFormModel();
        Model.FromEntity(enduranceEvent);
        return false;
    }

    public async Task Create(EnduranceEventFormModel model)
    {
        var enduranceEvent = EnduranceEvent.Create(model.Place, model.Country, model.FeiShowId);
        await _events.Create(enduranceEvent);
        _context.SetParent(enduranceEvent);
        Model = new EnduranceEventFormModel();
        Model.FromEntity(enduranceEvent);
        EmitChange();
    }

    public async Task Update(EnduranceEventFormModel model)
    {
        var enduranceEvent = EnduranceEvent.Update(
            model.Id,
            model.Place,
            model.Country,
            model.FeiShowId,
            _competitionParent.Children,
            _officialParent.Children
        );
        await _events.Update(enduranceEvent);
        _context.SetParent(enduranceEvent);
        EmitChange();
    }

    public Task<EnduranceEvent> Delete(EnduranceEvent enduranceEvent)
    {
        throw new NotImplementedException("Endurance event cannot be deleted");
    }
}
