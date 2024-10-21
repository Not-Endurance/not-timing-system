using Not.Application.Ports.CRUD;
using Not.Domain;
using Not.Safe;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Entities;
using NTS.Domain.Enums;
using NTS.Judge.Blazor.Ports;
using Event = NTS.Domain.Core.Entities.Event;
using Official = NTS.Domain.Core.Entities.Official;
using Competition = NTS.Domain.Core.Aggregates.Participations.Competition;
using NTS.Judge.Factories;

namespace NTS.Judge.Adapters.Behinds;

public class DashboardBehind : IDashboardBehind
{
    private readonly IRepository<Domain.Setup.Entities.Event> _setupRepository;
    private readonly IRepository<Event> _coreEventRespository;
    private readonly IRepository<Official> _coreOfficialRepository;
    private readonly IRepository<Participation> _participationRepository;
    private readonly IRepository<Ranking> _rankingRepository;

    public DashboardBehind(
        IRepository<Domain.Setup.Entities.Event> setupRepository,
        IRepository<Event> coreEventRespository,
        IRepository<Official> coreOfficialRepository,
        IRepository<Participation> participationRepository,
        IRepository<Ranking> rankingRepository)
    {
        _setupRepository = setupRepository;
        _coreEventRespository = coreEventRespository;
        _coreOfficialRepository = coreOfficialRepository;
        _participationRepository = participationRepository;
        _rankingRepository = rankingRepository;
    }

    async Task SafeStart()
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
    }

    async Task CreateEvent(Domain.Setup.Entities.Event setupEvent)
    {
        var @event = CoreFactory.CreateEvent(setupEvent);
        await _coreEventRespository.Create(@event);
    }

    async Task CreateOfficials(IEnumerable<Domain.Setup.Entities.Official> setupOfficials)
    {
        foreach (var setupOfficial in setupOfficials)
        {
            var official = CoreFactory.CreateOfficial(setupOfficial);
            await _coreOfficialRepository.Create(official);
        }
    }

    async Task CreateParticipationsAndRankings(Domain.Setup.Entities.Event setupEvent) 
    {
        foreach (var competition in setupEvent.Competitions)
        {
            var participationsAndRankingEntries = CoreFactory.CreateParticipationAndRankingEntries(competition);
            var participations = participationsAndRankingEntries.Participations;
            var rankingEntriesByCategory = participationsAndRankingEntries.RankingEntriesByCategory;
            foreach (var participation in participations) 
            { 
                await _participationRepository.Create(participation);
            }
            await CreateRankings(new Competition(competition.Name, competition.Ruleset), rankingEntriesByCategory);
        }      
    }

    async Task CreateRankings(Competition competition, Dictionary<AthleteCategory, List<RankingEntry>> rankingEntriesByCategory)
    {
        foreach(var relation in rankingEntriesByCategory)
        {
            var ranking = CoreFactory.CreateRanking(competition, relation.Key, relation.Value);
            await _rankingRepository.Create(ranking);
        }
    }


    #region SafePattern

    public Task Start()
    {
        return SafeHelper.Run(SafeStart);
    }

    #endregion
}
