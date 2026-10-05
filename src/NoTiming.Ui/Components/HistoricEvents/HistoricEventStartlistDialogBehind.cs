using Microsoft.AspNetCore.Components;
using Not.Blazor.Dialogs.Abstractions;
using NTS.Domain.Core.Objects.Startlists;

namespace NoTiming.Ui.Components.HistoricEvents;

public class HistoricEventStartlistDialogBehind : NDialog
{
    [Parameter]
    public IReadOnlyDictionary<int, IReadOnlyList<Starter>> HistoryByStage { get; set; } =
        new Dictionary<int, IReadOnlyList<Starter>>();
}
