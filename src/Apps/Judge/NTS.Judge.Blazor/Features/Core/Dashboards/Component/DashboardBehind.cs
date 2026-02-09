using MudBlazor;
using Not.Blazor.Components;
using NTS.Domain.Core.Aggregates;
using NTS.Judge.Features.Core.Dashboard;

namespace NTS.Judge.Blazor.Features.Core.Dashboards.Component;

public class DashboardBehind : NStatefulComponent<IDashboardService>
{
    protected Task<IEnumerable<Participation>> Search(string term, CancellationToken _)
    {
        if (string.IsNullOrEmpty(term))
        {
            return Task.FromResult(Service.Participations);
        }
        var result = Service.Participations.Where(x => x.ToString().ToLower().Contains(term.ToLower()));
        return Task.FromResult(result);
    }

    protected Color GetColor(Participation participation)
    {
        if (Service.RecentlyProcessed.Contains(participation.Combination.Number))
        {
            return Color.Warning;
        }
        if (participation.IsEliminated())
        {
            return Color.Error;
        }
        if (participation.IsComplete())
        {
            return Color.Success;
        }
        return Color.Primary;
    }
}
