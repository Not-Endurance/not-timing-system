using Microsoft.Extensions.DependencyInjection;

namespace Not.Startup;

public static class ServiceProviderExtensions
{
    public static async Task Startup(this IServiceProvider provider)
    {
        var initializers = provider.GetServices<IStartupInitializer>();
        var asnycInitializers = provider.GetServices<IStartupInitializerAsync>();

        foreach (var initializer in initializers)
        {
            initializer.RunAtStartup();
        }
        foreach (var initializer in asnycInitializers)
        {
            await initializer.RunAtStartupAsync();
        }
    }
}
