using NTS.Domain.Setup.Entities;
using Not.Application.Ports.CRUD;
using Not.Safe;
using NTS.Judge.Blazor.Pages.Setup.Ports;
using NTS.Judge.Blazor.Setup.Events;
using NTS.Judge.Contexts;
using Not.Application.Adapters.Behinds;
using Not.Application.Contexts;

namespace NTS.Judge.Events;

public class EventBehind : ObservableBehind, IEnduranceEventBehind
{
    private readonly IRepository<EnduranceEvent> _events;
    private readonly EventParentContext _context;
    private readonly IParentContext<Competition> _competitionParent;
    private readonly IParentContext<Official> _officialParent;

    public EventBehind(
        IRepository<EnduranceEvent> events, 
        EventParentContext context, 
        IParentContext<Competition> compeitionParent,
        IParentContext<Official> officialParent)
    {
        _events = events;
        _context = context;
        _competitionParent = compeitionParent;
        _officialParent = officialParent;
    }

    public EnduranceEventFormModel? Model { get; private set; }

    protected override async Task<bool> PerformInitialization(params IEnumerable<object> _)
    {
        await _context.Load(0);
        if (_context.Entity == null)
        {
            return false;
        }
        Model = new EnduranceEventFormModel();
        Model.FromEntity(_context.Entity);
        return false;
    }

    async Task<EnduranceEventFormModel> SafeCreate(EnduranceEventFormModel model)
    {
        _context.Entity = EnduranceEvent.Create(model.Place, model.Country);
        await _events.Create(_context.Entity);
        Model = model;
        EmitChange();
        return model;
    }

    async Task<EnduranceEventFormModel> SafeUpdate(EnduranceEventFormModel model)
    {
        _context.Entity = EnduranceEvent.Update(
            model.Id,
            model.Place,
            model.Country,
            _competitionParent.Children,
            _officialParent.Children);
        await _events.Update(_context.Entity);

        Model = model;
        EmitChange();
        return model;
    }

    #region SafePattern 

    public async Task<EnduranceEventFormModel> Create(EnduranceEventFormModel enduranceEvent)
    {
        return await SafeHelper.Run(() => SafeCreate(enduranceEvent)) ?? enduranceEvent;
    }

    public async Task<EnduranceEventFormModel> Update(EnduranceEventFormModel enduranceEvent)
    {
        return await SafeHelper.Run(() => SafeUpdate(enduranceEvent)) ?? enduranceEvent;
    }

    public Task<EnduranceEvent> Delete(EnduranceEvent enduranceEvent)
    {
        throw new NotImplementedException("Endurance event cannot be deleted");
    }

    #endregion
}
