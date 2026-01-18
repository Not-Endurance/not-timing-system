using System.Text;
using Not.Application.CRUD.Ports;
using Not.Domain.Exceptions;
using Not.Injection;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Core.Start.Factories;
using NTS.Judge.Features.Warp;
using Competition = NTS.Domain.Core.Aggregates.Participations.Competition;
using Official = NTS.Domain.Core.Aggregates.Official;
using Participation = NTS.Domain.Core.Aggregates.Participation;

namespace NTS.Judge.Features.Core.Start;

public class CoreStartService : ICoreStarter
{
    readonly ISelectedEventContext _eventContext;
    readonly IRepository<EnduranceEvent> _coreEventRepository;
    readonly IRepository<Official> _coreOfficialRepository;
    readonly IRepository<Participation> _participationRepository;
    readonly IRepository<Ranking> _rankingRepository;

    public CoreStartService(
        ISelectedEventContext eventContext,
        IRepository<EnduranceEvent> coreEventRepository,
        IRepository<Official> coreOfficialRepository,
        IRepository<Participation> participationRepository,
        IRepository<Ranking> rankingRepository
    )
    {
        _eventContext = eventContext;
        _coreEventRepository = coreEventRepository;
        _coreOfficialRepository = coreOfficialRepository;
        _participationRepository = participationRepository;
        _rankingRepository = rankingRepository;
    }

    public async Task<bool> Start()
    {
        var setupEvent = _eventContext.Event;
        if (setupEvent == null)
        {
            // TODO: Create ValidationException containing localization logic and inherit form it in DomainException. Use that here instead
            throw new DomainException("Cannot start - Event is not configured");
        }

        ValidateFeiConfiguration(setupEvent);

        var enduranceEvent = EnduranceEventFactory.Create(setupEvent);
        var officials = setupEvent.Officials.Select(OfficialFactory.Create);
        var (participations, rankings) = CreateParticipationsAndRankings(setupEvent);

        await _coreEventRepository.Create(enduranceEvent);
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
        return true;
    }

    (IEnumerable<Participation>, IEnumerable<Ranking>) CreateParticipationsAndRankings(
        Domain.Setup.Aggregates.UpcomingEvent setupEvent
    )
    {
        var participations = new List<Participation>();
        var rankings = new List<Ranking>();

        foreach (var setupCompetition in setupEvent.Competitions)
        {
            var (p, rankingEntriesByCategory) = ParticipationAndRankingFactory.Create(setupCompetition, participations);
            var r = rankingEntriesByCategory.Where(x => x.Value.Any()).Select(x => CreateRanking(setupCompetition, x));

            participations.AddRange(p);
            rankings.AddRange(r);
        }
        return (participations, rankings);
    }

    Ranking CreateRanking(
        Domain.Setup.Aggregates.UpcomingEvents.Competition setupCompetition,
        KeyValuePair<ParticipationCategory, List<RankingEntry>> entriesByCategory
    )
    {
        var competition = new Competition(setupCompetition.Name, setupCompetition.Ruleset, setupCompetition.Type);
        return new Ranking(
            competition,
            entriesByCategory.Key,
            setupCompetition.FeiId,
            setupCompetition.FeiRule,
            setupCompetition.FeiScheduleNumber,
            entriesByCategory.Value
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

public interface ICoreStarter : ITransient
{
    Task<bool> Start();
}
