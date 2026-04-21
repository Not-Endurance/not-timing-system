using Microsoft.AspNetCore.Components;
using MudBlazor;
using Not.Blazor.Components.Abstractions;
using Not.Formatting;
using NTS.Application.Startlists;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Blazor.Components.Startlist.Upcoming;

public class StartlistEntryTimerBehind : NStatefulComponent
{
    protected string DisplayTime { get; private set; } = "--:--:--";

    protected Color Color { get; private set; } = Color.Success;

    protected Typo Typo { get; set; } = Typo.caption;

    [Inject]
    protected IStartUpcoming Service { get; set; } = default!;

    [Parameter, EditorRequired]
    public required Starter Entry { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }

    protected override void OnBeforeRender()
    {
        var now = DateTimeOffset.Now;
        var start = Entry.Start.ToDateTimeOffset();
        var delta = start - now;
        var displayTime = FormattingHelper.Format(delta.Duration());
        if (Entry.State == StartlistEntryState.Late)
        {
            displayTime = $"- {displayTime}";
            Color = Color.Error;
        }
        else
        {
            Color = Color.Success;
        }
        DisplayTime = displayTime;
    }
}
