using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;

namespace NTS.Judge.Blazor.Core.Rankings.Protocols;

public partial class ProtocolRow
{
    Combination _combination = default!;
    PhaseCollection _phases = default!;
    Total? _total;
    string? _rankText;

    [Parameter, EditorRequired]
    public RankingEntry Entry { get; set; } = default!;

    protected override void OnParametersSet()
    {
        _rankText = Entry.Rank?.ToString() ?? Incomplete_string;
        _combination = Entry.Participation.Combination;
        _phases = Entry.Participation.Phases;
        _total = Entry.Participation.GetTotal();
    }
}
