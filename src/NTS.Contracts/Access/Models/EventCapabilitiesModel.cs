using NTS.Domain.Access;

namespace NTS.Contracts.Access.Models;

/// <summary>
/// What the Api says a person may do about an Event: its stage, the level of access the person has to it, and the actions
/// the Api allows them (ADR-0012). The Ui keeps no rule of its own about any of it.
/// </summary>
public class EventCapabilitiesModel
{
    public EventStage Stage { get; set; }
    public AccessLevel Level { get; set; }
    public bool CanConfigure { get; set; }
    public bool CanEditSetup { get; set; }
    public bool CanRun { get; set; }
    public bool CanAssignConsoleOperators { get; set; }
    public bool CanSnapshot { get; set; }
}
