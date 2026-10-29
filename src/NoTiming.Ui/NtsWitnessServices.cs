using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Not.Application.CRUD.Ports;
using Not.Application.RPC.SignalR;
using Not.Async;
using Not.Blazor.Client;
using Not.Blazor.Client.Browser;
using Not.Krud.ServiceRegistration;
using NoTiming.Ui.Storage.Browser;
using NTS;
using NTS.Application;

namespace NoTiming.Ui;

public static class NtsWitnessServices
{
    public static IServiceCollection AddNtsWitness(
        this IServiceCollection services,
        IConfiguration configuration,
        string baseUrl,
        Assembly rootAssembly
    )
    {
        services.ConfigureKrud();
        services
            .ConfigureNtsApplication(configuration, rootAssembly)
            .AddSharedCoreDomainServices()
            .ConfigureN()
            .AddRpcClient()
            .AddDomainEvents()
            .AddHttp(settings => settings.Host = baseUrl);

        services.TryAddScoped<IDurableRepository<DurableSnapshotGroup>>(provider =>
            new BrowserDurableRepository<DurableSnapshotGroup>(
                backup: provider.GetRequiredService<ILocalStorage>(),
                delay: provider.GetRequiredService<IDelay>(),
                prefix: DurableSnapshotGroup.KEY_PREFIX,
                options: DurableSnapshotGroup.Options
            )
        );

        return services.AddNts(configuration).NClientSideBlazor(configuration);
    }
}
