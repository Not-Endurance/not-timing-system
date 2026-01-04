using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Blazor;
using NTS.Application;
using NTS.Storage;

namespace NTS.Judge;

public static class NtsJudgeServices
{
    /// <summary>
    /// Necessary to be called directly from UI project, otherwise the runtime treeshakes this
    /// DLL off, because no resources are explicitly referenced.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection ConfigureNtsJudge(this IServiceCollection services, IConfiguration configuration)
    {
        return services
            .ConfigureNtsCommon(configuration)
            .ConfigureNtsApplication(configuration, Assembly.GetCallingAssembly())
            .ConfigureNtsStorage()
            .AddNBlazor(configuration);
    }
}
