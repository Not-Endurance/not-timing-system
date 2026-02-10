using Not.Application.CRUD.Ports;
using Not.Injection;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using Not.Notify;
using NTS.Application.Socket;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Features.Setup.UpcomingEvents;

public class UpcomingEventBehind
    : KrudServiceBase<UpcomingEvent, UpcomingEventFormModel>,
        IKrudMirror<Loop>,
        IKrudMirror<Combination>,
        IKrudMirror<Athlete>,
        IKrudMirror<Horse>, 
    ITransient
{
    readonly IUpdate<UpcomingEvent> _updater;
    readonly INtsSocketService _eventContext;

    public UpcomingEventBehind(IRepository<UpcomingEvent> events, INtsSocketService eventContext)
        : base(events, [])
    {
        _updater = events;
        _eventContext = eventContext;
    }

    public override Task Delete(UpcomingEvent entity)
    {
        NotifyHelper.Inform("Upcoming events cannot be deleted");
        return Task.CompletedTask;
    }

    // TODO: separate the Athlete, Horse, Loop and Combination AggregateRoots
    // from the UpcomingEvent AggregateRoot and maintain synced state using a domain event dispatcher
    // I.e. - Horse updates, raising a domain Event which updates UpcomingEvent state and (maybe?) triggers re-render
    public async Task Reflect(Loop loop)
    {
        if (_eventContext.Event == null)
        {
            return;
        }
        foreach (var competitions in _eventContext.Event.Competitions)
        {
            foreach (var phase in competitions.Phases)
            {
                phase.Reflect(loop);
            }
        }
        await _updater.Update(_eventContext.Event);
    }

    public async Task Reflect(Combination combination)
    {
        if (_eventContext.Event == null)
        {
            return;
        }
        foreach (var competitions in _eventContext.Event.Competitions)
        {
            foreach (var participation in competitions.Participations)
            {
                participation.Reflect(combination);
            }
        }
        await _updater.Update(_eventContext.Event);
    }

    public async Task Reflect(Athlete athlete)
    {
        if (_eventContext.Event == null)
        {
            return;
        }
        foreach (var combination in _eventContext.Event.Combinations)
        {
            combination.Reflect(athlete);
            foreach (var competition in _eventContext.Event.Competitions)
            {
                foreach (var participation in competition.Participations)
                {
                    participation.Reflect(combination);
                }
            }
        }

        await _updater.Update(_eventContext.Event);
    }

    public async Task Reflect(Horse horse)
    {
        if (_eventContext.Event == null)
        {
            return;
        }
        foreach (var combination in _eventContext.Event.Combinations)
        {
            combination.Reflect(horse);
            foreach (var competition in _eventContext.Event.Competitions)
            {
                foreach (var participation in competition.Participations)
                {
                    participation.Reflect(combination);
                }
            }
        }
        await _updater.Update(_eventContext.Event);
    }
}
