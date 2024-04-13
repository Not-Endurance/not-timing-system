using Core;
using Core.Application;
using Core.Application.Services;
using Core.Domain;
using Core.Localization;
using EMS.Witness.Services;
using EMS.Witness.Platforms.Services;
using EMS.Witness.Rpc;
using Core.Application.Rpc;

namespace EMS.Witness;

public static class WitnessConfiguration
{
    public static IServiceCollection AddWitnessServices(this IServiceCollection services)
    {
        var assemblies = CoreConstants.Assemblies
            .Concat(LocalizationConstants.Assemblies)
            .Concat(DomainConstants.Assemblies)
            .Concat(CoreApplicationConstants.Assemblies)
            .Concat(WitnessConstants.Assemblies)
            .ToArray();

        services
            .AddCore(assemblies)
            .AddSingleton(new Toaster())
            .AddSingleton<IToaster>(x => x.GetRequiredService<Toaster>())
            .AddSingleton<INotificationService>(x => x.GetRequiredService<Toaster>())
            .AddTransient<IPermissionsService, PermissionsService>()
            .AddSingleton<WitnessContext>()
            .AddSingleton<IWitnessContext>(provider => provider.GetRequiredService<WitnessContext>())
            .AddTransient<IDateService, DateService>()
            .AddSingleton<IRpcSocket, SignalRSocket>()
            .AddSingleton<IStartlistClient, StartlistClient>()
            .AddSingleton<IParticipantsClient, ParticipantsClient>()
            .AddSingleton<WitnessState>()
            .AddSingleton<IWitnessState>(x => x.GetRequiredService<WitnessState>())
            .AddTransient<IPersistenceService, PersistenceService>()
            .AddTransient<IRpcInitalizer, RpcInitalizer>()
            .AddSingleton<LoggingClient>()
            .AddSingleton<IWitnessLogger, LoggingClient>();

        return services;
    }
}
