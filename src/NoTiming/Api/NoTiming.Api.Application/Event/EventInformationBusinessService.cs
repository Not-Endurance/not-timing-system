using System.Text;
using NC.Application.CRUD.Ports;
using NC.Domain.Exceptions;
using NC.Exceptions;
using NC.Injection;
using NoTiming.Application.Factories;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Aggregates.Results;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Setup.Services.StartValidation;
using static NC.Localization.NStrings;
using static NoTiming.Shared.Localization.NoTimingStrings;
using CoreEventInformationModel = NoTiming.Application.Contracts.Core.Models.EventInformationModel;
using CoreOfficialModel = NoTiming.Application.Contracts.Core.Models.OfficialModel;
using CoreOperatorModel = NoTiming.Application.Contracts.Core.Models.OperatorModel;
using CoreParticipationModel = NoTiming.Application.Contracts.Core.Models.ParticipationModel;
using CoreRankingModel = NoTiming.Application.Contracts.Core.Models.RankingModel;
using SetupConfigureEventModel = NoTiming.Application.Contracts.Setup.Models.ConfigureEventModel;

namespace NoTiming.Api.Application.Event;

public interface IEventInformationBusinessService
{
    Task<IEnumerable<CoreEventInformationModel>> ReadActive();
    Task<IEnumerable<CoreEventInformationModel>> ReadPast();
    Task<CoreEventInformationModel> Start(int configureEventId);
    Task Deactivate(int eventInformationId);
    Task FinalizeDueEvents();
    Task Finalize(int eventInformationId);
    Task Reopen(int eventInformationId);
}

public class EventInformationBusinessService : IEventInformationBusinessService, ITransient
{
    readonly IRepository<SetupConfigureEventModel> _configureEvents;
    readonly IRepository<CoreEventInformationModel> _eventInformation;
    readonly IRepository<CoreOfficialModel> _officials;
    readonly IRepository<CoreOperatorModel> _operators;
    readonly IRepository<CoreParticipationModel> _participations;
    readonly IRepository<CoreRankingModel> _rankings;
    readonly TimeProvider _clock;

    public EventInformationBusinessService(
        IRepository<SetupConfigureEventModel> configureEvents,
        IRepository<CoreEventInformationModel> eventInformation,
        IRepository<CoreOfficialModel> officials,
        IRepository<CoreOperatorModel> operators,
        IRepository<CoreParticipationModel> participations,
        IRepository<CoreRankingModel> rankings,
        TimeProvider clock
    )
    {
        _clock = clock;
        _configureEvents = configureEvents;
        _eventInformation = eventInformation;
        _officials = officials;
        _operators = operators;
        _participations = participations;
        _rankings = rankings;
    }

    public async Task<CoreEventInformationModel> Start(int configureEventId)
    {
        await EnsureEventInformationNotStarted(configureEventId);

        var setupEvent = await GetSetupEvent(configureEventId);
        var validation = StartValidator.Validate(setupEvent);
        var issues = validation.Data ?? [];
        if (issues.Any())
        {
            throw new DomainException(CreateStartValidationMessage(issues));
        }

        ValidateFeiConfiguration(setupEvent);

        var eventInformation = EventInformationFactory.Create(setupEvent);
        var eventInformationModel = CoreEventInformationModel.From(eventInformation);
        await _eventInformation.Create(eventInformationModel);

        var officials = setupEvent.Officials.Select(x =>
            CoreOfficialModel.MapFrom(OfficialFactory.Create(x, setupEvent.Id))
        );
        var operators = setupEvent.Operators.Select(x =>
            CoreOperatorModel.MapFrom(OperatorFactory.Create(x, setupEvent.Id))
        );
        var (participations, rankings) = CreateParticipationsAndRankings(setupEvent);

        foreach (var official in officials)
        {
            await _officials.Create(official);
        }
        foreach (var @operator in operators)
        {
            await _operators.Create(@operator);
        }
        foreach (var participation in participations.Select(CoreParticipationModel.MapFrom))
        {
            await _participations.Create(participation);
        }
        foreach (var ranking in rankings.Select(CoreRankingModel.From))
        {
            await _rankings.Create(ranking);
        }

        return eventInformationModel;
    }

    public async Task<IEnumerable<CoreEventInformationModel>> ReadActive()
    {
        var now = _clock.GetUtcNow();
        await DeactivateExpiredActiveEvents(now);
        return await _eventInformation.ReadMany(x => x.IsActive && x.EndDay > now) ?? [];
    }

