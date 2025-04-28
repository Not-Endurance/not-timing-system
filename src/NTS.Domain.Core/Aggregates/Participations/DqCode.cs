using System.ComponentModel;

namespace NTS.Domain.Core.Aggregates.Participations;

public enum DqCode
{
    /// <summary>
    /// Underweight
    /// </summary>
    [Description("Underweight")] // TODO: DisplayAttribute and ResourceType
    UW = 1,

    /// <summary>
    /// Late Presentation
    /// </summary>
    [Description("Late Presentation")]
    LP = 2,

    /// <summary>
    /// Hyposensitivity
    /// </summary>
    [Description("Hyposensitivity")]
    HYPO = 3,

    /// <summary>
    /// Horse Abuse
    /// </summary>
    [Description("Horse Abuse")]
    HA = 4,

    /// <summary>
    /// Horse Not Presented
    /// </summary>
    [Description("Horse Not Presented")]
    HNP = 5,

    /// <summary>
    /// Specify custom reason for disqualification
    /// </summary>
    [Description("Specify custom reason for disqualification")]
    other = 6,
}
