using Microsoft.AspNetCore.Components;
using MudBlazor;
using NTS.Domain.Core.Aggregates;

namespace NTS.Blazor.Components.ParticipationChips;

public class ParticipationChipsBehind : ComponentBase
{
    [Parameter]
    public Participation SelectedParticipation { get; set; } = default!;

    [Parameter, EditorRequired]
    public IEnumerable<Participation> ParticipationCollection { get; set; } = default!;

    [Parameter]
    public Size ChipSize { get; set; } = Size.Medium;

    [Parameter]
    public Func<Participation, Color>? GetColorAction { get; set; } = default!;

    [Parameter]
    public Action<Participation>? ClickAction { get; set; } = default!;

    public void ClickHandler(Participation participation)
    {
        ClickAction?.Invoke(participation);
    }
}
