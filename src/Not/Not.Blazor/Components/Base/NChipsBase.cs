using MudBlazor;

namespace Not.Blazor.Components;

public class NChipsBase<T> : NBindableComponent<T>
{
    [Parameter, EditorRequired]
    public required IEnumerable<T> Set { get; set; }

    [Parameter]
    public Size ChipSize { get; set; } = Size.Medium;

    [Parameter]
    public Func<T, Color>? SetChipColor { get; set; }
}
