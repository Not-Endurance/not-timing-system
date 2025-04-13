using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.Configurations;
using Not.Application.RPC.SignalR;

namespace Not.Application.RPC;

public static class RpcServiceCollectionExtensions
{
    public static IServiceCollection AddRpcSocket(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSettings<RpcSettings>(configuration, x => !string.IsNullOrWhiteSpace(x.Host) || !string.IsNullOrWhiteSpace(x.HubPattern));
        return services;
    }
}
