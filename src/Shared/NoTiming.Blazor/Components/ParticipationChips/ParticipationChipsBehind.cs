using Microsoft.AspNetCore.Components;
using MudBlazor;
using Not.Blazor.Components.Abstractions;
using NTS.Domain.Core.Aggregates;

namespace NTS.Blazor.Components.ParticipationChips;

public abstract class ParticipationChipsBehind : NStatefulComponent
{
    [Parameter]
    public bool Compact { get; set; }

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

    protected Size GetChipSize()
    {
        return Compact ? Size.Small : Size.Medium;
    }

    protected string GetChipStyle()
    {
        return Compact
            ? "min-width: 2.1rem; padding-inline: 0.15rem; height: 1.8rem; font-size: 1rem; font-weight: 700;"
            : "min-width: 1rem";
    }

    protected string GetGroupTextClass()
    {
        return Compact ? "pl-2" : "pl-4";
    }

    protected Typo GetGroupTextTypo()
    {
        return Compact ? Typo.caption : Typo.body2;
    }
}
