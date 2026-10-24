using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Not.Application.Environments;

public static class NEnvironmentContextServiceCollectionExtensions
{
    public static IServiceCollection AddNEnvironmentContext(this IServiceCollection services, string? environment)
    {
        services.TryAddSingleton(new NEnvironmentContext(environment));
        services.TryAddSingleton<IEnvironmentContext>(provider => provider.GetRequiredService<NEnvironmentContext>());
        return services;
    }
}
