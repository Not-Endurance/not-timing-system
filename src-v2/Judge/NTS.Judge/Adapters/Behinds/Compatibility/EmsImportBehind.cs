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
    private readonly IRepository<EnduranceEvent> _eventRepository;
    private readonly IEmsToCoreImporter _emsToCoreImporter;

    public EmsImportBehind(IRepository<EnduranceEvent> eventRepository, IEmsToCoreImporter emsToCoreImporter)
    {
        _eventRepository = eventRepository;
        _emsToCoreImporter = emsToCoreImporter;
    }

    async Task SafeImport(string emsStateFilePath)
    {
        var contents = await File.ReadAllTextAsync(emsStateFilePath);
        var emsState = contents.FromJson<EmsState>();

        var country = new Country(emsState.Event.Country.IsoCode, "zz", emsState.Event.Country.Name);
        var enduranceEvent = EnduranceEvent.Create(emsState.Event.PopulatedPlace, country);

        foreach (var offical in CreateOfficials(emsState.Event))
        {
            enduranceEvent.Add(offical);
        }
        foreach (var competition in CreateCompetitions(emsState.Event))
        {
            enduranceEvent.Add(@competition);
        }

        await _eventRepository.Update(enduranceEvent);
    }

    async Task SafeImportCore(string contents)
    {
        await _emsToCoreImporter.Import(contents);
    }

    IEnumerable<Competition> CreateCompetitions(EmsEnduranceEvent emsEvent)
    {
        foreach (var emsCompetition in emsEvent.Competitions)
        {
            yield return Competition.Create(emsCompetition.Name, MapType(emsCompetition.Type), emsCompetition.StartTime.ToDateTimeOffset(), 10);
        }

        CompetitionRuleset MapType(NTS.Compatibility.EMS.Entities.Competitions.EmsCompetitionType emsType)
        {
            if (emsType == NTS.Compatibility.EMS.Entities.Competitions.EmsCompetitionType.National)
            {
                return CompetitionRuleset.Regional;
            }
            else if (emsType == NTS.Compatibility.EMS.Entities.Competitions.EmsCompetitionType.International)
            {
                return CompetitionRuleset.FEI;
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
