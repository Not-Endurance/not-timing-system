using Microsoft.Extensions.DependencyInjection;
using Not.Logging;
using NTS.Judge.Startup;

namespace NTS.Persistence.Startup;

public static class JudgeStartup
{
    /// <summary>
    /// Necessary to be called directly from UI project, otherwise the runtime treeshakes this
    /// DLL off, because no resources are explicitly referenced.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddJudge(this IServiceCollection services)
    {
        return services;
    }
}
