using Microsoft.AspNetCore.Components;
using MudBlazor;
using Not.Blazor.Components.Abstractions;
using NTS.Domain.Core.Aggregates;

namespace NTS.Blazor.Components.ParticipationChips;

public abstract class ParticipationChipsBehind : NStatefulComponent
{
    [Parameter]
    public IReadOnlyList<int> RecentlyTimed { get; set; } = [];

    [Parameter]
    [EditorRequired]
    public required IReadOnlyList<Participation> Participations { get; set; }

    [Parameter]
    public Participation? Selected { get; set; }

    [Parameter]
    public EventCallback<Participation?> SelectedChanged { get; set; }

    protected Task Select(Participation participation)
    {
        try
        {
            if (!SelectedChanged.HasDelegate)
            {
                return Task.CompletedTask;
            }

            return SelectedChanged.InvokeAsync(participation);
        }
        catch (Exception ex)
        {
            Handle(ex);
            return Task.CompletedTask;
        }
    }

    protected Color GetColor(Participation participation)
    {
        try
        {
            if (RecentlyTimed.Contains(participation.Combination.Number))
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
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
        return Color.Primary;
    }
}
