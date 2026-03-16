using Microsoft.AspNetCore.Components;
using Not.Blazor.Components.Abstractions;
using NTS.Application.Startlists;

namespace NTS.Blazor.Components.Startlist.Upcoming;

public class StartlistUpcomingBehind : NStatefulComponent, IDisposable
{
    static readonly TimeSpan TIMER_INTERVAL = TimeSpan.FromSeconds(1);

    System.Timers.Timer _timer = default!;

    protected string[] TableHeaders =>
        [Number_string, Athlete_string, Loops_string, Start_Time_string, Start_In_string];

    [Parameter]
    public bool Mobile { get; set; } = false;

    [Inject]
    public IStartUpcoming Service { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }

    protected override void OnInitialized()
    {
        _timer = new(TIMER_INTERVAL);
        _timer.Elapsed += OnElapsed;
        _timer.Start();
    }

    public override void Dispose()
    {
        base.Dispose();
        _timer.Elapsed -= OnElapsed;
        _timer.Dispose();
    }

    void OnElapsed(object? _, System.Timers.ElapsedEventArgs __)
    {
        Service.Refresh();
    }
}
