using Microsoft.AspNetCore.Components.Rendering;
using MudBlazor;
using NC.Blazor.Components.Text.Abstractions;

namespace NC.Blazor.Components.Text;

public class NHeadline : NTextBase
{
    public NHeadline()
    {
        Typo = Typo.h4;
        Align = Align.Center;
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        base.BuildRenderTree(builder);
    }
}
