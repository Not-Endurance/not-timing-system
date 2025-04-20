using Not.Application.Behinds;
using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Setup.EnduranceEvents;
using NTS.Judge.Core.Behinds;

namespace NTS.Judge.Features.Setup.Home;

// TODO: Try to decouple Home logic in separate behind and inherit CrudBehind here
public class UpcomingEventBehind : ObservableBehind, IEnduranceEventBehind, ICrudReflection<Loop>, ICrudReflection<Combination>, ICrudReflection<Athlete>, ICrudReflection<Horse>
{
    readonly IRepository<UpcomingEvent> _events;
    readonly UpcomingEventCrudeContext _crudeContext;
    readonly IUpdate<UpcomingEvent> _updater;
    readonly IEventContext _eventContext;

    public UpcomingEventBehind(IRepository<UpcomingEvent> events, UpcomingEventCrudeContext crudeContext, IUpdate<UpcomingEvent> updater, IEventContext eventContext)
    {
        _events = events;
        _crudeContext = crudeContext;
        _updater = updater;
        _eventContext = eventContext;
    }

    public EnduranceEventFormModel? Model { get; private set; }

    protected override async Task<bool> PerformInitialization(params IEnumerable<object> _)
    {
        var enduranceEvent = await _events.Read(0);
        if (enduranceEvent == null)
        {
            return false;
        }
        _crudeContext.Set(enduranceEvent);
        Model = new EnduranceEventFormModel();
        Model.FromEntity(enduranceEvent);
        return false;
    }

    public async Task Create(EnduranceEventFormModel model)
    {
        var enduranceEvent = UpcomingEvent.Create(model.Name, model.Place, model.Country, model.FeiShowId);
        await _events.Create(enduranceEvent);
        _crudeContext.Set(enduranceEvent);
        Model = new EnduranceEventFormModel();
        Model.FromEntity(enduranceEvent);
        EmitChange();
    }

    public async Task Update(EnduranceEventFormModel model)
    {
        var enduranceEvent = UpcomingEvent.Update(
            model.Id,
            model.Name,
            model.Place,
            model.Country,
            model.FeiShowId,
            ((ICrudeParent<Competition>)_crudeContext).Children,
            ((ICrudeParent<Official>)_crudeContext).Children,
            ((ICrudeParent<Loop>)_crudeContext).Children,
            ((ICrudeParent<Combination>)_crudeContext).Children
        );
        await _events.Update(enduranceEvent);
        _crudeContext.Set(enduranceEvent);
        EmitChange();
    }

    public Task<UpcomingEvent> Delete(UpcomingEvent upcomingEvent)
    {
        throw new NotImplementedException("Endurance event cannot be deleted");
    }

    public async Task Reflect(Loop update)
    {
        foreach (var competitions in ((ICrudeParent<Competition>)_crudeContext).Children)
        {
            foreach (var phase in competitions.Phases)
            {
                phase.Reflect(update);
            }
        }
        await _updater.Update(_eventContext.Event!);
    }

    public async Task Reflect(Combination update)
    {
        foreach (var competitions in ((ICrudeParent<Competition>)_crudeContext).Children)
        {
            foreach (var participation in competitions.Participations)
            {
                participation.Reflect(update);
            }
        }
        await _updater.Update(_eventContext.Event!);
    }

    public async Task Reflect(Athlete update)
    {
        foreach (var combination in ((ICrudeParent<Combination>)_crudeContext).Children)
        {
            combination.Reflect(update);
            foreach (var competition in ((ICrudeParent<Competition>)_crudeContext).Children)
            {
                foreach (var participation in competition.Participations)
                {
                    participation.Reflect(combination);
                }
            }
        }

        await _updater.Update(_eventContext.Event!);
    }

    public async Task Reflect(Horse update)
    {
        foreach (var combination in ((ICrudeParent<Combination>)_crudeContext).Children)
        {
            combination.Reflect(update);
            foreach (var competition in ((ICrudeParent<Competition>)_crudeContext).Children)
            {
                foreach (var participation in competition.Participations)
                {
                    participation.Reflect(combination);
                }
            }
        }
        await _updater.Update(_eventContext.Event!);
    }
}
