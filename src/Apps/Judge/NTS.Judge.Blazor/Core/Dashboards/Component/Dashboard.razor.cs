using MudBlazor;
using NTS.Domain.Core.Aggregates;
using NTS.Judge.Features.Core.Dashboard;

namespace NTS.Judge.Blazor.Core.Dashboards.Component;

public partial class Dashboard
{
    [Inject]
    IDashboardService Behind { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Behind);
    }

    Task<IEnumerable<Participation>> Search(string term)
    {
        if (string.IsNullOrEmpty(term))
        {
            return Task.FromResult(Behind.Participations);
        }
        var result = Behind.Participations.Where(x => x.ToString().ToLower().Contains(term.ToLower()));
        return Task.FromResult(result);
    }

    Color GetColor(Participation participation)
    {
        if (Behind.RecentlyProcessed.Contains(participation.Combination.Number))
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
