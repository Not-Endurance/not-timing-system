using System.ComponentModel;

namespace NTS.Domain.Core.Aggregates.Participations;

public enum FtqCode
{
    /// <summary>
    /// Not respecting applicable speed restrictions
    /// </summary>
    [Description("Not respecting applicable speed restrictions")] // TODO: DisplayAttribute and ResourceType
    SP = 1,

    /// <summary>
    /// Irregular Gait
    /// </summary>
    [Description("Irregular Gait")]
    GA = 2,

    /// <summary>
    /// Metabolic issue
    /// </summary>
    [Description("Metabolic issue")]
    ME = 3,

    /// <summary>
    /// Minor injury
    /// </summary>
    [Description("Minor injury")]
    MI = 4,

    /// <summary>
    /// Serious injury (musculoskeletal)
    /// </summary>
    [Description("Serious injury (musculoskeletal)")]
    SIMUSCU = 5,

    /// <summary>
    /// Serious injury (metabolic)
    /// </summary>
    [Description("Serious injury (metabolic)")]
    SIMETA = 6,

    /// <summary>
    /// Catastrophic injury
    /// </summary>
    [Description("Catastrophic injury")]
    CI = 7,

    /// <summary>
    /// Out of time
    /// </summary>
    [Description("Out of time")]
    OT = 8,

    /// <summary>
    /// Failed to complete a Loop, but passes Horse inspection after that Loop. <seealso cref="FailedToQualify.Complement"/> is required
    /// </summary>
    [Description("Failed to complete a Loop, but passes Horse inspection after that Loop.")]
    FTC = 9,
}
