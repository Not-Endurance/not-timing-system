using MudBlazor;

namespace Not.Blazor.Components;

public abstract class NChipsBehind<T> : MudChipSet<T>
{
    [Parameter]
    public IEnumerable<T> Items { get; set; } = [];

    [Parameter]
    public Func<T, string>? TextSelector { get; set; }

    [Parameter]
    public Size ChipSize { get; set; } = Size.Medium;

    [Parameter]
    public Func<T, Color>? GetChipColor { get; set; }

    [Parameter]
    public Action<T>? OnSelect { get; set; }

    protected string GetText(T item)
    {
        return TextSelector?.Invoke(item)
            ?? item!.ToString()
            ?? throw new Exception(
                $"Either provide '{nameof(TextSelector)}' or ensure '{typeof(T).FullName}' provides non-null value from '{nameof(ToString)}'"
            );
    }

    protected Color GetColor(T item)
    {
        return GetChipColor?.Invoke(item) ?? Color.Primary;
    }
}
