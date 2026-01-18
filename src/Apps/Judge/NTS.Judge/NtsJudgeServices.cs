using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.Krud.ServiceRegistration;
using Not.Blazor;
using NTS.Application;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge;

public static class NtsJudgeServices
{
    public static IServiceCollection ConfigureNtsJudge(this IServiceCollection services, IConfiguration configuration)
    {
        services.ConfigureNtsApplication(configuration, Assembly.GetCallingAssembly())
            .AddStartlist()
            .ConfigureN()
            .AddSignalR();

        services
            .ConfigureKrud()
            .RegisterAggregate<UpcomingEvent>()
            .RegisterAggregate<Athlete>()
            .RegisterAggregate<Horse>()
            .RegisterAggregate<Club>();

        return services.ConfigureNts(configuration).AddNBlazor(configuration);
    }
}
