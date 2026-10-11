using NTS.Contracts.Core;
using NTS.Domain.Core.Aggregates;

namespace NTS.Contracts.Features.Console;

/// <summary>
/// The Rankings of a Live Event as the Main Operator changes them (#646): a Ranking of Participations it chooses is made, and
/// one is removed. A Ranking holds no placing while the Event is Live: the Results derive them (ADR-0006). What the Api refuses
/// (an Event that is not the person's to run, one that has ended) is thrown as a domain error in its own words, so that the
/// dialog that asked keeps what was typed.
/// </summary>
public interface IConsoleRankings
{
    /// <summary>Opens the Rankings of the Event the view shows: what is changed is changed in it, and the view reads them again.</summary>
    void Show(IViewedEvent view);

    /// <summary>Removes the Ranking, and with it the Results that were made from it, which are derived and not kept.</summary>
    Task Delete(Ranking ranking);
}
