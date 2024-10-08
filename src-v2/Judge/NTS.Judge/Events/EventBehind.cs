using NTS.Domain.Setup.Entities;
using Not.Application.Ports.CRUD;
using Not.Blazor.Ports.Behinds;
using Not.Exceptions;
using Not.Safe;
using NTS.Judge.Blazor.Pages.Setup.Ports;
using NTS.Judge.Blazor.Setup.Events;

namespace NTS.Judge.Events;

public class EventBehind : ObservableBehind, IEnduranceEventBehind
{
    public static Event? StaticEnduranceEvent { get; internal set; }

    private readonly IRepository<Event> StaticEnduranceEventRepository;

    public EventBehind(IRepository<Event> eventRepository)
    {
        StaticEnduranceEventRepository = eventRepository;
    }

    public EventFormModel? Model { get; private set; }

    protected override async Task<bool> PerformInitialization(params IEnumerable<object> _)
    {
        StaticEnduranceEvent = await StaticEnduranceEventRepository.Read(0);
        if (StaticEnduranceEvent == null)
        {
            return false;
        }
        Model = new EventFormModel();
        Model.FromEntity(StaticEnduranceEvent);
        return true;
    }

    async Task<EventFormModel> SafeCreate(EventFormModel model)
    {
        StaticEnduranceEvent = Event.Create(model.Place, model.Country);
        await StaticEnduranceEventRepository.Create(StaticEnduranceEvent);
        Model = model;
        EmitChange();
        return model;
    }

    async Task<EventFormModel> SafeUpdate(EventFormModel model)
    {
        StaticEnduranceEvent = Event.Update(model.Id, model.Place, model.Country, model.Competitions, model.Officials);
        await StaticEnduranceEventRepository.Update(StaticEnduranceEvent);
        Model = model;
        EmitChange();
        return model;
    }

    async Task<Official> SafeCreate(Official child)
    {
        GuardHelper.ThrowIfDefault(StaticEnduranceEvent);

        StaticEnduranceEvent.Add(child);
        await StaticEnduranceEventRepository.Update(StaticEnduranceEvent);
        return child;
    }

    async Task<Official> SafeDelete(Official child)
    {
        GuardHelper.ThrowIfDefault(StaticEnduranceEvent);

        StaticEnduranceEvent.Remove(child);
        await StaticEnduranceEventRepository.Update(StaticEnduranceEvent);
        return child;
    }

    async Task<Official> SafeUpdate(Official child)
    {
        GuardHelper.ThrowIfDefault(StaticEnduranceEvent);

        StaticEnduranceEvent.Update(child);
        await StaticEnduranceEventRepository.Update(StaticEnduranceEvent);
        return child;
    }

    async Task<Event?> SafeInitialize(int id)
    {
        return StaticEnduranceEvent = await StaticEnduranceEventRepository.Read(id);
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
