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
    readonly IRepository<EnduranceEvent> _coreEventRespository;
    readonly IRepository<Official> _coreOfficialRepository;
    readonly IRepository<Participation> _participationRepository;
    readonly IRepository<Ranking> _rankingRepository;

    public CoreStarter(
        IRepository<Domain.Setup.Aggregates.EnduranceEvent> setupRepository,
        IRepository<EnduranceEvent> coreEventRespository,
        IRepository<Official> coreOfficialRepository,
        IRepository<Participation> participationRepository,
        IRepository<Ranking> rankingRepository
    )
    {
        _setupRepository = setupRepository;
        _coreEventRespository = coreEventRespository;
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
        await CreateEvent(setupEvent);
        await CreateOfficials(setupEvent.Officials);
        await CreateParticipationsAndRankings(setupEvent);
        return true;
    }

    async Task CreateEvent(Domain.Setup.Aggregates.EnduranceEvent setupEvent)
    {
        var @event = EnduranceEventFactory.Create(setupEvent);
        await _coreEventRespository.Create(@event);
    }

    async Task CreateOfficials(IEnumerable<Domain.Setup.Aggregates.Official> setupOfficials)
    {
        foreach (var setupOfficial in setupOfficials)
        {
            var official = OfficialFactory.Create(setupOfficial);
            await _coreOfficialRepository.Create(official);
        }
    }

    async Task CreateParticipationsAndRankings(Domain.Setup.Aggregates.EnduranceEvent setupEvent)
    {
        foreach (var competition in setupEvent.Competitions)
        {
            var (participations, rankingEntriesByCategory) = await ParticipationAndRankingrFactory.Create(
                competition,
                _participationRepository
            );
            foreach (var participation in participations)
            {
                await _participationRepository.Create(participation);
            }
            await CreateRankings(
                new Competition(competition.Name, competition.Ruleset, competition.Type),
                rankingEntriesByCategory
            );
        }
    }

    async Task CreateRankings(
        Competition competition,
        Dictionary<AthleteCategory, List<RankingEntry>> rankingEntriesByCategory
    )
    {
        foreach (var relation in rankingEntriesByCategory)
        {
            if (relation.Value.Count > 0)
            {
                var ranking = RankingFactory.Create(competition, relation.Key, relation.Value);
                await _rankingRepository.Create(ranking);
            }
        }
    }
}

public interface ICoreStarter : ITransient
{
    Task<bool> Start();
}
