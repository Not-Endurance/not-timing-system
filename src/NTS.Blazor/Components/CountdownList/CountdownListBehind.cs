using Microsoft.AspNetCore.Components;
using Not.Blazor.Components.Abstractions;

namespace NTS.Blazor.Components.CountdownList;

public class CountdownListBehind<TItem> : NComponent, IDisposable
{
    static readonly TimeSpan TIMER_INTERVAL = TimeSpan.FromSeconds(1);

    System.Timers.Timer _timer = default!;

    [Parameter]
    public IReadOnlyList<TItem> Items { get; set; } = [];

    [Parameter]
    public RenderFragment? ColGroupContent { get; set; }

    [Parameter]
    public RenderFragment? HeaderContent { get; set; }

    [Parameter]
    public RenderFragment<TItem>? RowTemplate { get; set; }

    [Parameter]
    public EventCallback OnTick { get; set; }

    protected override void OnInitialized()
    {
        _timer = new(TIMER_INTERVAL);
        _timer.Elapsed += OnElapsed;
        _timer.Start();
    }

    public override void Dispose()
    {
        _timer.Elapsed -= OnElapsed;
        _timer.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    void OnElapsed(object? _, System.Timers.ElapsedEventArgs __)
    {
        if (OnTick.HasDelegate)
        {
            _ = InvokeAsync(OnTick.InvokeAsync);
        }
    }
}
