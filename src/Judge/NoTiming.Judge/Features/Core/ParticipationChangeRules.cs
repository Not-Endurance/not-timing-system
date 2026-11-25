using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Events;

namespace NoTiming.Judge.Features.Core;

/// <summary>
/// Tells which participation changes matter to the judge's derived state. A change arrives as the participation's new
/// state, so these rules compare it with the state before the change.
/// </summary>
internal static class ParticipationChangeRules
{
    /// <summary>A phase has completed: the number of completed phases went up.</summary>
    public static bool PhaseCompleted(ParticipationChanged change)
    {
        return change.Previous != null && CompletedPhases(change.Participation) > CompletedPhases(change.Previous);
    }

    /// <summary>
    /// The participation was eliminated or its elimination was lifted. With no known previous state, only an elimination
    /// counts: a change to a participation this judge has not seen yet is usually an arrival, which does not update rankings.
    /// </summary>
    public static bool EliminationChanged(ParticipationChanged change)
    {
        if (change.Previous == null)
        {
            return change.Participation.IsEliminated();
        }

        return change.Previous.IsEliminated() != change.Participation.IsEliminated();
    }

    static int CompletedPhases(Participation participation)
    {
        return participation.Phases.Count(x => x.IsComplete());
    }
}
