using NTS.Domain.Setup.Entities;
using Not.Application.Ports.CRUD;
using Not.Blazor.Ports.Behinds;
using Not.Safe;
using NTS.Judge.Blazor.Pages.Setup.Ports;
using NTS.Judge.Blazor.Setup.Events;
using NTS.Judge.Contexts;

namespace NTS.Judge.Events;

public class EventBehind : ObservableBehind, IEnduranceEventBehind
{
    private readonly IRepository<Event> _events;
    private readonly EventParentContext _context;

    public EventBehind(IRepository<Event> events, EventParentContext context)
    {
        _events = events;
        _context = context;
    }

    public EventFormModel? Model { get; private set; }

    protected override async Task<bool> PerformInitialization(params IEnumerable<object> _)
    {
        await _context.Load(0);
        if (_context.Entity == null)
        {
            return false;
        }
        Model = new EventFormModel();
        Model.FromEntity(_context.Entity);
        return false;
    }

    async Task<EventFormModel> SafeCreate(EventFormModel model)
    {
        _context.Entity = Event.Create(model.Place, model.Country);
        await _events.Create(_context.Entity);
        Model = model;
        EmitChange();
        return model;
    }

    async Task<EventFormModel> SafeUpdate(EventFormModel model)
    {
        _context.Entity = Event.Update(model.Id, model.Place, model.Country, model.Competitions, model.Officials);
        await _events.Update(_context.Entity);
        Model = model;
        EmitChange();
        return model;
    }

    #region SafePattern 

    public async Task<EventFormModel> Create(EventFormModel @event)
    {
        return await SafeHelper.Run(() => SafeCreate(@event)) ?? @event;
    }

    public async Task<EventFormModel> Update(EventFormModel @event)
    {
        return await SafeHelper.Run(() => SafeUpdate(@event)) ?? @event;
    }

    public Task<Event> Delete(Event @event)
    {
        throw new NotImplementedException("Endurance event cannot be deleted");
    }

    #endregion
}
