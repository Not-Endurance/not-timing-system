using NTS.Domain.Enums;

namespace NTS.Domain.Access;

/// <summary>
/// What the host may do by itself to an Event, at the stage it is in (#629, ADR-0007): the one place that names the writes
/// an Event that is no longer Live still takes. Every write a person makes is decided by <see cref="AccessPolicy"/>, which
/// refuses an Event that has ended, so the exemption of the host is a row here and nowhere else: the finalisation of the
/// placings of its Rankings, once the Event has ended and not before.
/// </summary>
public static class HostPolicy
{
    public static bool IsAllowed(HostOperation operation, EventStage stage)
    {
        return operation switch
        {
            HostOperation.RankingFinalisation => stage == EventStage.Historic,
            _ => throw new ArgumentOutOfRangeException(
                nameof(operation),
                operation,
                "A host operation the policy has no row for."
            ),
        };
    }
}
