using MudBlazor;
using Not.Services;

namespace Not.Blazor.Mud.Components.Base;

public class NotButtonBase : MudButton
{
    [Inject]
    private ILocalizer _localizer { get; set; } = default!;

    protected override void OnParametersSet()
    {
        if (Text == null)
        {
            ChildContent = null;
        }
        else
        {
            ChildContent = new RenderFragment(x => x.AddContent(0, _localizer.Get(Text)));
        }
    }

    [Parameter]
    public string? Text { get; set; }
}
