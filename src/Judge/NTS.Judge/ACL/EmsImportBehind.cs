using Not.Application.CRUD.Ports;
using Not.Extensions;
using Not.Injection;
using Not.Safe;
using Not.Serialization;
using NTS.ACL;
using NTS.ACL.Entities.Competitions;
using NTS.ACL.Entities.EnduranceEvents;
using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;
using static NTS.Domain.Enums.OfficialRole;

namespace NTS.Judge.ACL;

public class EmsImporters : IEmsImporter
{
    readonly IRepository<EnduranceEvent> _eventRepository;
    readonly IEmsToCoreImporter _emsToCoreImporter;

    public EmsImporters(
        IRepository<EnduranceEvent> eventRepository,
        IEmsToCoreImporter emsToCoreImporter
    )
    {
        _eventRepository = eventRepository;
        _emsToCoreImporter = emsToCoreImporter;
    }

    public Task Import(string path)
    {
        Task action() => SafeImport(path);
        return SafeHelper.Run(action);
    }

    public Task ImportCore(string contents)
    {
        Task action() => SafeImportCore(contents);
        return SafeHelper.Run(action);
    }

    async Task SafeImport(string emsStateFilePath)
    {
        var contents = await File.ReadAllTextAsync(emsStateFilePath);
        var emsState = contents.FromConvertedJson<EmsState>();

        var country = new Country(
            0,
            emsState.Event.Country.IsoCode,
            null,
            emsState.Event.Country.Name
        );
        var enduranceEvent = EnduranceEvent.Create(emsState.Event.PopulatedPlace, country);

        foreach (var offical in CreateOfficials(emsState.Event))
        {
            enduranceEvent.Add(offical);
        }
        foreach (var competition in CreateCompetitions(emsState.Event))
        {
            enduranceEvent.Add(competition);
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
            var (type, ruleset) = MapRuleset(emsCompetition.Type);
            var start = emsCompetition.StartTime.ToDateTimeOffset();
            yield return Competition.Create(emsCompetition.Name, type, ruleset, start, 10);
        }

        static (CompetitionType type, CompetitionRuleset ruleset) MapRuleset(
            EmsCompetitionType emsType
        )
        {
            if (emsType == EmsCompetitionType.National)
            {
                return (CompetitionType.Qualification, CompetitionRuleset.Regional);
            }
            else if (emsType == EmsCompetitionType.International)
            {
                return (CompetitionType.Star, CompetitionRuleset.FEI);
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
            var item = Official.Create(jury.Name, GroundJury);
            result.Add(item);
        }
        ;
        foreach (var vet in emsEvent.MembersOfVetCommittee)
        {
            var item = Official.Create(vet.Name, VeterinaryCommission);
            result.Add(item);
        }
        return result;
    }
}

public interface IEmsImporter : ITransient
{
    Task Import(string emsStateFilePath);

    /// <summary>
    /// Create Core entities directly from EMS data
    /// </summary>
    /// <param name="contents">Contents of EMS data file (has to be before "EventStarted = true")</param>
    Task ImportCore(string contents);
}
