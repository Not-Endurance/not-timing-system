using Not.Blazor.Components.Abstractions;
using NTS.Contracts.Features.Participants;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Ui.Features.Participants;

/// <summary>
/// A Participation of a history, opened (#639): its Phases and its totals as the Results show them, and the Rankings besides
/// its own that count it, each with what it placed as there.
/// </summary>
public class ParticipationDetailBehind : NComponent
{
    protected override bool ObserveBreakpointChanges => true;

    /// <summary>The totals are beside the table on a narrow screen and above it on a wide one, as in the Results.</summary>
    protected bool UseInlineLayout => !IsMdAndDown;

    protected string EventAddress => Routes.Of(Routes.HISTORIC_EVENT_DETAILS_PAGE, Row.EventId);

    [Parameter, EditorRequired]
    public HistoricParticipation Row { get; set; } = default!;

    [Parameter, EditorRequired]
    public Participation Participation { get; set; } = default!;

    protected static string AlsoText(CountedPlacing counted)
    {
        var text = $"{Also_counted_in_string} {counted.Name} ({counted.Category})";
        return counted.Placing == null ? text : $"{text}: {PlacingText.Of(counted.Placing)}";
    }
}
