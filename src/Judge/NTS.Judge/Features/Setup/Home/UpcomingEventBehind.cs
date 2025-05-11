using Not.Application.Behinds;
using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Notify;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Setup.EnduranceEvents;
using NTS.Judge.Core.Behinds;
using NTS.Judge.Features.Warp;

namespace NTS.Judge.Features.Setup.Home;

// TODO: Try to decouple Home logic in separate behind and inherit CrudBehind here
public class UpcomingEventBehind
    : CrudBehind<UpcomingEvent, EnduranceEventFormModel>,
        ICrudReflection<Loop>,
        ICrudReflection<Combination>,
        ICrudReflection<Athlete>,
        ICrudReflection<Horse>
{
    readonly UpcomingEventCrudeContext _crudeContext;
    readonly IUpdate<UpcomingEvent> _updater;
    readonly IEventContext _eventContext;

    public UpcomingEventBehind(
        IRepository<UpcomingEvent> events,
        UpcomingEventCrudeContext crudeContext,
        IEventContext eventContext
    )
        : base(events, [])
    {
        _updater = events;
        _crudeContext = crudeContext;
        _eventContext = eventContext;
    }

    protected override UpcomingEvent CreateEntity(EnduranceEventFormModel model)
    {
        return UpcomingEvent.Create(model.Name, model.Place, model.Country, model.FeiShowId);
    }

    protected override UpcomingEvent UpdateEntity(EnduranceEventFormModel model)
    {
        return UpcomingEvent.Update(
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
    }

    public override Task Delete(UpcomingEvent entity)
    {
        NotifyHelper.Inform("Upcoming events cannot be deleted");
        return Task.CompletedTask;
    }

    public async Task Reflect(Loop loop)
    {
        foreach (var competitions in ((ICrudeParent<Competition>)_crudeContext).Children)
        {
            foreach (var phase in competitions.Phases)
            {
                phase.Reflect(loop);
            }
        }
        await _updater.Update(_eventContext.Event!);
    }

    public async Task Reflect(Combination combination)
    {
        foreach (var competitions in ((ICrudeParent<Competition>)_crudeContext).Children)
        {
            foreach (var participation in competitions.Participations)
            {
                participation.Reflect(combination);
            }
        }
        await _updater.Update(_eventContext.Event!);
    }

    public async Task Reflect(Athlete athlete)
    {
        foreach (var combination in ((ICrudeParent<Combination>)_crudeContext).Children)
        {
            combination.Reflect(athlete);
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

    public async Task Reflect(Horse horse)
    {
        foreach (var combination in ((ICrudeParent<Combination>)_crudeContext).Children)
        {
            combination.Reflect(horse);
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
