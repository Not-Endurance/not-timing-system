using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NC.Blazor;
using NC.Blazor.Client;
using NC.Startup;
using NoTiming.Judge.Blazor.Features.Socket;

namespace NoTiming.Judge.Blazor;

public static class JudgeBlazorServices
{
    public static IServiceCollection AddJudgeBlazor(this IServiceCollection services, IConfiguration configuration)
    {
        return services
            .AddNBlazor(configuration)
            .NClientSideBlazor(configuration)
            .AddScoped<IStartupInitializerAsync, JudgeStartupEventInformationCoordinator>();
    }
}