    public async Task<IEnumerable<CoreEventInformationModel>> ReadPast()
    {
        var items = await _eventInformation.ReadMany(x => !x.IsActive) ?? [];
        return items.OrderByDescending(x => x.EndDay);
    }

    public async Task Deactivate(int eventInformationId)
    {
        var eventInformation = await _eventInformation.Read(eventInformationId);
        if (eventInformation == null)
        {
            return;
        }

        eventInformation.IsActive = false;
        await _eventInformation.Update(eventInformation);
    }

    // Finalizes every event whose last day has ended. Runs nightly. A finalized event is skipped, so running it twice is harmless.
    public async Task FinalizeDueEvents()
    {
        var now = _clock.GetUtcNow();
        var ended = await _eventInformation.ReadMany(x => x.EndDay < now) ?? [];
        var due = ended.Where(x => !x.IsFinalized).ToList();
        foreach (var eventInformation in due)
        {
            await Finalize(eventInformation.Id);
        }
    }

    public async Task Finalize(int eventInformationId)
    {
        var eventInformation = await _eventInformation.Read(eventInformationId);
        if (eventInformation == null || eventInformation.IsFinalized)
        {
            return;
        }

        var rankings = await _rankings.ReadMany(x => x.EventId == eventInformationId) ?? [];
        foreach (var ranking in rankings)
        {
            var placings = RankingFinalization.PlaceEntries(ranking.MapToEntity());
            foreach (var entry in ranking.Entries)
            {
                entry.Rank = placings.TryGetValue(entry.Participation.Id, out var rank) ? rank : null;
            }
            await _rankings.Update(ranking);
        }

        eventInformation.IsFinalized = true;
        await _eventInformation.Update(eventInformation);
    }

    // Reopens a finalized event and clears its placings, so they are placed again from the live results.
    public async Task Reopen(int eventInformationId)
    {
        var eventInformation = await _eventInformation.Read(eventInformationId);
        if (eventInformation == null || !eventInformation.IsFinalized)
        {
            return;
        }

        eventInformation.IsFinalized = false;
        await _eventInformation.Update(eventInformation);

        var rankings = await _rankings.ReadMany(x => x.EventId == eventInformationId) ?? [];
        foreach (var ranking in rankings)
        {
            foreach (var entry in ranking.Entries)
            {
                entry.Rank = null;
            }
            await _rankings.Update(ranking);
        }
    }

    async Task DeactivateExpiredActiveEvents(DateTimeOffset now)
    {
        var expiredEvents = await _eventInformation.ReadMany(x => x.IsActive && x.EndDay <= now) ?? [];
        foreach (var expiredEvent in expiredEvents)
        {
            expiredEvent.IsActive = false;
            await _eventInformation.Update(expiredEvent);
        }
    }

    static string CreateStartValidationMessage(IReadOnlyList<StartValidationIssue> issues)
    {
        var validationBuilder = new StringBuilder()
            .AppendLine(Start_validation_invalid_setup_title_string)
            .AppendLine(Start_validation_invalid_setup_description_string);

        foreach (var issue in issues)
        {
            validationBuilder.AppendLine(issue.Summary);
            if (!issue.IsAutoCorrectable)
            {
                continue;
            }

            foreach (var competition in issue.Competitions)
            {
                validationBuilder.AppendLine($"- {competition.CompetitionName}: {competition.PhaseSignature}");
            }
        }

        return validationBuilder.ToString().TrimEnd();
    }

    async Task<NoTiming.Domain.Setup.Aggregates.ConfigureEvent> GetSetupEvent(int configureEventId)
    {
        var configureEvent = await _configureEvents.Read(configureEventId);
        return configureEvent?.MapToEntity()
            ?? throw GuardHelper.Exception($"Event with id '{configureEventId}' is not selected");
    }

    async Task EnsureEventInformationNotStarted(int configureEventId)
    {
        var existing = await _eventInformation.Read(configureEventId);
        if (existing != null)
        {
            throw new DomainException(
                $"Cannot start configure event '{configureEventId}' because its event information is already active."
            );
        }
    }

