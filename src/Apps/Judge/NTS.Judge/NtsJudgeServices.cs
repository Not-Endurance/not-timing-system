using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.CRUD.Ports;
using Not.Application.Krud.Services;
using Not.Blazor;
using NTS.Application;
using NTS.Domain.Setup.Aggregates;
using Not.Application.Krud;

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

        // TODO: extact Krud in a separate project and create builder
        services.AddKrudV1Tree<UpcomingEvent>();
        services.AddKrudV1Tree<Athlete>();
        services.AddKrudV1Tree<Horse>();
        services.AddKrudV1Tree<Club>();
        services.AddTransient(typeof(IRepository<>), typeof(KrudInMemoryNodeRepository<>));

        return services.ConfigureNts(configuration).AddNBlazor(configuration);
    }
}
