using Microsoft.AspNetCore.Components.Rendering;
using MudBlazor;
using Not.Blazor.Components.Text.Abstractions;

namespace Not.Blazor.Components.Text;

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
