using System.Text;
using MongoDB.Driver;
using Not.Application.CRUD.Ports;
using Not.Domain.Exceptions;
using Not.Exceptions;
using Not.Injection;
using Not.Structures;
using NTS.Application.Factories;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Services.StartValidation;

namespace NTS.Judge.Features;

public class StartBusinessService : IStartBusiness
{
    readonly IRepository<UpcomingEvent> _upcomingEvents;
    readonly IRepository<EnduranceEvent> _enduranceEvents;
    readonly IRepository<Official> _coreOfficialRepository;
    readonly IRepository<Participation> _participationRepository;
    readonly IRepository<Ranking> _rankingRepository;

    public StartBusinessService(
        IRepository<UpcomingEvent> upcomingEvents,
        IRepository<EnduranceEvent> coreEventRepository,
        IRepository<Official> coreOfficialRepository,
        IRepository<Participation> participationRepository,
        IRepository<Ranking> rankingRepository
    )
    {
        _upcomingEvents = upcomingEvents;
        _enduranceEvents = coreEventRepository;
        _coreOfficialRepository = coreOfficialRepository;
        _participationRepository = participationRepository;
        _rankingRepository = rankingRepository;
    }

    public async Task<Result<IReadOnlyList<StartValidationIssue>>> Validate(int upcomingEventId)
    {
        var setupEvent = await GetSetupEvent(upcomingEventId);
        return StartValidator.Validate(setupEvent);
    }

    public async Task DeleteParticipation(int upcomingEventId, int participationNumber, int competitionId)
    {
        var setupEvent = await GetSetupEvent(upcomingEventId);
        var competition = setupEvent.Competitions.FirstOrDefault(x => x.Id == competitionId);
        if (competition == null)
        {
            throw GuardHelper.Exception($"Competition with id '{competitionId}' does not exist");
        }

        var participation = competition.Participations.FirstOrDefault(x => x.Combination.Number == participationNumber);
        if (participation == null)
        {
            return;
        }

        competition.Remove(participation);
        await _upcomingEvents.Update(setupEvent);
    }

    public async Task<EnduranceEvent> CreateEnduranceEvent(int upcomingEventId)
    {
        await EnsureEnduranceEventNotStarted(upcomingEventId);

        var setupEvent = await GetSetupEvent(upcomingEventId);
        var validation = StartValidator.Validate(setupEvent);
        var issues = validation.Data ?? [];
        if (issues.Any())
        {
            throw new DomainException(CreateStartValidationMessage(issues));
        }

        // TODO: Convert this to FeiStartValidationRule (IStartValidationRule)
        ValidateFeiConfiguration(setupEvent);

        var enduranceEvent = EnduranceEventFactory.Create(setupEvent);
        await _enduranceEvents.Create(enduranceEvent);

        return enduranceEvent;
    }

    public async Task StartEnduranceEvent(int upcomingEventId)
    {
        var upcomingEvent = await GetSetupEvent(upcomingEventId);
        var officials = upcomingEvent.Officials.Select(x => OfficialFactory.Create(x, upcomingEvent.Id));
        var (participations, rankings) = CreateParticipationsAndRankings(upcomingEvent);
        foreach (var official in officials)
        {
            await _coreOfficialRepository.Create(official);
        }
        foreach (var participation in participations)
        {
            await _participationRepository.Create(participation);
        }
        foreach (var ranking in rankings)
        {
            await _rankingRepository.Create(ranking);
        }
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

    async Task<UpcomingEvent> GetSetupEvent(int upcomingEventId)
    {
        var upcomingEvent = await _upcomingEvents.Read(upcomingEventId);
        return upcomingEvent ?? throw GuardHelper.Exception($"Event with id '{upcomingEventId}' is not selected");
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

    (IEnumerable<Participation>, IEnumerable<Ranking>) CreateParticipationsAndRankings(UpcomingEvent setupEvent)
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

    Ranking CreateRanking(
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

    void ValidateFeiConfiguration(UpcomingEvent setupEvent)
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

public interface IStartBusiness : ITransient
{
    Task<Result<IReadOnlyList<StartValidationIssue>>> Validate(int upcomingEventId);
    Task DeleteParticipation(int upcomingEventId, int participationNumber, int competitionId);
    Task<EnduranceEvent> CreateEnduranceEvent(int upcomingEventId);
    Task StartEnduranceEvent(int upcomingEventId);
}
