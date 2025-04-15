using Not.Application.CRUD.Ports;
using Not.Domain.Exceptions;
using Not.Injection;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Enums;
using NTS.Judge.Core.Start.Factories;

namespace NTS.Judge.Core.Start;

public class CoreStarter : ICoreStarter
{
    readonly IRepository<Domain.Setup.Aggregates.EnduranceEvent> _setupRepository;
    readonly IRepository<EnduranceEvent> _coreEventRepository;
    readonly IRepository<Official> _coreOfficialRepository;
    readonly IRepository<Participation> _participationRepository;
    readonly IRepository<Ranking> _rankingRepository;

    public CoreStarter(
        IRepository<Domain.Setup.Aggregates.EnduranceEvent> setupRepository,
        IRepository<EnduranceEvent> coreEventRepository,
        IRepository<Official> coreOfficialRepository,
        IRepository<Participation> participationRepository,
        IRepository<Ranking> rankingRepository
    )
    {
        _setupRepository = setupRepository;
        _coreEventRepository = coreEventRepository;
        _coreOfficialRepository = coreOfficialRepository;
        _participationRepository = participationRepository;
        _rankingRepository = rankingRepository;
    }

    public async Task<bool> Start()
    {
        var setupEvent = await _setupRepository.Read(0);
        if (setupEvent == null)
        {
            // TODO: Create ValidationException containing localization logic and inherit form it in DomainException. Use that here instead
            throw new DomainException("Cannot start - Event is not configured");
        }

        var enduranceEvent = EnduranceEventFactory.Create(setupEvent);
        var officials = setupEvent.Officials.Select(OfficialFactory.Create);
        var (participations, rankings) =  CreateParticipationsAndRankings(setupEvent);
        
        await _coreEventRepository.Create(enduranceEvent);
        foreach (var official in officials)
        {
            await  _coreOfficialRepository.Create(official);
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

    (IEnumerable<Participation>, IEnumerable<Ranking>) CreateParticipationsAndRankings(Domain.Setup.Aggregates.EnduranceEvent setupEvent)
    {
        var participations = new List<Participation>();
        var rankings = new List<Ranking>();
        
        foreach (var setupCompetition in setupEvent.Competitions)
        {
            var (p, rankingEntriesByCategory) = ParticipationAndRankingFactory.Create(
                setupCompetition,
                participations
            );
            var r = rankingEntriesByCategory
                .Where(x => x.Value.Any())
                .Select(x => CreateRanking(setupCompetition, x));
            
            participations.AddRange(p);
            rankings.AddRange(r);
        }
        return (participations, rankings);
    }

    Ranking CreateRanking(
        Domain.Setup.Aggregates.Competition setupCompetition,
        KeyValuePair<AthleteCategory, List<RankingEntry>> entriesByCategory
    )
    {
        var competition = new Competition(setupCompetition.Name, setupCompetition.Ruleset, setupCompetition.Type);
        return RankingFactory.Create(
            competition,
            entriesByCategory.Key,
            entriesByCategory.Value,
            setupCompetition.FeiRule,
            setupCompetition.FeiEventCode,
            setupCompetition.FeiScheduleNumber,
            setupCompetition.FeiCategoryEventNumber
        );
    }
}

public interface ICoreStarter : ITransient
{
    Task<bool> Start();
}
