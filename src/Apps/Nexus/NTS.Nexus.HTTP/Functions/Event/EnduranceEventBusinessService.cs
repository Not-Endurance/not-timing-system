using System.Text;
using Not.Application.CRUD.Ports;
using Not.Domain.Exceptions;
using Not.Exceptions;
using Not.Injection;
using CoreEnduranceEventModel = NTS.Application.Core.EnduranceEventModel;
using CoreOfficialModel = NTS.Application.Core.OfficialModel;
using CoreParticipationModel = NTS.Application.Core.ParticipationModel;
using CoreRankingModel = NTS.Application.Core.RankingModel;
using NTS.Application.Factories;
using SetupUpcomingEventModel = NTS.Application.Setup.UpcomingEventModel;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Services.StartValidation;

namespace NTS.Nexus.HTTP.Functions.Event;

public interface IEnduranceEventBusinessService
{
    Task<CoreEnduranceEventModel> Start(int upcomingEventId);
}

public class EnduranceEventBusinessService : IEnduranceEventBusinessService, ITransient
{
    readonly IRepository<SetupUpcomingEventModel> _upcomingEvents;
    readonly IRepository<CoreEnduranceEventModel> _enduranceEvents;
    readonly IRepository<CoreOfficialModel> _officials;
    readonly IRepository<CoreParticipationModel> _participations;
    readonly IRepository<CoreRankingModel> _rankings;

    public EnduranceEventBusinessService(
        IRepository<SetupUpcomingEventModel> upcomingEvents,
        IRepository<CoreEnduranceEventModel> enduranceEvents,
        IRepository<CoreOfficialModel> officials,
        IRepository<CoreParticipationModel> participations,
        IRepository<CoreRankingModel> rankings
    )
    {
        _upcomingEvents = upcomingEvents;
        _enduranceEvents = enduranceEvents;
        _officials = officials;
        _participations = participations;
        _rankings = rankings;
    }

    public async Task<CoreEnduranceEventModel> Start(int upcomingEventId)
    {
        await EnsureEnduranceEventNotStarted(upcomingEventId);

        var setupEvent = await GetSetupEvent(upcomingEventId);
        var validation = StartValidator.Validate(setupEvent);
        var issues = validation.Data ?? [];
        if (issues.Any())
        {
            throw new DomainException(CreateStartValidationMessage(issues));
        }

        ValidateFeiConfiguration(setupEvent);

        var enduranceEvent = EnduranceEventFactory.Create(setupEvent);
        var enduranceEventModel = CoreEnduranceEventModel.From(enduranceEvent);
        await _enduranceEvents.Create(enduranceEventModel);

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

        return enduranceEventModel;
    }

    static string CreateStartValidationMessage(IReadOnlyList<StartValidationIssue> issues)
    {
        var validationBuilder = new StringBuilder()
            .AppendLine(Start_validation_invalid_setup_title_string)
            .AppendLine(Start_validation_invalid_setup_description_string);

        foreach (var issue in issues)
        {
            validationBuilder.AppendLine(
                string.Format(Start_validation_issue_different_phase_configurations_string, issue.ParticipationNumber)
            );
            foreach (var competition in issue.Competitions)
            {
                validationBuilder.AppendLine($"- {competition.CompetitionName}: {competition.PhaseSignature}");
            }
        }

        return validationBuilder.ToString().TrimEnd();
    }

    async Task<Domain.Setup.Aggregates.UpcomingEvent> GetSetupEvent(int upcomingEventId)
    {
        var upcomingEvent = await _upcomingEvents.Read(upcomingEventId);
        return upcomingEvent?.MapToEntity()
            ?? throw GuardHelper.Exception($"Event with id '{upcomingEventId}' is not selected");
    }

    async Task EnsureEnduranceEventNotStarted(int upcomingEventId)
    {
        var existing = await _enduranceEvents.Read(upcomingEventId);
        if (existing != null)
        {
            throw new DomainException(
                $"Cannot start upcoming event '{upcomingEventId}' because its endurance event is already active."
            );
        }
    }

    (
        IEnumerable<Participation> Participations,
        IEnumerable<Ranking> Rankings
    ) CreateParticipationsAndRankings(Domain.Setup.Aggregates.UpcomingEvent setupEvent)
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
        Domain.Setup.Aggregates.UpcomingEvents.Competition setupCompetition,
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

    static void ValidateFeiConfiguration(Domain.Setup.Aggregates.UpcomingEvent setupEvent)
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
