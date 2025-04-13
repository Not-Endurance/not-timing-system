using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.RPC;
using Not.Blazor.Injection;
using NTS.Application;

namespace NTS.Judge.Blazor;

public static class JudgeBlazorServiceCollectionExtensions
{
    public static IServiceCollection ConfigureJudgeBlazor(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .ConfigureNts(configuration)
            .AddNotBlazor(configuration)
            .AddRpcSocket(configuration);

        return services;
    }
}
