using Microsoft.AspNetCore.Components;
using MudBlazor;
using Not.Blazor.Components.Abstractions;
using NTS.Application.Core;
using NTS.Domain.Core.Aggregates;

namespace NTS.Blazor.Components.ParticipationChips;

public abstract class ParticipationChipsBehind : NStatefulComponent
{
    [Inject]
    IParticipationContext RecentService { get; set; } = default!;

    protected Participation? Selected { get; set; }

    protected IReadOnlyList<Participation> Participations => RecentService.Participations;

    [Parameter]
    public Action<Participation>? OnSelected { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(RecentService);    
    }

    protected Color GetColor(Participation participation)
    {
        if (RecentService.RecentlyTimed.Contains(participation.Combination.Number))
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
