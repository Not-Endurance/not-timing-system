using Not.Blazor.Components.Abstractions;
using NTS.Application.Core;
using NTS.Domain.Core.Aggregates;
using NTS.Witness.Blazor.Features.Socket;
using NTS.Witness.Features.Core.Dashboard;

namespace NTS.Witness.Blazor.Features.Core.Performance;

public class PerformanceContentBehind : NStatefulComponent
{
    [Inject]
    protected IPerformanceService PerformanceService { get; set; } = default!;

    [Inject]
    protected BlazorSocketService BlazorSocketService { get; set; } = default!;

    protected Participation? Selected
    {
        get => PerformanceService.Selected;
        set => PerformanceService.Selected = value;
    }

    protected IReadOnlyList<Participation> Participations => PerformanceService.Participations;

    protected override async Task OnInitializedAsync()
    {
        await Observe(PerformanceService);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await BlazorSocketService.EnsureConnected();
        }
    }

    protected Task<IEnumerable<Participation?>> Search(string term, CancellationToken _)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return Task.FromResult(Participations.Cast<Participation?>());
            }

            var result = Participations
                .Where(x => x.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
                .Cast<Participation?>();
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            Handle(ex);
            return Task.FromResult(Enumerable.Empty<Participation?>());
        }
    }
}
