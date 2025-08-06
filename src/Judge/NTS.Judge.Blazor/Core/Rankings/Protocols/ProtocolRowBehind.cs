using Not.Blazor.Components;
using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Blazor.Core.Rankings.Protocols;

public class ProtocolRowBehind : NComponent
{
    [Parameter, EditorRequired]
    public RankingEntry Entry { get; set; } = default!;

    public string GetRankText()
    {
        if (Entry.Participation.IsEliminated())
        {
            return " "; //TODO: implement NText component to display string with value null as white space
        }
        return Entry.Rank?.ToString() ?? Incomplete_string;
    }
}
