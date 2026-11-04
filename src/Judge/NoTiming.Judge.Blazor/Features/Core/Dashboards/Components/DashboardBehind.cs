using MudBlazor;
using NC.Blazor.Components.Abstractions;
using NoTiming.Application.Contracts.Core;
using NoTiming.Application.Contracts.Core.Models;
using NoTiming.Domain.Core.Aggregates;

namespace NoTiming.Judge.Blazor.Features.Core.Dashboards.Components;

public class DashboardBehind : NStatefulComponent
{
    [Inject]
    IParticipationContext Service { get; set; } = default!;

    protected IReadOnlyList<Participation> Participations => Service.Participations;
    protected IReadOnlyList<int> RecentlyTimed => Service.RecentlyTimed;

    protected Participation? Selected
    {
        get => Service.Selected;
        set => Service.Selected = value;
    }

    protected Task<IEnumerable<Participation>> Search(string term, CancellationToken _)
    {
        if (string.IsNullOrEmpty(term))
        {
            return Task.FromResult(Service.Participations.AsEnumerable());
        }
        var result = Service.Participations.Where(x => x.ToString().ToLower().Contains(term.ToLower()));
        return Task.FromResult(result);
    }

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }
}
