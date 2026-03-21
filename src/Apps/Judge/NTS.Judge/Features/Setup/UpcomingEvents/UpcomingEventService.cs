using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Events;
using Not.Injection;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using Not.Notify;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Features.Setup.UpcomingEvents;

public class UpcomingEventService
    : KrudServiceBase<UpcomingEvent, UpcomingEventFormModel>,
        IKrudMirror<Loop>,
        IKrudMirror<Combination>,
        IKrudMirror<Athlete>,
        IKrudMirror<Horse>,
        ITransient
{
    readonly IRepository<UpcomingEvent> _upcomingEvents;
    readonly INotifier _notifier;
    readonly ISelectedUpcomingEventContext _selectedEventContext;

    public UpcomingEventService(
        IRepository<UpcomingEvent> upcomingEvents,
        INotifier notifier,
        ISelectedUpcomingEventContext context
    )
        : base(upcomingEvents, [])
    {
        _upcomingEvents = upcomingEvents;
        _notifier = notifier;
        _selectedEventContext = context;
    }

    public IEventSubscriber ObservableEvent { get; } = new Event();

    public override Task Delete(UpcomingEvent entity)
    {
        _notifier.Inform(Upcoming_events_cannot_be_deleted_string);
        return Task.CompletedTask;
    }

    // TODO: Move Reflection in Krud
    public async Task Reflect(Loop loop)
    {
        await UpdateReflectingEvents(upcomingEvent =>
        {
            var hasReflected = false;
            foreach (var competitions in upcomingEvent.Competitions)
            {
                foreach (var phase in competitions.Phases)
                {
                    hasReflected = true;
                    phase.Reflect(loop);
                }
            }
            return hasReflected;
        });
    }

    public async Task Reflect(Combination combination)
    {
        await UpdateReflectingEvents(upcomingEvent =>
        {
            var hasReflected = false;
            foreach (var competitions in upcomingEvent.Competitions)
            {
                foreach (var participation in competitions.Participations)
                {
                    hasReflected = true;
                    participation.Reflect(combination);
                }
            }
            return hasReflected;
        });
    }

    public async Task Reflect(Athlete athlete)
    {
        await UpdateReflectingEvents(upcomingEvent =>
        {
            var hasReflected = false;
            foreach (var combination in upcomingEvent.Combinations)
            {
                hasReflected = hasReflected || combination.Reflect(athlete);
                if (hasReflected)
                {
                    foreach (var competition in upcomingEvent.Competitions)
                    {
                        foreach (var participation in competition.Participations)
                        {
                            participation.Reflect(combination);
                        }
                    }
                }
            }
            return hasReflected;
        });
    }

    public async Task Reflect(Horse horse)
    {
        await UpdateReflectingEvents(upcomingEvent =>
        {
            var hasReflected = false;
            foreach (var combination in upcomingEvent.Combinations)
            {
                hasReflected = hasReflected || combination.Reflect(horse);
                if (hasReflected)
                {
                    foreach (var competition in upcomingEvent.Competitions)
                    {
                        foreach (var participation in competition.Participations)
                        {
                            participation.Reflect(combination);
                        }
                    }
                }
            }
            return hasReflected;
        });
    }

    async Task UpdateReflectingEvents(Func<UpcomingEvent, bool> reflect)
    {
        if (_selectedEventContext.Event == null)
        {
            return;
        }
        if (reflect(_selectedEventContext.Event))
        {
            await _upcomingEvents.Update(_selectedEventContext.Event);
        }
    }
}
