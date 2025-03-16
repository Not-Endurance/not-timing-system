using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.RPC;
using Not.Blazor.Injection;
using NTS.Application;

namespace NTS.Judge.Blazor;

public static class JudgeBlazorInjection
{
    public static IServiceCollection ConfigureJudgeBlazor(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddLocalization(x => x.ResourcesPath = "Resources/Localization")
            .AddNotBlazor(configuration)
            .AddRpcSocket(RpcProtocol.Http, "localhost", ApplicationConstants.RPC_PORT, ApplicationConstants.JUDGE_HUB);

        return services;
    }
}
