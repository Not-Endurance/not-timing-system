using Not.Blazor.Components;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Objects;

namespace NTS.Judge.Blazor.Core.Rankings.Ranklists;

public class RanklistRowBehind : NComponent
{
    protected bool Expanded { get; private set; }
    protected PhaseCollection Phases { get; private set; } = default!;
    protected Combination Combination { get; private set; } = default!;
    protected Eliminated? Eliminated { get; private set; }
    protected Total? Total { get; private set; }
    protected TimeInterval? TotalInterval { get; private set; }
    protected string? Rank { get; private set; }

    [Parameter, EditorRequired]
    public RankingEntry Entry { get; set; } = default!;

    protected override void OnParametersSet()
    {
        var participation = Entry.Participation;
        Phases = participation.Phases;
        Combination = participation.Combination;
        Eliminated = participation.Eliminated;
        Total = participation.GetTotal();
        TotalInterval = Total?.RideInterval + Total?.RecoveryIntervalWithoutFinal;
        Rank = Entry.Rank?.ToString();

        if (Entry.IsNotRanked)
        {
            Rank = X_string;
            return;
        }
        if (participation.IsEliminated())
        {
            Rank = null;
        }
    }

    public void Toggle()
    {
        Expanded = !Expanded;
    }
}
