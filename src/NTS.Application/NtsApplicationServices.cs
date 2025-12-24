using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.HTTP;
using Not.Application.RPC;
using Not.Injection;

namespace NTS.Application;

public static class NtsApplicationServices
{
    public static IServiceCollection ConfigureNtsApplication(this IServiceCollection services, IConfiguration configuration, Assembly rootAssembly)
    {
        return services
            .AddNConventionalServices(rootAssembly)
            .AddNRpcSocket(configuration)
            .AddNHttp(configuration);
    }
}
