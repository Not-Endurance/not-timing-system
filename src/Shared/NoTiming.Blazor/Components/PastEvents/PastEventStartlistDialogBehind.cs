using Microsoft.AspNetCore.Components;
using NC.Blazor.Dialogs.Abstractions;
using NoTiming.Domain.Core.Objects.Startlists;

namespace NoTiming.Blazor.Components.PastEvents;

public class PastEventStartlistDialogBehind : NDialog
{
    [Parameter]
    public IReadOnlyDictionary<int, IReadOnlyList<Starter>> HistoryByStage { get; set; } =
        new Dictionary<int, IReadOnlyList<Starter>>();
}
