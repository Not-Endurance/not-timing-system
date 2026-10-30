using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Not.Application.CRUD.Ports;
using Not.Application.RPC.SignalR;
using Not.Async;
using Not.Blazor.Client;
using Not.Injection;
using Not.Krud.ServiceRegistration;
using NoTiming.Ui.Features.Core.Events;
using NoTiming.Ui.Features.Lists;
using Not.Blazor.Client.Browser;
using NoTiming.Ui.Storage.Browser;
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

        services.TryAddScoped<IDurableRepository<DurableSnapshotGroup>>(
            provider => new BrowserDurableRepository<DurableSnapshotGroup>(
                backup: provider.GetRequiredService<ILocalStorage>(),
                delay: provider.GetRequiredService<IDelay>(),
                prefix: DurableSnapshotGroup.KEY_PREFIX,
                options: DurableSnapshotGroup.Options
            )
        );

        return services.AddNts(configuration).NClientSideBlazor(configuration);
    }
}
