using Not.Application.CRUD.Ports;
using Not.Extensions;
using Not.Injection;
using Not.Serialization;
using NTS.ACL;
using NTS.ACL.Entities.Competitions;
using NTS.ACL.Entities.EnduranceEvents;
using NTS.ACL.Factories;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using static NTS.Domain.Enums.OfficialRole;

namespace NTS.Judge.ACL;

public class EmsToCoreImporter : IEmsToCoreImporter
{
    readonly IRepository<EnduranceEvent> _events;
    readonly IRepository<Official> _officials;
    readonly IRepository<Participation> _participations;
    readonly IRepository<Ranking> _classfications;

    public EmsToCoreImporter(
        IRepository<EnduranceEvent> events,
        IRepository<Official> officials,
        IRepository<Participation> participations,
        IRepository<Ranking> classfications
    )
    {
        _events = events;
        _officials = officials;
        _participations = participations;
        _classfications = classfications;
    }

    public async Task Import(string emsJson, bool adjustTime = true)
    {
        var existingEvent = await _events.Read(0);
        if (existingEvent != null)
        {
            throw new Exception($"Cannot import data as Event already exists: '{existingEvent}'");
        }

        var emsState = emsJson.FromConvertedJson<EmsState>();

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

    EnduranceEvent CreateEvent(EmsEnduranceEvent emsEvent, bool adjustTime)
    {
        var country = new Country(0, emsEvent.Country.IsoCode, null, emsEvent.Country.Name);
        var startTime = emsEvent.Competitions.OrderBy(x => x.StartTime).First().StartTime;
        if (adjustTime)
        {
            startTime = DateTime.UtcNow.AddHours(-1);
        }
        return new EnduranceEvent(
            DomainModelHelper.GenerateId(),
            country,
            emsEvent.PopulatedPlace,
            "populated place",
            new DateTimeOffset(startTime),
            new DateTimeOffset(startTime + TimeSpan.FromHours(12)),
            null,
            null,
            null
        );
    }

    IEnumerable<Official> CreateOfficials(EmsEnduranceEvent emsEvent)
    {
        var result = new List<Official>();
        if (emsEvent.PresidentGroundJury != null)
        {
            var item = new Official(Person.Create(emsEvent.PresidentGroundJury.Name), GroundJuryPresident);
            result.Add(item);
        }
        if (emsEvent.PresidentVetCommittee != null)
        {
            var item = new Official(Person.Create(emsEvent.PresidentVetCommittee.Name), VeterinaryCommissionPresident);
            result.Add(item);
        }
        if (emsEvent.FeiTechDelegate != null)
        {
            var item = new Official(Person.Create(emsEvent.FeiTechDelegate.Name), TechnicalDelegate);
            result.Add(item);
        }
        if (emsEvent.FeiVetDelegate != null)
        {
            var item = new Official(Person.Create(emsEvent.FeiVetDelegate.Name), ForeignVeterinaryDelegate);
            result.Add(item);
        }
        if (emsEvent.ForeignJudge != null)
        {
            var item = new Official(Person.Create(emsEvent.ForeignJudge.Name), ForeignJudge);
            result.Add(item);
        }
        foreach (var jury in emsEvent.MembersOfJudgeCommittee)
        {
            var item = new Official(Person.Create(jury.Name), GroundJury);
            result.Add(item);
        }
        ;
        foreach (var vet in emsEvent.MembersOfVetCommittee)
        {
            var item = new Official(Person.Create(vet.Name), VeterinaryCommission);
            result.Add(item);
        }
        return result;
    }

    (IEnumerable<Ranking>, IEnumerable<Participation>) CreateRankingsAndParticipations(EmsState state, bool adjustTime)
    {
        var result = new List<Ranking>();
        var entriesforClassification =
            new Dictionary<
                EmsCompetition,
                Dictionary<AthleteCategory, List<(RankingEntry entry, Participation particpation)>>
            >();
        foreach (var emsParticipation in state.Participations)
        {
            foreach (var competitionId in emsParticipation.CompetitionsIds)
            {
                var competition = state.Event.Competitions.First(x => x.Id == competitionId);
                var participation = ParticipationFactory.CreateCore(emsParticipation, competition, adjustTime);
                var category = emsParticipation.Participant.Athlete.Category.ToNtsCategory();
                var entry = new RankingEntry(participation, !emsParticipation.Participant.Unranked);
                if (
                    entriesforClassification.ContainsKey(competition)
                    && entriesforClassification[competition].ContainsKey(category)
                )
                {
                    entriesforClassification[competition][category].Add((entry, participation));
                }
                else if (entriesforClassification.ContainsKey(competition))
                {
                    entriesforClassification[competition].Add(category, [(entry, participation)]);
                }
                else
                {
                    entriesforClassification.Add(
                        competition,
                        new Dictionary<AthleteCategory, List<(RankingEntry entry, Participation particpation)>>
                        {
                            {
                                category,
                                new List<(RankingEntry entry, Participation particpation)> { (entry, participation) }
                            },
                        }
                    );
                }
            }
        }
        foreach (var (emsCompetition, entriesByCategory) in entriesforClassification)
        {
            foreach (var (category, tuples) in entriesByCategory)
            {
                const int DEFAULT_NTS_COMPETITION_TYPE = 0;
                var entries = tuples.Select(x => x.entry);
                var competition = new Competition(
                    emsCompetition.Name,
                    CompetitionFactory.MapCompetitionRuleset(emsCompetition.Type),
                    DEFAULT_NTS_COMPETITION_TYPE
                );
                result.Add(new Ranking(competition, category, entries));
            }
        }
        var participations = entriesforClassification
            .Values.SelectMany(x => x.Values)
            .SelectMany(x => x.Select(y => y.particpation))
            .Distinct();
        return (result, participations);
    }
}

public interface IEmsToCoreImporter : ITransient
{
    Task Import(string filePath, bool adjustTime = true);
}
