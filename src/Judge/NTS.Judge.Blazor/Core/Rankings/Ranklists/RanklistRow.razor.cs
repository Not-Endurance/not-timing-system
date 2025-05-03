using Not.Localization;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Objects;

namespace NTS.Judge.Blazor.Core.Rankings.Ranklists;

public partial class RanklistRow
{
    bool _expanded;
    PhaseCollection _phases = default!;
    Combination _combination = default!;
    Eliminated? _eliminated;
    Total? _total;
    TimeInterval? _totalInterval;
    string? _rank;

    [Parameter]
    public RankingEntry Entry { get; set; } = default!;

    protected override void OnParametersSet()
    {
        var participation = Entry.Participation;
        _phases = participation.Phases;
        _combination = participation.Combination;
        _eliminated = participation.Eliminated;
        _total = participation.GetTotal();
        _totalInterval = _total?.RideInterval + _total?.RecoveryIntervalWithoutFinal;
        _rank = Entry.Rank?.ToString();

        if (Entry.IsNotRanked)
        {
            _rank = NStrings.X_string;
            return;
        }
        if (participation.IsEliminated())
        {
            _rank = null;
            return;
        }
    }

    void Toggle()
    {
        _expanded = !_expanded;
    }
}
