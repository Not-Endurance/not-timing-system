using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.SystemProcess;
using NTS.Application.Handshake;

namespace NTS.Judge.Warp;

internal static class JudgeWarpInjection
{
    public static IServiceCollection RegisterServices(this IServiceCollection services, IConfiguration configuration, ProcessTetherContext tetherContext)
    {
        return services
            .AddHostedService<ProcessTetherLoop>()
            .AddHostedService<NetworkBroadcastService>()
            .AddSingleton(tetherContext);
    }
}
