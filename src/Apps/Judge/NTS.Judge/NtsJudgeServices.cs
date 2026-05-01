using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Krud.ServiceRegistration;
using NTS.Application;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge;

public static class NtsJudgeServices
{
    public static IServiceCollection AddNtsJudge(
        this IServiceCollection services,
        IConfiguration configuration,
        Assembly rootAssembly
    )
    {
        services
            .ConfigureNtsApplication(configuration, rootAssembly)
            .AddSharedCoreDomainServices()
            .ConfigureN()
            .AddRpcClient()
            .AddDomainEvents();

        services
            .ConfigureKrud()
            .RegisterAggregate<UpcomingEvent>()
            .RegisterAggregate<Athlete>()
            .RegisterAggregate<Horse>()
            .RegisterAggregate<Club>();

        return services.AddNts(configuration);
    }
}
