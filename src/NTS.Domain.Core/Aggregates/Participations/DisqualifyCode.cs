using System.ComponentModel;

namespace NTS.Domain.Core.Aggregates.Participations;

public enum DisqualifyCode
{
    [Description("Underweight")] // TODO: DisplayAttribute and ResourceType
    UW = 1,

    [Description("Late Presentation")]
    LP = 2,

    [Description("Hyposensitivity")]
    HYPO = 3,

    [Description("Horse Abuse")]
    HA = 4,

    [Description("Horse Not Presented")]
    HNP = 5,

    [Description("Specify custom reason for disqualification")]
    other = 6,
}
