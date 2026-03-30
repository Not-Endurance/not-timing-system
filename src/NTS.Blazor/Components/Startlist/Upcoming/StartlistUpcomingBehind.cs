using Microsoft.AspNetCore.Components;
using Not.Blazor.Components.Abstractions;
using NTS.Application.Startlists;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Blazor.Components.Startlist.Upcoming;

public class StartlistUpcomingBehind : NStatefulComponent, IDisposable
{
    static readonly TimeSpan TIMER_INTERVAL = TimeSpan.FromSeconds(1);

    System.Timers.Timer _timer = default!;

    protected string NumberAndLoopHeader => $"{Number_string} / {Loops_string}";

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

    protected string FormatAthlete(Starter entry)
    {
        return entry.Athlete.ToString();
    }

    protected string FormatLoop(Starter entry)
    {
        return $"{entry.Distance:0.##}{km_string}";
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
