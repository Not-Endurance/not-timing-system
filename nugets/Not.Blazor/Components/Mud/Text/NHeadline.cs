using Microsoft.AspNetCore.Components.Rendering;
using MudBlazor;

namespace Not.Blazor.Components;

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
