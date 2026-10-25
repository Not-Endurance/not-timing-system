using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application;
using Not.Application.RPC.SignalR;
using Not.Blazor.Client;
using Not.Injection;
using Not.Krud.ServiceRegistration;
using NoTiming.Ui.Features.Core.Events;
using NoTiming.Ui.Features.Lists;
using NTS;
using NTS.Contracts.Arrivelists;
using NTS.Contracts.Core;
using NTS.Contracts.Presentlists;
using NTS.Contracts.Startlists;
using NTS.Domain.Setup.Aggregates;

namespace NoTiming.Ui;

public static class NtsWitnessServices
{
    public static IServiceCollection AddNtsWitness(
        this IServiceCollection services,
        IConfiguration configuration,
        Assembly rootAssembly
    )
    {
        services
            .ConfigureKrud()
            .RegisterAggregate<ConfigureEvent>()
            .RegisterAggregate<Athlete>()
            .RegisterAggregate<Horse>()
            .RegisterAggregate<Club>();
        services.AddNConventionalServices(rootAssembly);
        services.Add<IEventInformationService, ILiveEventsContext, EventInformationService>(ServiceLifetime.Scoped);
        services.Add<IArrivelistService, ArrivelistService>(ServiceLifetime.Scoped);
        services.Add<IPresentlistService, PresentlistService>(ServiceLifetime.Scoped);
        services.Add<IStartUpcoming, IStartHistory, StartlistService>(ServiceLifetime.Scoped);
        new NApplicationBuilder(services, configuration).AddRpcClient().AddDomainEvents().AddHttp();

        return services.AddNts(configuration).NClientSideBlazor(configuration);
    }
}
