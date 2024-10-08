using Not.Application.Adapters.Behinds;
using Not.Application.Ports.CRUD;
using NTS.Domain.Setup.Entities;
using NTS.Judge.Setup.Competitions;

namespace NTS.Judge.Events;

public class CompetitionBehind : SimpleCrudBehind<Competition, CompetitionFormModel>
{
    private readonly IRepository<Event> _events;

    public CompetitionBehind(IRepository<Event> events) : base(null!)
    {
        _events = events;
    }

    protected override async Task<bool> PerformInitialization(params IEnumerable<object> args)
    {
        EventBehind.StaticEnduranceEvent = await _events.Read(0);
        ObservableCollection.AddRange(EventBehind.StaticEnduranceEvent?.Competitions ?? []);
        return EventBehind.StaticEnduranceEvent != null;
    }

    protected override async Task<CompetitionFormModel> SafeCreate(CompetitionFormModel model)
    {
        var entity = CreateEntity(model);
        EventBehind.StaticEnduranceEvent!.Add(entity);
        await _events.Update(EventBehind.StaticEnduranceEvent);
        ObservableCollection.AddOrReplace(entity);
        return model;
    }

    protected override async Task<CompetitionFormModel> SafeUpdate(CompetitionFormModel model)
    {
        var entity = UpdateEntity(model);
        EventBehind.StaticEnduranceEvent!.Update(entity);
        await _events.Update(EventBehind.StaticEnduranceEvent);
        ObservableCollection.AddOrReplace(entity);
        return model;
    }

    protected override async Task<Competition> SafeDelete(Competition entity)
    {
        EventBehind.StaticEnduranceEvent!.Remove(entity);
        await _events.Update(EventBehind.StaticEnduranceEvent);
        ObservableCollection.Remove(entity);
        return entity;
    }

    protected override Competition CreateEntity(CompetitionFormModel model)
    {
        return Competition.Create(model.Name, model.Type, model.StartTime, model.CRIRecovery);
    }

    protected override Competition UpdateEntity(CompetitionFormModel model)
    {
        return Competition.Update(model.Id, model.Name, model.Type, model.StartTime, model.CRIRecovery, model.Phases, model.Contestants);
    }
}
