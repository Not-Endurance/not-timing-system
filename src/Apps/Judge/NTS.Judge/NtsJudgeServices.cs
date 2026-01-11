using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.Krud;
using Not.Blazor;
using NTS.Application;
using NTS.Domain.Setup.Aggregates;

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
        services.ConfigureNtsApplication(configuration, Assembly.GetCallingAssembly()).AddStartlist().AddRpcClient();
        return services.ConfigureNts(configuration).AddNBlazor(configuration);
    }
}
