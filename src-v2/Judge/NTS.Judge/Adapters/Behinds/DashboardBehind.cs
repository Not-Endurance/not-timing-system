using Not.Application.Ports.CRUD;
using Not.Domain;
using Not.Notifier;
using Not.Safe;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Entities;
using NTS.Domain.Enums;
using NTS.Judge.Blazor.Ports;
using Event = NTS.Domain.Core.Entities.Event;
using Official = NTS.Domain.Core.Entities.Official;
using Phase = NTS.Domain.Core.Aggregates.Participations.Phase;
using Competition = NTS.Domain.Core.Aggregates.Participations.Competition;

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
            throw new DomainException("Cannot start - event is not configured");
        }
        await CreateEvent(setupEvent);
        await CreateOfficials(setupEvent.Officials);
        await CreateParticipations(setupEvent);
    }

    async Task CreateEvent(Domain.Setup.Entities.Event setupEvent)
    {
        if (!setupEvent.Competitions.Any())
        {
            NotifyHelper.Warn("Cannot start Endurance event: there are no competitions configured");
            return;
        }
        var competitionStartTimes = setupEvent.Competitions.Select(x => x.StartTime);
        var startDate = competitionStartTimes.First();
        var endDate = competitionStartTimes.Last();

        var @event = new Event(setupEvent.Country, setupEvent.Place, "", startDate, endDate, null, null, null); // TODO: fix city and place
        await _coreEventRespository.Create(@event);
    }

    async Task CreateOfficials(IEnumerable<Domain.Setup.Entities.Official> setupOfficials)
    {
        foreach (var setupOfficial in setupOfficials)
        {
            var official = new Official(setupOfficial.Person, setupOfficial.Role);
            await _coreOfficialRepository.Create(official);
        }
    }

    async Task CreateParticipations(Domain.Setup.Entities.Event setupEvent) 
    {
        foreach(var competition in setupEvent.Competitions)
        {
            var setupPhases = competition.Phases;
            var competitionDistance = 0m;
            var phases = new List<Phase>();
            var categoryEntriesPairs = new Dictionary<AthleteCategory, List<RankingEntry>>
            {
                { AthleteCategory.Senior, new List<RankingEntry>() },
                { AthleteCategory.Children, new List<RankingEntry>() },
                { AthleteCategory.JuniorOrYoungAdult, new List<RankingEntry>() },
                { AthleteCategory.Training, new List<RankingEntry>() }
            };
            foreach(var phase in setupPhases)
            {
                phases.Add(new Phase(phase.Loop!.Distance, phase.Recovery, phase.Rest, competition.Ruleset, setupPhases.Last() == phase, competition.CriRecovery));
                competitionDistance += (decimal)phase.Loop!.Distance;
            }
            foreach(var contestant in competition.Contestants)
            {
                var combination = contestant.Combination;
                var maxSpeed = (double?)null;
                var minSpeed = (double?)null;
                //consider adding magic ints to StaticOptions or constants
                if(competition.Type == CompetitionType.Qualification)
                {
                    minSpeed = 10;
                    maxSpeed = 16;
                }
                if(combination.Athlete.Category == AthleteCategory.Children)
                {
                    minSpeed = 8;
                    maxSpeed = 12;
                }
                maxSpeed = contestant.MaxSpeedOverride != null ? contestant.MaxSpeedOverride : maxSpeed;
                var tandem = new Tandem(combination.Number, combination.Athlete.Person, combination.Horse.Name, competitionDistance, combination.Athlete.Country, combination.Athlete.Club, minSpeed, maxSpeed);
                var participation = new Participation(competition.Name, competition.Ruleset, tandem, phases);
                await _participationRepository.Create(participation);
                var rankingEntry = new RankingEntry(participation, !contestant.IsUnranked);
                foreach(var pair in categoryEntriesPairs)
                {
                    if(pair.Key == combination.Athlete.Category)
                    {
                        pair.Value.Add(rankingEntry);
                    }
                }
            }
            await CreateRankings(new Competition(competition.Name, competition.Ruleset), categoryEntriesPairs);
        }
    }

    async Task CreateRankings(Competition competition, Dictionary<AthleteCategory, List<RankingEntry>> categoryEntriesPairs)
    {
        foreach(var relation in categoryEntriesPairs)
        {
            var ranking = new Ranking(competition, relation.Key, relation.Value);
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
