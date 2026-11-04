using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NC.Krud.ServiceRegistration;
using NoTiming.Application;
using NoTiming.Domain.Setup.Aggregates;
using NoTiming.Shared;

namespace NoTiming.Judge;

public static class NoTimingJudgeServices
{
    public static IServiceCollection AddNoTimingJudge(
        this IServiceCollection services,
        IConfiguration configuration,
        Assembly rootAssembly
    )
    {
        services
            .ConfigureNoTimingApplication(configuration, rootAssembly)
            .AddSharedCoreDomainServices()
            .ConfigureN()
            .AddRpcClient()
            .AddDomainEvents();

        services
            .ConfigureKrud()
            .RegisterAggregate<ConfigureEvent>()
            .RegisterAggregate<Athlete>()
            .RegisterAggregate<Horse>()
            .RegisterAggregate<Club>();

        return services.AddNoTiming(configuration);
    }
}
