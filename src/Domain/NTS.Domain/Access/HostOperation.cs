namespace NTS.Domain.Access;

/// <summary>
/// Something the host does by itself, with nobody to ask: no caller made it, so the access policy has no one to decide for
/// (#629, ADR-0007). The server refuses every write to an Event that is no longer Live, and what the host does to one is
/// named here, one operation by one, and decided by <see cref="HostPolicy"/>.
/// </summary>
public enum HostOperation
{
    /// <summary>
    /// Store the final placings of the Rankings of an Event that has ended (#640): the one write that a Historic Event
    /// takes, and the host's alone, which no route of the Api reaches.
    /// </summary>
    RankingFinalisation = 1,
}
