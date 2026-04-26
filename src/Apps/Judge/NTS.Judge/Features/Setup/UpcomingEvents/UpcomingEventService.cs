using Not.Application.CRUD.Ports;
using Not.Events;
using Not.Exceptions;
using Not.Injection;
using Not.Krud.Services;
using Not.Notify;
using Not.Structures;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Services.StartValidation;

namespace NTS.Judge.Features.Setup.UpcomingEvents;

public class UpcomingEventService
    : KrudServiceBase<UpcomingEvent, UpcomingEventFormModel>,
        IUpcomingEventService,
        ITransient
{
    readonly IRepository<UpcomingEvent> _upcomingEvents;
    readonly INotifier _notifier;

    public UpcomingEventService(IRepository<UpcomingEvent> upcomingEvents, INotifier notifier)
        : base(upcomingEvents, [])
    {
        _upcomingEvents = upcomingEvents;
        _notifier = notifier;
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

    async Task<UpcomingEvent> GetEvent(int upcomingEventId)
    {
        var upcomingEvent = await _upcomingEvents.Read(upcomingEventId);
        return upcomingEvent ?? throw GuardHelper.Exception($"Event with id '{upcomingEventId}' is not selected");
    }
}
