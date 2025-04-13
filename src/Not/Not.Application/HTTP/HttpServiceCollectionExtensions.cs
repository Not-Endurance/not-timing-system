using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.Options;

namespace Not.Application.HTTP;

public static class HttpServiceCollectionExtensions
{
    public static IServiceCollection AddNHttp(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient();
        services.AddTransient<NHttpClient>();
        services.AddSettings<NHttpSettings>(configuration, x => !string.IsNullOrWhiteSpace(x.Host));
        return services;
    }
}
