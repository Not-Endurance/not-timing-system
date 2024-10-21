using Not.Application.Ports.CRUD;
using Not.Extensions;
using Not.Safe;
using Not.Serialization;
using NTS.Compatibility.EMS;
using NTS.Compatibility.EMS.Entities.EnduranceEvents;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Entities;
using NTS.Judge.ACL;
using NTS.Judge.Blazor.Ports;
using static NTS.Domain.Enums.OfficialRole;

namespace NTS.Judge.Adapters.Behinds.Compatibility;

public class EmsImportBehind : IEmsImportBehind
{
    private readonly IRepository<Event> _eventRepository;
    private readonly IEmsToCoreImporter _emsToCoreImporter;

    public EmsImportBehind(IRepository<Event> eventRepository, IEmsToCoreImporter emsToCoreImporter)
    {
        _eventRepository = eventRepository;
        _emsToCoreImporter = emsToCoreImporter;
    }

    async Task SafeImport(string emsStateFilePath)
    {
        var contents = await File.ReadAllTextAsync(emsStateFilePath);
        var emsState = contents.FromJson<EmsState>();

        var country = new Country(emsState.Event.Country.IsoCode, "zz", emsState.Event.Country.Name);
        var @event = Event.Create(emsState.Event.PopulatedPlace, country);

        foreach (var offical in CreateOfficials(emsState.Event))
        {
            @event.Add(offical);
        }
        foreach (var competition in CreateCompetitions(emsState.Event))
        {
            @event.Add(@competition);
        }

        await _eventRepository.Update(@event);
    }

    async Task SafeImportCore(string contents)
    {
        await _emsToCoreImporter.Import(contents);
    }

    IEnumerable<Competition> CreateCompetitions(EmsEnduranceEvent emsEvent)
    {
        foreach (var emsCompetition in emsEvent.Competitions)
        {
            var competitionType = MapRuleset(emsCompetition.Type);
            yield return Competition.Create(emsCompetition.Name, competitionType.ruleset, competitionType.type ,emsCompetition.StartTime.ToDateTimeOffset(), 10);
        }

        (CompetitionRuleset ruleset, CompetitionType type) MapRuleset(NTS.Compatibility.EMS.Entities.Competitions.EmsCompetitionType emsType)
        {
            if (emsType == NTS.Compatibility.EMS.Entities.Competitions.EmsCompetitionType.National)
            {
                return (CompetitionRuleset.Regional, CompetitionType.Qualification);
            }
            else if (emsType == NTS.Compatibility.EMS.Entities.Competitions.EmsCompetitionType.International)
            {
                return (CompetitionRuleset.FEI, CompetitionType.Star);
            }
            throw new Exception();
        }
    }

    IEnumerable<Official> CreateOfficials(EmsEnduranceEvent emsEvent)
    {
        var result = new List<Official>
        {
            Official.Create(emsEvent.PresidentGroundJury.Name, GroundJuryPresident),
            Official.Create(emsEvent.PresidentVetCommittee.Name, VeterinaryCommissionPresident),
            Official.Create(emsEvent.FeiTechDelegate.Name, TechnicalDelegate),
            Official.Create(emsEvent.FeiVetDelegate.Name, ForeignVeterinaryDelegate),
            Official.Create(emsEvent.ForeignJudge.Name, ForeignJudge),
        };
        foreach (var jury in emsEvent.MembersOfJudgeCommittee)
        {
            result.Add(Official.Create(jury.Name, GroundJury));
        };
        foreach (var vet in emsEvent.MembersOfVetCommittee)
        {
            result.Add(Official.Create(vet.Name, VeterinaryCommission));
        }
        return result;
    }

    #region SafePattern

    public Task Import(string path)
    {
        return SafeHelper.Run(() => SafeImport(path));
    }

    public Task ImportCore(string contents)
    {
        return SafeHelper.Run(() => SafeImportCore(contents));
    }

    #endregion
}
