using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.HTTP;

namespace NTS.Judge;

public static class JudgeInjection
{
    /// <summary>
    /// Necessary to be called directly from UI project, otherwise the runtime treeshakes this
    /// DLL off, because no resources are explicitly referenced.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection ConfigureJudge(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddNHttp(configuration);
        return services;
    }
}
