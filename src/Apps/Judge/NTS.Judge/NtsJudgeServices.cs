using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Blazor;
using NTS.Application;
using NTS.Domain.Setup.Aggregates;
using Not.Application.Krud.ServiceRegistration;

namespace NTS.Judge;

public static class NtsJudgeServices
{
    public static IServiceCollection ConfigureNtsJudge(this IServiceCollection services, IConfiguration configuration)
    {
        services.ConfigureNtsApplication(configuration, Assembly.GetCallingAssembly()).AddStartlist().AddRpcClient();

        services.ConfigureKrud()
            .RegisterAggregate<UpcomingEvent>()
            .RegisterAggregate<Athlete>()
            .RegisterAggregate<Horse>()
            .RegisterAggregate<Club>();

        return services.ConfigureNts(configuration).AddNBlazor(configuration);
    }
}
