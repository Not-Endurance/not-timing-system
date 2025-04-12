using Microsoft.Extensions.DependencyInjection;
using Not.Application.RPC.SignalR;

namespace Not.Application.RPC;

public static class RpcServiceCollectionExtensions
{
    public static IServiceCollection AddRpcSocket(
        this IServiceCollection services,
        RpcProtocol protocol,
        string host, 
        string hubPattern,
        int? port
    )
    {
        var context = new SignalRContext(protocol, host, hubPattern, port);
        var socket = new SignalRSocket(context);
        services.AddSingleton<IRpcSocket>(socket);
        return services;
    }
}
