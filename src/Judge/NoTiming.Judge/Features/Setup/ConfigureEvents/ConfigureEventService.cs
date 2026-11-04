using Not.Application.CRUD.Ports;
using Not.Events;
using Not.Exceptions;
using Not.Injection;
using Not.Krud.Services;
using Not.Notify;
using Not.Structures;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Services.StartValidation;

namespace NTS.Judge.Features.Setup.ConfigureEvents;

public class ConfigureEventService
    : KrudServiceBase<ConfigureEvent, ConfigureEventFormModel>,
        IConfigureEventService,
        ITransient
{
    readonly IRepository<ConfigureEvent> _configureEvents;
    readonly INotifier _notifier;

    public ConfigureEventService(IRepository<ConfigureEvent> configureEvents, INotifier notifier)
        : base(configureEvents, [])
    {
        _configureEvents = configureEvents;
        _notifier = notifier;
    }

    public IEventSubscriber ObservableEvent { get; } = new Event();

    public async Task<Result<IReadOnlyList<StartValidationIssue>>> Validate(int configureEventId)
    {
        var setupEvent = await GetEvent(configureEventId);
        return StartValidator.Validate(setupEvent);
    }

    public async Task DeleteParticipation(int configureEventId, int participationNumber, int competitionId)
    {
        var setupEvent = await GetEvent(configureEventId);
        var competition = setupEvent.Competitions.FirstOrDefault(x => x.Id == competitionId);
        GuardHelper.ThrowIfDefault(competition, $"Competition with id '{competitionId}' does not exist");

        var participation = competition.Participations.FirstOrDefault(x => x.Combination.Number == participationNumber);
        if (participation == null)
        {
            return;
        }

        competition.Remove(participation);
        await _configureEvents.Update(setupEvent);
    }

    public override Task Delete(ConfigureEvent entity)
    {
        _notifier.Inform(Configure_events_cannot_be_deleted_string);
        return Task.CompletedTask;
    }

    async Task<ConfigureEvent> GetEvent(int configureEventId)
    {
        var configureEvent = await _configureEvents.Read(configureEventId);
        return configureEvent ?? throw GuardHelper.Exception($"Event with id '{configureEventId}' is not selected");
    }
}
