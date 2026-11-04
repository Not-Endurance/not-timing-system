using Microsoft.AspNetCore.Components;
using Not.Injection;

namespace Not.Blazor.Components.Print;

public interface INHtmlComponentRenderer : ITransient
{
    Task<string> Render<TComponent>(IReadOnlyDictionary<string, object?> parameters)
        where TComponent : IComponent;
}
