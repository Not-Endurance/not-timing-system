using NTS.Relay.ACL.Entities.Competitions;
using NTS.Relay.ACL.Entities.Countries;
using NTS.Relay.ACL.Entities.Personnels;
using NTS.Relay.ACL.Abstractions;

namespace NTS.Relay.ACL.Entities.EnduranceEvents;

public class EmsEnduranceEvent : EmsDomainBase<EmsEnduranceEventException>
{
    List<EmsPersonnel> membersOfVetCommittee = [];
    List<EmsPersonnel> membersOfJudgeCommittee = [];
    List<EmsPersonnel> stewards = [];
    List<EmsCompetition> competitions = [];

    [Newtonsoft.Json.JsonConstructor]
    EmsEnduranceEvent() { }

    internal EmsEnduranceEvent(string name, EmsCountry country, string feiId, string feiCode, string showFeiId)
        : this(name, country)
    {
        FeiId = feiId;
        FeiCode = feiCode;
        ShowFeiId = showFeiId;
    }

    internal EmsEnduranceEvent(string name, EmsCountry country)
        : base(GENERATE_ID)
    {
        Name = name;
        Country = country;
    }

    public string FeiCode { get; internal set; }
    public string ShowFeiId { get; internal set; }
    public string FeiId { get; internal set; }
    public string Name { get; internal set; }
    public string PopulatedPlace { get; internal set; }
    public bool HasStarted { get; internal set; }
    public EmsCountry Country { get; internal set; }
    public EmsPersonnel PresidentGroundJury { get; internal set; }
    public EmsPersonnel PresidentVetCommittee { get; internal set; }
    public EmsPersonnel ForeignJudge { get; internal set; }
    public EmsPersonnel FeiTechDelegate { get; internal set; }
    public EmsPersonnel FeiVetDelegate { get; internal set; }
    public EmsPersonnel ActiveVet { get; internal set; }
    public IReadOnlyList<EmsPersonnel> MembersOfVetCommittee
    {
        get => membersOfVetCommittee.AsReadOnly();
        private set => membersOfVetCommittee = value.ToList();
    }
    public IReadOnlyList<EmsPersonnel> MembersOfJudgeCommittee
    {
        get => membersOfJudgeCommittee.AsReadOnly();
        private set => membersOfJudgeCommittee = value.ToList();
    }
    public IReadOnlyList<EmsPersonnel> Stewards
    {
        get => stewards.AsReadOnly();
        private set => stewards = value.ToList();
    }
    public IReadOnlyList<EmsCompetition> Competitions
    {
        get => competitions.AsReadOnly();
        private set => competitions = value.ToList();
    }

    public void Save(EmsCompetition competition)
    {
        throw new NotImplementedException();
    }

    public void Save(EmsPersonnel personnel)
    {
        throw new NotImplementedException();
    }
}
