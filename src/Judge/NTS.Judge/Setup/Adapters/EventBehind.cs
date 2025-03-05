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
    readonly IParentContext<Competition> _competitionParent;
    readonly IParentContext<Official> _officialParent;

    public EventBehind(
        IRepository<EnduranceEvent> events,
        EventParentContext context,
        IParentContext<Competition> compeitionParent,
        IParentContext<Official> officialParent
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
        _context.Entity = await _events.Read(0);
        if (_context.Entity == null)
        {
            return false;
        }
        Model = new EnduranceEventFormModel();
        Model.FromEntity(_context.Entity);
        return false;
    }

    public async Task Create(EnduranceEventFormModel enduranceEvent)
    {
        Task action() => SafeCreate(enduranceEvent);
        await SafeHelper.Run(action);
    }

    public async Task Update(EnduranceEventFormModel enduranceEvent)
    {
        Task action() => SafeUpdate(enduranceEvent);
        await SafeHelper.Run(action);
    }

    public Task<EnduranceEvent> Delete(EnduranceEvent enduranceEvent)
    {
        throw new NotImplementedException("Endurance event cannot be deleted");
    }

    async Task SafeCreate(EnduranceEventFormModel model)
    {
        _context.Entity = EnduranceEvent.Create(model.Place, model.Country);
        await _events.Create(_context.Entity);
        Model = model;
        EmitChange();
    }

    async Task SafeUpdate(EnduranceEventFormModel model)
    {
        _context.Entity = EnduranceEvent.Update(
            model.Id,
            model.Place,
            model.Country,
            _competitionParent.Children,
            _officialParent.Children
        );
        await _events.Update(_context.Entity);

        Model = model;
        EmitChange();
    }
}
