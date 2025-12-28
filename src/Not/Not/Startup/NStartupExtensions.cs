using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Not.Startup;

public static class NStartupExtensions
{
    public static async Task Startup(this WebApplication app)
    {
        var initializers = app.Services.GetServices<IStartupInitializer>();
        var asnycInitializers = app.Services.GetServices<IStartupInitializerAsync>();

        foreach (var initializer in initializers)
        {
            initializer.RunAtStartup();
        }
        foreach (var initializer in asnycInitializers)
        {
            await initializer.RunAtStartupAsync();
        }

        await app.RunAsync();
    }
}
