using Microsoft.AspNetCore.Components;
using NC.Injection;

namespace NC.Blazor.Components.Print;

public interface INHtmlComponentRenderer : ITransient
{
    Task<string> Render<TComponent>(IReadOnlyDictionary<string, object?> parameters)
        where TComponent : IComponent;
}
