using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Not.Blazor.Components.Print;

public sealed class NHtmlComponentRenderer : INHtmlComponentRenderer
{
    readonly ILoggerFactory _loggerFactory;
    readonly IServiceScopeFactory _scopeFactory;

    public NHtmlComponentRenderer(IServiceScopeFactory scopeFactory, ILoggerFactory loggerFactory)
    {
        _scopeFactory = scopeFactory;
        _loggerFactory = loggerFactory;
    }

    public async Task<string> Render<TComponent>(IReadOnlyDictionary<string, object?> parameters)
        where TComponent : IComponent
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        await using var renderer = new HtmlRenderer(scope.ServiceProvider, _loggerFactory);
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var dictionary = parameters.ToDictionary(x => x.Key, x => x.Value);
            var component = await renderer.RenderComponentAsync<TComponent>(
                ParameterView.FromDictionary(dictionary)
            );
            return component.ToHtmlString();
        });
    }
}
