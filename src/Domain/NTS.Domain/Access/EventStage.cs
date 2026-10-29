namespace NTS.Domain.Access;

/// <summary>
/// Where an Event is in its life (ADR-0007, ADR-0012): it is set up and not started, then Live until the end of its
/// last day, then Historic. The access policy takes the stage as a fact; the rule that derives it from the Event's span
/// and a clock belongs to the Event.
/// </summary>
public enum EventStage
{
    Unstarted = 0,
    Live = 1,
    Historic = 2,
}
