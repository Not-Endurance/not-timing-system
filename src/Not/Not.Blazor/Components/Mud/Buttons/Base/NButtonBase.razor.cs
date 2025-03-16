using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Not.Localization;

namespace Not.Blazor.Components;

public abstract partial class NButtonBase : MudButton
{
    [Inject]
    ILocalizer Localizer { get; set; } = default!;

    [Parameter]
    public string? Text { get; set; }

    public bool IsLoading { get; protected set; }

    protected override void OnParametersSet()
    {
        ChildContent = Text == null ? null : new RenderFragment(x => x.AddContent(0, Localizer.Get(Text)));
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
