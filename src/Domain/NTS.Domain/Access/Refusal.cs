namespace NTS.Domain.Access;

/// <summary>
/// Why an action is refused. The role that is missing comes before the stage of the Event that does not take the
/// action, so a person who may not do it never learns anything but that. The Api gives each a status and a stable code.
/// </summary>
public enum Refusal
{
    NotSignedIn = 1,
    NotAllowed = 2,
    NotTenantRoot = 3,
    NotMainOperator = 4,
    NotDeveloper = 5,
    TenantNotOperational = 6,

    /// <summary>The action needs a started Event and this one is not started yet.</summary>
    EventNotStarted = 7,

    /// <summary>The action needs an Event that is not started and this one is Live.</summary>
    EventStarted = 8,

    /// <summary>The Event is Historic, and nothing about one can be changed.</summary>
    EventEnded = 9,
}
