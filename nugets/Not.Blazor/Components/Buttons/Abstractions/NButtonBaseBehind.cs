using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace Not.Blazor.Components.Buttons.Abstractions;

public abstract class NButtonBaseBehind : MudButton
{
    protected bool IsLoading { get; set; }

    [Parameter]
    public string? Text { get; set; }

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
