using System.Reflection.Metadata;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Not.Blazor.Components.Abstractions;

namespace Not.Blazor.Components.Buttons;

public class NMultiButtonBehind : NComponent
{
    int _selectedIndex;

    protected NMultiButtonDescriptor? SelectedDescriptor =>
        Descriptors.Count == 0 || _selectedIndex < 0 || _selectedIndex >= Descriptors.Count
            ? null
            : Descriptors[_selectedIndex];

    [Parameter]
    public IReadOnlyList<NMultiButtonDescriptor> Descriptors { get; set; } = [];

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public bool MaxWidth { get; set; }

    [Parameter]
    public Color Color { get; set; } = Color.Primary;

    [Parameter]
    public Color SelectorColor { get; set; } = Color.Secondary

    protected override void OnParametersSet()
    {
        if (Descriptors.Count == 0)
        {
            _selectedIndex = 0;
            return;
        }

        if (_selectedIndex >= Descriptors.Count)
        {
            _selectedIndex = 0;
        }
    }

    protected void Select(int index)
    {
        if (index < 0 || index >= Descriptors.Count)
        {
            return;
        }

        _selectedIndex = index;
    }

    protected async Task TriggerSelectedAction(MouseEventArgs _)
    {
        if (SelectedDescriptor == null)
        {
            return;
        }

        try
        {
            await SelectedDescriptor.SafeAction();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}

public sealed class NMultiButtonDescriptor
{
    public NMultiButtonDescriptor(string content, Func<Task> safeAction, string? icon = null)
    {
        Content = content;
        SafeAction = safeAction;
        Icon = icon;
    }

    public string Content { get; }
    public Func<Task> SafeAction { get; }
    public string? Icon { get; }
}
