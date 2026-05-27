using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Blazor;
using Not.Blazor.Client;
using NTS.Judge.Blazor.Features.Print;
using Not.Startup;
using NTS.Judge.Blazor.Features.Socket;

namespace NTS.Judge.Blazor;

public static class JudgeBlazorServices
{
    public static IServiceCollection AddJudgeBlazor(this IServiceCollection services, IConfiguration configuration)
    {
        return services
            .AddNBlazor(configuration)
            .NClientSideBlazor(configuration)
            .AddTransient<IJudgePdfBrowserService, JudgePdfBrowserService>()
            .AddScoped<IStartupInitializerAsync, JudgeStartupEventInformationCoordinator>();
    }
}
