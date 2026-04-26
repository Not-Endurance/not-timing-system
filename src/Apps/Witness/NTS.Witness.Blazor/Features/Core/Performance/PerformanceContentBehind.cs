using Not.Blazor.Components.Abstractions;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Witness.Blazor.Features.Socket;

namespace NTS.Witness.Blazor.Features.Core.Performance;

public class PerformanceContentBehind : NStatefulComponent
{
    [Inject]
    IParticipationContext Context { get; set; } = default!;

    [Inject]
    BlazorSocketService BlazorSocketService { get; set; } = default!;

    protected IReadOnlyList<int> Recent => Context.RecentlyTimed;

    protected Participation? Selected
    {
        get => Context.Selected;
        set => Context.Selected = value;
    }

    protected IReadOnlyList<Participation> Participations => Context.Participations;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Context);
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
