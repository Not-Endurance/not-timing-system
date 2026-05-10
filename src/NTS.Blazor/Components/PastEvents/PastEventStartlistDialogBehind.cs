using Microsoft.AspNetCore.Components;
using Not.Blazor.Dialogs.Abstractions;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Blazor.Components.PastEvents;

public class PastEventStartlistDialogBehind : NDialog
{
    [Parameter]
    public IReadOnlyDictionary<int, IReadOnlyList<Starter>> HistoryByStage { get; set; } =
        new Dictionary<int, IReadOnlyList<Starter>>();
}
