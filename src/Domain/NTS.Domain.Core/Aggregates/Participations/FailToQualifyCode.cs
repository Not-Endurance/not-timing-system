using System.ComponentModel;

namespace NTS.Domain.Core.Aggregates.Participations;

public enum FailToQualifyCode
{
    [Description("Not respecting applicable speed restrictions")] // TODO: DisplayAttribute and ResourceType
    SP = 1,

    [Description("Irregular Gait")]
    GA = 2,

    [Description("Metabolic issue")]
    ME = 3,

    [Description("Minor injury")]
    MI = 4,

    [Description("Serious injury (musculoskeletal)")]
    SIMUSCU = 5,

    [Description("Serious injury (metabolic)")]
    SIMETA = 6,

    [Description("Catastrophic injury")]
    CI = 7,

    [Description("Out of time")]
    OT = 8,

    [Description("Failed to complete a Loop, but passes Horse inspection after that Loop.")]
    FTC = 9,
}
