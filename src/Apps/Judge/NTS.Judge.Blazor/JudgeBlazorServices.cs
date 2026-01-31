using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Blazor;

namespace NTS.Judge.Blazor;

public static class JudgeBlazorServices
{
    public static IServiceCollection AddJudgeBlazor(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddNBlazor(configuration);
    }
}
