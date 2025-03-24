using System.ComponentModel;

namespace NTS.Domain.Enums;

public enum OfficialRole
{
    Steward = 1,

    [Description("Chief Steward")]
    ChiefSteward = 2,

    [Description("Member of Veterinary Commission ")]
    VeterinaryCommission = 3,

    [Description("President of Veterinary Commission")]
    VeterinaryCommissionPresident = 4,

    [Description("Ground Jury")]
    GroundJury = 5,

    [Description("President of Ground Jury")]
    GroundJuryPresident = 6,

    [Description("Technical Delegate")]
    TechnicalDelegate = 7,

    [Description("Foreign Judge")]
    ForeignJudge = 8,

    [Description("Foreign Veterinary Delegate")]
    ForeignVeterinaryDelegate = 9,
}
