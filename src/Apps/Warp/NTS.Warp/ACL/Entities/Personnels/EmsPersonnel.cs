using NTS.Warp.ACL.Abstractions;

namespace NTS.Warp.ACL.Entities.Personnels;

public class EmsPersonnel : EmsDomainBase<EmsPersonnelException>, IEmsPersonnelState
{
    [Newtonsoft.Json.JsonConstructor]
    EmsPersonnel() { }

    public EmsPersonnel(IEmsPersonnelState state)
        : base(GENERATE_ID)
    {
        Name = Validator.IsFullName(state.Name);
        Role = Validator.IsRequired(state.Role, nameof(state.Role));
    }

    public string Name { get; private set; }
    public PersonnelRole Role { get; private set; }
}

public enum PersonnelRole
{
    Invalid = 0,
    PresidentGroundJury = 1,
    PresidentVetCommission = 2,
    ForeignJudge = 3,
    FeiTechDelegate = 4,
    FeiVetDelegate = 5,
    ActiveVet = 6,
    MemberOfVetCommittee = 10,
    MemberOfJudgeCommittee = 11,
    Steward = 12,
}
