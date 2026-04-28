using Not.Blazor.Dialogs.Abstractions;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Judge.Blazor.Features.PastEvents;

public class PastEventStartlistDialogBehind : NDialog
{
    [Parameter]
    public IReadOnlyDictionary<int, IReadOnlyList<Starter>> HistoryByStage { get; set; } =
        new Dictionary<int, IReadOnlyList<Starter>>();
}
