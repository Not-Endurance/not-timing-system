using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Not.Blazor.Components.Abstractions;

namespace Not.Blazor.Components.Buttons.Abstractions;

public abstract class NButtonBaseBehind : NComponentBase
{
    protected bool IsLoading { get; set; }

    [Parameter]
    public string? Text { get; set; }

    [Parameter]
    public EventCallback<MouseEventArgs> OnClick { get; set; }

    [Parameter]
    public ButtonType ButtonType { get; set; }

    [Parameter]
    public Variant Variant { get; set; }

    [Parameter]
    public Color Color { get; set; }

    [Parameter]
    public string? StartIcon { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public Size Size { get; set; } = Size.Medium;

    [Parameter]
    public Size? IconSize { get; set; }

    protected override void OnParametersSet()
    {
        ChildContent ??= Text == null ? null : new RenderFragment(x => x.AddContent(0, Text));
    }

    protected async Task HandleClick(MouseEventArgs args)
    {
        try
        {
            IsLoading = true;
            if (OnClick.HasDelegate)
            {
                await OnClick.InvokeAsync(args);
            }
        }
        finally
        {
            IsLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    protected Color SelectColor()
    {
        if (Color == Color.Primary)
        {
            return Color.Secondary;
        }
        else
        {
            return Color.Primary;
        }
    }
}
