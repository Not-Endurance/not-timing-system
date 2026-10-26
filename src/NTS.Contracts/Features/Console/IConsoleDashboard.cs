using Not.Application.Behinds.Adapters;
using NTS.Contracts.Core;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Enums;

namespace NTS.Contracts.Features.Console;

/// <summary>
/// The dashboard of a Live Event in the Console (#646): the Participations of the Event, the one the Console Operator has selected,
/// and what is done to it: its times are edited, a time is recorded for it, a Representation or an Inspection is requested
/// for it, it is eliminated and restored. Every change is written to the Api against the version of the Participation that
/// the person saw (ADR-0013): when the Participation has changed since, the Api refuses it (409 <c>participation-changed</c>),
/// the Participation is read again and the person is told, so that what they typed is kept in the form and can be sent
/// again over what is there now. It is a view over the store of the Participations, never a copy of it.
/// </summary>
public interface IConsoleDashboard : IStatefulService
{
    /// <summary>Every Participation of the Event, by start number: an eliminated or a complete one too, as it may be restored.</summary>
    IReadOnlyList<Participation> Participations { get; }

    /// <summary>The start numbers that a time was recorded for here and that the person has not looked at since.</summary>
    IReadOnlyList<int> RecentlyTimed { get; }

    /// <summary>The Participation the person is working on; none when the Event has none.</summary>
    Participation? Selected { get; set; }

    bool IsEliminated { get; }

    /// <summary>Whether a Representation is requested in the Phase that a request made now would act on.</summary>
    bool IsRepresentRequested { get; }

    /// <summary>Whether an Inspection is requested in the Phase that a request made now would act on.</summary>
    bool IsInspectionRequested { get; }

    /// <summary>Opens the dashboard for the Event the view shows: it is refused as a write is when the view cannot write.</summary>
    Task Show(IViewedEvent view);

    /// <summary>Records a time of the day that the Console Operator names as an Arrival or a Presentation of the selected Participation.</summary>
    Task RecordTime(SnapshotType type, TimeSpan timeOfDay);

    Task RequestRepresentation(bool isRequested);

    Task RequestInspection(bool isRequested);

    Task Withdraw();

    Task Retire();

    Task FinishNotRanked(string reason);

    Task Disqualify(DisqualifyCode[] codes, string? reason);

    Task FailToQualify(FailToQualifyCode[] codes, string? reason);

    Task RestoreQualification();

    /// <summary>
    /// Disables the time events and enables the others as one change to the selected Participation, which is what the timeline's
    /// Save does (#619): the outcome of a disabled event becomes a manual reject and its time is shown no more, the time before
    /// it takes over, an enabled event is accepted again, and the Participation is evaluated once (eliminated or restored for
    /// time, the next Phase started, the Phase announced). The change is made whole or not at all: one that leaves an earlier
    /// Phase incomplete while a later one has times, or that names an event that is no longer as it was chosen, is refused with
    /// the events named, and nothing is written.
    /// </summary>
    Task ChangeTimeEvents(IReadOnlyCollection<Guid> disable, IReadOnlyCollection<Guid> enable);
}