    (IEnumerable<Participation> Participations, IEnumerable<Ranking> Rankings) CreateParticipationsAndRankings(
        NoTiming.Domain.Setup.Aggregates.ConfigureEvent setupEvent
    )
    {
        var participations = new List<Participation>();
        var rankings = new List<Ranking>();

        foreach (var setupCompetition in setupEvent.Competitions)
        {
            var (p, rankingEntriesByCategory) = ParticipationAndRankingFactory.Create(
                setupCompetition,
                participations,
                setupEvent.Id
            );
            var r = rankingEntriesByCategory
                .Where(x => x.Value.Any())
                .Select(x => CreateRanking(setupCompetition, x, setupEvent.Id));

            participations.AddRange(p);
            rankings.AddRange(r);
        }
        return (participations, rankings);
    }

    static Ranking CreateRanking(
        NoTiming.Domain.Setup.Aggregates.ConfigureEvents.Competition setupCompetition,
        KeyValuePair<ParticipationCategory, List<RankingEntry>> entriesByCategory,
        int eventId
    )
    {
        return new Ranking(
            setupCompetition.Name,
            setupCompetition.EnglishNames,
            entriesByCategory.Key,
            setupCompetition.FeiEventId,
            setupCompetition.FeiEventCode,
            setupCompetition.FeiCompetitionId,
            setupCompetition.FeiRule,
            setupCompetition.FeiScheduleNumber,
            entriesByCategory.Value,
            eventId
        );
    }

    static void ValidateFeiConfiguration(NoTiming.Domain.Setup.Aggregates.ConfigureEvent setupEvent)
    {
        if (!HasAnyFeiConfiguration(setupEvent))
        {
            return;
        }

        var validationBuilder = new StringBuilder();
        if (string.IsNullOrWhiteSpace(setupEvent.FeiShowId))
        {
            validationBuilder.AppendLine(FEI_Show_ID_string);
        }
        foreach (var competition in setupEvent.Competitions.Where(HasAnyCompetitionFeiConfiguration))
        {
            if (string.IsNullOrWhiteSpace(competition.FeiEventId))
            {
                validationBuilder.AppendLine($"{competition.Name}: {FEI_Event_ID_string}");
            }
            if (string.IsNullOrWhiteSpace(competition.FeiEventCode))
            {
                validationBuilder.AppendLine($"{competition.Name}: {FEI_Event_Code_string}");
            }
            if (string.IsNullOrWhiteSpace(competition.FeiCompetitionId))
            {
                validationBuilder.AppendLine($"{competition.Name}: {FEI_Competition_ID_string}");
            }
            if (string.IsNullOrWhiteSpace(competition.FeiRule))
            {
                validationBuilder.AppendLine($"{competition.Name}: {FEI_Rule_string}");
            }
            if (string.IsNullOrWhiteSpace(competition.FeiScheduleNumber))
            {
                validationBuilder.AppendLine($"{competition.Name}: {FEI_Schedule_Number_string}");
            }
            foreach (var participation in competition.Participations)
            {
                if (string.IsNullOrWhiteSpace(participation.Combination.Horse.FeiId))
                {
                    validationBuilder.AppendLine(
                        $"#{participation.Combination.Number}, {participation.Combination.Horse.Name}: {FEI_ID_string}"
                    );
                }
                if (string.IsNullOrWhiteSpace(participation.Combination.Athlete.FeiId))
                {
                    var name = participation.Combination.Athlete.Name;
                    validationBuilder.AppendLine($"#{participation.Combination.Number}, {name}: {FEI_ID_string}");
                }
            }
        }
        var validation = validationBuilder.ToString();
        if (!string.IsNullOrWhiteSpace(validation))
        {
            var message = string.Format(Missing_FEI_export_configurations_colon__, Environment.NewLine + validation);
            throw new DomainException(message);
        }
    }

    static bool HasAnyFeiConfiguration(NoTiming.Domain.Setup.Aggregates.ConfigureEvent setupEvent)
    {
        return !string.IsNullOrWhiteSpace(setupEvent.FeiShowId)
            || setupEvent.Competitions.Any(HasAnyCompetitionFeiConfiguration);
    }

    static bool HasAnyCompetitionFeiConfiguration(
        NoTiming.Domain.Setup.Aggregates.ConfigureEvents.Competition competition
    )
    {
        return !string.IsNullOrWhiteSpace(competition.FeiEventId)
            || !string.IsNullOrWhiteSpace(competition.FeiEventCode)
            || !string.IsNullOrWhiteSpace(competition.FeiCompetitionId)
            || !string.IsNullOrWhiteSpace(competition.FeiRule)
            || !string.IsNullOrWhiteSpace(competition.FeiScheduleNumber);
    }
}
