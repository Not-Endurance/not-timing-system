using MudBlazor;
using Not.Blazor.Components.Abstractions;
using NoTiming.Console.Features.Dashboard.Timeline;
using NTS.Contracts.Core;
using NTS.Contracts.Features.Console;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Console.Features.Dashboard;

/// <summary>
/// The dashboard of the Event the page opened: the Participation the Main Operator works on is chosen by its number or by
/// what is typed, and its times, its Representation and Inspection requests and its elimination are the panels beside it.
/// </summary>
public class DashboardPageBehind : NStatefulComponent
{
    IDisposable? _watching;
    bool _disposed;

    [Inject]
    IConsoleDashboard Dashboard { get; set; } = default!;

    [Inject]
    IConsoleHandouts Handouts { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    protected IReadOnlyList<Participation> Participations => Dashboard.Participations;
    protected IReadOnlyList<int> RecentlyTimed => Dashboard.RecentlyTimed;

    protected Participation? Selected
    {
        get => Dashboard.Selected;
        set => Dashboard.Selected = value;
    }

    [CascadingParameter]
    public IViewedEvent View { get; set; } = default!;

    protected Task<IEnumerable<Participation?>> Search(string term, CancellationToken _)
    {
        var found = string.IsNullOrEmpty(term)
            ? Dashboard.Participations
            :
            [
                .. Dashboard.Participations.Where(x =>
                    x.ToString().Contains(term, StringComparison.CurrentCultureIgnoreCase)
                ),
            ];
        return Task.FromResult(found.Cast<Participation?>());
    }

    /// <summary>The timeline of the selected Participation: how each of its times was recorded, and which of them to disable or enable.</summary>
    protected async Task ShowTimeline()
    {
        try
        {
            await DialogService.ShowAsync<TimelineDialog>(
                $"{Timeline_string}: {Selected}",
                new DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true }
            );
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await Dashboard.Show(View);
            Watch();
            await Observe(Dashboard);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    public override void Dispose()
    {
        _disposed = true;
        _watching?.Dispose();
        base.Dispose();
    }

    /// <summary>A Phase that completes while the page is open makes its Handout; a page that left meanwhile watches nothing.</summary>
    void Watch()
    {
        if (!_disposed)
        {
            _watching = Handouts.Watch(View);
        }
    }
}
