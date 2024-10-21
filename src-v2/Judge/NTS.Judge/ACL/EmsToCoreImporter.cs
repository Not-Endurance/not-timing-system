using NTS.Compatibility.EMS;
using NTS.Domain.Core.Entities;
using NTS.Domain.Objects;
using Not.Serialization;
using NTS.Compatibility.EMS.Entities.EnduranceEvents;
using static NTS.Domain.Enums.OfficialRole;
using NTS.Compatibility.EMS.Entities.Competitions;
using NTS.Domain.Enums;
using NTS.Judge.ACL.Factories;
using NTS.Domain.Core.Entities.ParticipationAggregate;
using NTS.Compatibility.EMS.Enums;
using Not.Application.Ports.CRUD;
using Not.Injection;

namespace NTS.Judge.ACL;

public class EmsToCoreImporter : IEmsToCoreImporter
{
    private readonly IRepository<EnduranceEvent> _events;
    private readonly IRepository<Official> _officials;
    private readonly IRepository<Participation> _participations;
    private readonly IRepository<Ranking> _classfications;

    public EmsToCoreImporter(
        IRepository<EnduranceEvent> events,
        IRepository<Official> officials,
        IRepository<Participation> participations,
        IRepository<Ranking> classfications)
    {
        _events = events;
        _officials = officials;
        _participations = participations;
        _classfications = classfications;
    }

    public async Task Import(string emsJson, bool adjustTime = true)
    {
        var existingEvent = await _events.Read(default);
        if (existingEvent != null)
        {
            throw new Exception($"Cannot import data as Event already exists: '{existingEvent}'");
        }

        var emsState = emsJson.FromJson<EmsState>();

        var enduranceEvent = CreateEvent(emsState.Event, adjustTime);
        //TODOL interesting why some imports fail without ToList? 2024-vakarel (finished) for example
        var officials = CreateOfficials(emsState.Event).ToList();
        var (rankings, participations) = CreateRankingsAndParticipations(emsState, adjustTime);

        await _events.Create(enduranceEvent);
        foreach (var official in officials)
        {
            await _officials.Create(official);
        }
        foreach (var participation in participations)
        {
            await _participations.Create(participation);
        }
        foreach (var classification in rankings)
        {
            await _classfications.Create(classification);
        }
    }

    private EnduranceEvent CreateEvent(EmsEnduranceEvent emsEvent, bool adjustTime)
    {
        var country = new Country(emsEvent.Country.IsoCode, "zz", emsEvent.Country.Name);
        var startTime = emsEvent.Competitions.OrderBy(x => x.StartTime).First().StartTime;
        if (adjustTime)
        {
            startTime = DateTime.UtcNow.AddHours(-1);
        }
        return new EnduranceEvent(
            country,
            emsEvent.PopulatedPlace,
            "populated place",
            new DateTimeOffset(startTime),
            new DateTimeOffset(startTime + TimeSpan.FromHours(12)),
            null, null, null);
    }

    private IEnumerable<Official> CreateOfficials(EmsEnduranceEvent emsEvent)
    {
        var result = new List<Official>();
        if (emsEvent.PresidentGroundJury != null)
        {
            result.Add(new (Person.Create(emsEvent.PresidentGroundJury.Name), GroundJuryPresident));
        }
        if (emsEvent.PresidentVetCommittee != null)
        {
            result.Add(new(Person.Create(emsEvent.PresidentVetCommittee.Name), VeterinaryCommissionPresident));
        }
        if (emsEvent.FeiTechDelegate != null)
        {
            result.Add(new(Person.Create(emsEvent.FeiTechDelegate.Name), TechnicalDelegate));
        }
        if (emsEvent.FeiVetDelegate != null)
        {
            result.Add(new(Person.Create(emsEvent.FeiVetDelegate.Name), ForeignVeterinaryDelegate));
        }
        if (emsEvent.ForeignJudge != null)
        {
            result.Add(new(Person.Create(emsEvent.ForeignJudge.Name), ForeignJudge));
        }
        foreach (var jury in emsEvent.MembersOfJudgeCommittee)
        {
            result.Add(new (Person.Create(jury.Name), GroundJury));
        };
        foreach (var vet in emsEvent.MembersOfVetCommittee)
        {
            result.Add(new(Person.Create(vet.Name), VeterinaryCommission));
        }
        return result;
    }

    private (IEnumerable<Ranking>, IEnumerable<Participation>) CreateRankingsAndParticipations(EmsState state, bool adjustTime)
    {
        var result = new List<Ranking>();
        var entriesforClassification = new Dictionary<EmsCompetition, Dictionary<AthleteCategory, List<(RankingEntry entry, Participation particpation)>>>();
        foreach (var emsParticipation in state.Participations)
        {
            foreach (var competitionId in emsParticipation.CompetitionsIds)
            {
                var competition = state.Event.Competitions.First(x => x.Id == competitionId);
                var participation = ParticipationFactory.CreateCore(emsParticipation, competition, adjustTime);
                var category = EmsCategoryToAthleteCategory(emsParticipation.Participant.Athlete.Category);
                var entry = new RankingEntry(participation, !emsParticipation.Participant.Unranked);
                if (entriesforClassification.ContainsKey(competition) && entriesforClassification[competition].ContainsKey(category))
                {
                    entriesforClassification[competition][category].Add((entry, participation));
                }
                else if (entriesforClassification.ContainsKey(competition))
                {
                    entriesforClassification[competition].Add(category, new List<(RankingEntry entry, Participation particpation)> { (entry, participation) });
                }
                else
                {
                    entriesforClassification.Add(
                        competition,
                        new Dictionary<AthleteCategory, List<(RankingEntry entry, Participation particpation)>>
                        { 
                            { category, new List<(RankingEntry entry, Participation particpation)> { (entry, participation) } } 
                        });
                }

            }
        }
        foreach (var (emsCompetition, entriesByCategory) in entriesforClassification)
        {
            foreach (var (category, tuples) in entriesByCategory)
            {
                var entries = tuples.Select(x => x.entry);
                var competition = new Competition(emsCompetition.Name, CompetitionFactory.MapCompetitionRuleset(emsCompetition.Type));
                result.Add(new Ranking(competition, category, entries));
            }
            

        }
        var participations = entriesforClassification
            .Values
            .SelectMany(x => x.Values)
            .SelectMany(x => x.Select(y => y.particpation))
            .Distinct();
        return (result, participations);
    }

    private AthleteCategory EmsCategoryToAthleteCategory(EmsCategory category)
    {
        return category switch
        {
            EmsCategory.Seniors => AthleteCategory.Senior,
            EmsCategory.Children => AthleteCategory.Children,
            EmsCategory.JuniorOrYoungAdults => AthleteCategory.JuniorOrYoungAdult,
            _ => throw new NotImplementedException(),
        };
    }
}

public interface IEmsToCoreImporter : ITransientService
{
    Task Import(string filePath, bool adjustTime = true);
}