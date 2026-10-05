namespace NTS.Domain.Core.Aggregates.Participations.Entities;

/// <summary>What became of a time that reached a Phase (ADR-0005): it set a time, or it was set aside and why.</summary>
public enum TimeEventOutcome
{
    Accepted = 1,

    /// <summary>The Phase has its Arrive time already.</summary>
    RejectedDuplicateArrive = 2,

    /// <summary>A Representation was requested and the Phase has its Present and its Represent times already.</summary>
    RejectedDuplicatePresent = 3,

    /// <summary>The final Phase is complete, so the Participation takes no more times.</summary>
    RejectedParticipationComplete = 4,

    /// <summary>The finish line is separate, and a final time was taken at a Phase that is not the last.</summary>
    RejectedSeparateStageLine = 5,

    /// <summary>The finish line is separate, and the arrival at the last Phase was taken at the stage line.</summary>
    RejectedSeparateFinishLine = 6,

    /// <summary>The time breaks the order Start, Arrive, Presentation, Representation, among the times that exist.</summary>
    RejectedInvalidTime = 7,

    /// <summary>The main Operator disabled the event.</summary>
    RejectedManually = 8,
}
