using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.UdpHandshake;

namespace NTS.Warp.InProcess;

internal static class JudgeWarpInjection
{
    public static IServiceCollection RegisterServices(this IServiceCollection services, IConfiguration _)
    {
        return services.AddHostedService<NetworkBroadcastService>();
    }
}
