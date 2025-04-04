using System.ComponentModel;

namespace NTS.Domain.Enums;

public enum OfficialRole
{
    [Description("Steward")]
    Steward = 1,

    [Description("Chief Steward")]
    ChiefSteward = 2,

    [Description("Veterinary Commission Member")]
    VeterinaryCommissionMember = 3,

    [Description("President Veterinary Commission")]
    VeterinaryCommissionPresident = 4,

    [Description("Ground Jury Member")]
    GroundJury = 5,

    [Description("Ground Jury President")]
    GroundJuryPresident = 6,

    [Description("Technical Delegate")]
    TechnicalDelegate = 7,

    [Description("Foreign Judge")]
    ForeignJudge = 8,

    [Description("Foreign Veterinary Delegate")]
    ForeignVeterinaryDelegate = 9,

    [Description("President Treating Veterinary Commission")]
    PresidentTreatingVeterinaryCommission = 10,

    [Description("Treating Veterinary Commission Member")]
    TreatingVeterinaryCommissionMember = 11,

    [Description("Veterinary Service Member")]
    VeterinaryServiceMember = 12,
}
