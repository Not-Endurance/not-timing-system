using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Not.Application.HTTP;

public static class HttpServiceCollectionExtensions
{
    public static IServiceCollection AddNHttp(this IServiceCollection services, IConfiguration _)
    {
        services.AddHttpClient();
        services.AddTransient<NHttpClient>();
        return services;
    }
}
