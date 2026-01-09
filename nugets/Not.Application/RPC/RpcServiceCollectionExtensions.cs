using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.Configurations;

namespace Not.Application.RPC;

public static class RpcServiceCollectionExtensions
{
    public static IServiceCollection AddRpcCient(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSettings<RpcSettings>(
            configuration,
            x => !string.IsNullOrWhiteSpace(x.Host) || !string.IsNullOrWhiteSpace(x.HubPattern)
        );
        return services;
    }
}
