using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NC.Application.Identity;

namespace NC.Application.HTTP;

public static class NHttpServiceCollectionExtensions
{
    public static IServiceCollection AddNHttpClient(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddTransient<NHttpClient>();
        services.TryAddScoped<IClientIdentity, ClientIdentity>();
        return services;
    }
}
