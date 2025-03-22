using System.ComponentModel;

namespace NTS.Judge.Blazor.Nexus;

public enum InquiryType
{
    [Description("Horse ID")]
    HorseId = 1,

    [Description("Horse name")]
    HorseName = 3,

    [Description("Athlete ID")]
    AthleteId = 2,

    [Description("Athlete name")]
    AthleteName = 4,

    [Description("Official ID")]
    OfficialId = 5,

    [Description("Horse name")]
    OfficialName = 6,
}
