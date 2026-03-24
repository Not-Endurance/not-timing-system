using Not.Application.CRUD.Ports;
using Not.Events;
using Not.Exceptions;
using Not.Injection;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using Not.Notify;
using Not.Structures;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;
using NTS.Domain.Setup.Services.StartValidation;

namespace NTS.Judge.Features.Setup.UpcomingEvents;

public class UpcomingEventService
    : KrudServiceBase<UpcomingEvent, UpcomingEventFormModel>,
        IUpcomingEventService,
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

    public async Task<Result<IReadOnlyList<StartValidationIssue>>> Validate(int upcomingEventId)
    {
        var setupEvent = await GetEvent(upcomingEventId);
        return StartValidator.Validate(setupEvent);
    }

    public async Task DeleteParticipation(int upcomingEventId, int participationNumber, int competitionId)
    {
        var setupEvent = await GetEvent(upcomingEventId);
        var competition = setupEvent.Competitions.FirstOrDefault(x => x.Id == competitionId);
        GuardHelper.ThrowIfDefault(competition, $"Competition with id '{competitionId}' does not exist");

        var participation = competition.Participations.FirstOrDefault(x => x.Combination.Number == participationNumber);
        if (participation == null)
        {
            return;
        }

        competition.Remove(participation);
        await _upcomingEvents.Update(setupEvent);
    }

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

    async Task<UpcomingEvent> GetEvent(int upcomingEventId)
    {
        var upcomingEvent = await _upcomingEvents.Read(upcomingEventId);
        return upcomingEvent ?? throw GuardHelper.Exception($"Event with id '{upcomingEventId}' is not selected");
    }
}

public interface IUpcomingEventService : ITransient
{
    Task<Result<IReadOnlyList<StartValidationIssue>>> Validate(int upcomingEventId);
    Task DeleteParticipation(int upcomingEventId, int participationNumber, int competitionId);
}
