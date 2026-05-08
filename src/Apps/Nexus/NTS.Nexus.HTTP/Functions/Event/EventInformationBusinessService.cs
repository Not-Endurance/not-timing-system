using System.Text;
using Not.Application.CRUD.Ports;
using Not.Domain.Exceptions;
using Not.Exceptions;
using Not.Injection;
using NTS.Application.Factories;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Services.StartValidation;
using CoreEventInformationModel = NTS.Application.Contracts.Core.Models.EventInformationModel;
using CoreOfficialModel = NTS.Application.Contracts.Core.Models.OfficialModel;
using CoreParticipationModel = NTS.Application.Contracts.Core.Models.ParticipationModel;
using CoreRankingModel = NTS.Application.Contracts.Core.Models.RankingModel;
using SetupConfigureEventModel = NTS.Application.Contracts.Setup.Models.ConfigureEventModel;

namespace NTS.Nexus.HTTP.Functions.Event;

public interface IEventInformationBusinessService
{
    Task<CoreEventInformationModel> Start(int configureEventId);
}

public class EventInformationBusinessService : IEventInformationBusinessService, ITransient
{
    readonly IRepository<SetupConfigureEventModel> _configureEvents;
    readonly IRepository<CoreEventInformationModel> _eventInformation;
    readonly IRepository<CoreOfficialModel> _officials;
    readonly IRepository<CoreParticipationModel> _participations;
    readonly IRepository<CoreRankingModel> _rankings;

    public EventInformationBusinessService(
        IRepository<SetupConfigureEventModel> configureEvents,
        IRepository<CoreEventInformationModel> eventInformation,
        IRepository<CoreOfficialModel> officials,
        IRepository<CoreParticipationModel> participations,
        IRepository<CoreRankingModel> rankings
    )
    {
        _configureEvents = configureEvents;
        _eventInformation = eventInformation;
        _officials = officials;
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
        var (participations, rankings) = CreateParticipationsAndRankings(setupEvent);

        foreach (var official in officials)
        {
            await _officials.Create(official);
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

    async Task<Domain.Setup.Aggregates.ConfigureEvent> GetSetupEvent(int configureEventId)
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
        Domain.Setup.Aggregates.ConfigureEvent setupEvent
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
        Domain.Setup.Aggregates.ConfigureEvents.Competition setupCompetition,
        KeyValuePair<ParticipationCategory, List<RankingEntry>> entriesByCategory,
        int eventId
    )
    {
        return new Ranking(
            setupCompetition.Name,
            setupCompetition.Ruleset,
            setupCompetition.Type,
            entriesByCategory.Key,
            setupCompetition.FeiId,
            setupCompetition.FeiRule,
            setupCompetition.FeiScheduleNumber,
            entriesByCategory.Value,
            eventId
        );
    }

    static void ValidateFeiConfiguration(Domain.Setup.Aggregates.ConfigureEvent setupEvent)
    {
        if (
            !string.IsNullOrWhiteSpace(setupEvent.FeiId)
            || !string.IsNullOrWhiteSpace(setupEvent.ShowFeiId)
            || !string.IsNullOrWhiteSpace(setupEvent.FeiEventCode)
        )
        {
            var validationBuilder = new StringBuilder();
            if (string.IsNullOrWhiteSpace(setupEvent.FeiId))
            {
                validationBuilder.AppendLine(FEI_ID);
            }
            if (string.IsNullOrWhiteSpace(setupEvent.FeiEventCode))
            {
                validationBuilder.AppendLine(FEI_Event_Code);
            }
            if (string.IsNullOrWhiteSpace(setupEvent.ShowFeiId))
            {
                validationBuilder.AppendLine(FEI_Show_ID);
            }
            foreach (var competition in setupEvent.Competitions)
            {
                if (
                    string.IsNullOrWhiteSpace(competition.FeiId)
                    && string.IsNullOrWhiteSpace(competition.FeiRule)
                    && string.IsNullOrWhiteSpace(competition.FeiScheduleNumber)
                )
                {
                    continue;
                }
                if (string.IsNullOrWhiteSpace(competition.FeiRule))
                {
                    validationBuilder.AppendLine($"{competition.Name}: {FEI_Rule}");
                }
                if (string.IsNullOrWhiteSpace(competition.FeiScheduleNumber))
                {
                    validationBuilder.AppendLine($"{competition.Name}: {FEI_Schedule_NR}");
                }
                foreach (var participation in competition.Participations)
                {
                    if (string.IsNullOrWhiteSpace(participation.Combination.Horse.FeiId))
                    {
                        validationBuilder.AppendLine(
                            $"#{participation.Combination.Number}, {participation.Combination.Horse.Name}: {FEI_ID}"
                        );
                    }
                    if (string.IsNullOrWhiteSpace(participation.Combination.Athlete.FeiId))
                    {
                        var name = string.Join(' ', participation.Combination.Athlete.Names);
                        validationBuilder.AppendLine($"#{participation.Combination.Number}, {name}: {FEI_ID}");
                    }
                }
            }
            var validation = validationBuilder.ToString();
            if (!string.IsNullOrWhiteSpace(validation))
            {
                var message = string.Format(
                    Missing_FEI_export_configurations_colon__,
                    Environment.NewLine + validation
                );
                throw new DomainException(message);
            }
        }
    }
}
