using Not.Application.Adapters.Behinds;
using Not.Application.Ports.CRUD;
using NTS.Domain.Setup.Entities;
using NTS.Judge.Blazor.Setup.Officials;

namespace NTS.Judge.Events;

public class OfficialBehind : SimpleCrudBehind<Official, OfficialFormModel>
{
    private readonly IRepository<Event> _events;

    public OfficialBehind(IRepository<Event> events) : base(null!)
    {
        _events = events;
    }

    protected override async Task<bool> PerformInitialization(params IEnumerable<object> args)
    {
        EventBehind.StaticEnduranceEvent = await _events.Read(0);
        ObservableCollection.AddRange(EventBehind.StaticEnduranceEvent?.Officials ?? []);
        return EventBehind.StaticEnduranceEvent != null;
    }

    protected override async Task<OfficialFormModel> SafeCreate(OfficialFormModel model)
    {
        var entity = CreateEntity(model);
        EventBehind.StaticEnduranceEvent!.Add(entity);
        await _events.Update(EventBehind.StaticEnduranceEvent);
        ObservableCollection.AddOrReplace(entity);
        return model;
    }

    protected override async Task<OfficialFormModel> SafeUpdate(OfficialFormModel model)
    {
        var entity = UpdateEntity(model);
        EventBehind.StaticEnduranceEvent!.Update(entity);
        await _events.Update(EventBehind.StaticEnduranceEvent);
        ObservableCollection.AddOrReplace(entity);
        return model;
    }

    protected override async Task<Official> SafeDelete(Official entity)
    {
        EventBehind.StaticEnduranceEvent!.Remove(entity);
        await _events.Update(EventBehind.StaticEnduranceEvent);
        ObservableCollection.Remove(entity);
        return entity;
    }

    protected override Official CreateEntity(OfficialFormModel model)
    {
        return Official.Create(model.Name, model.Role);
    }

    protected override Official UpdateEntity(OfficialFormModel model)
    {
        return Official.Update(model.Id, model.Name, model.Role);
    }
}
