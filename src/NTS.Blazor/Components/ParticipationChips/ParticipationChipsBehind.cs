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

    protected Participation? Selected => RecentService.Selected;

    protected IReadOnlyList<Participation> Participations => RecentService.Participations;

    protected override async Task OnInitializedAsync()
    {
        await Observe(RecentService);
    }

    protected Task Select(Participation participation)
    {
        RecentService.Selected = participation;
        return Task.CompletedTask;
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
