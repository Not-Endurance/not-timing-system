using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NC.Application.HTTP;
using NC.Application.RPC;
using NC.Application.RPC.Clients;
using NC.Krud.Abstractions;
using NC.Notify;
using NC.Startup;
using NoTiming.Application.Contracts;
using NoTiming.Application.Contracts.Core;
using NoTiming.Application.Contracts.Socket;
using NoTiming.Domain.Aggregates;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Judge;
using NoTiming.Judge.Contracts.Features.Core.Dashboard;
using NoTiming.Storage;
using NoTiming.IntegrationTests.Infrastructure;

namespace NoTiming.IntegrationTests.Drivers;

internal sealed class JudgeDriver : IAsyncDisposable
{
    readonly ServiceProvider _provider;
    readonly INoTimingSocketService _socketService;
    readonly IJudgeSnapshotService _snapshotService;
    readonly IParticipationContext _participationContext;
    readonly IEventScopedRepository<Participation> _participationRepository;
    readonly IOptions<NHttpSettings> _httpSettings;

    public JudgeDriver(Uri noTimingBaseUrl)
    {
        var configuration = DriverConfiguration.Create(noTimingBaseUrl, ApplicationConstants.JUDGE_HUB, "IntegrationJudge");
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.ConfigureNoTimingStorage(configuration).AddRestApiStorage();
        services.AddNoTimingJudge(configuration, typeof(NoTimingJudgeServices).Assembly);
        services.AddNotifier();

        _provider = services.BuildServiceProvider();
        _socketService = _provider.GetRequiredService<INoTimingSocketService>();
        _snapshotService = _provider.GetRequiredService<IJudgeSnapshotService>();
        _participationContext = _provider.GetRequiredService<IParticipationContext>();
        _participationRepository = _provider.GetRequiredService<IEventScopedRepository<Participation>>();
        _httpSettings = _provider.GetRequiredService<IOptions<NHttpSettings>>();
    }

    public IReadOnlyList<Participation> Participations => _participationContext.Participations;
    public IReadOnlyList<int> RecentlyTimed => _participationContext.RecentlyTimed;
    public bool IsConnected => _socketService.IsConnected;
    public string ParticipationRepositoryType => _participationRepository.GetType().FullName ?? "unknown";
    public string HttpBaseUrl => _httpSettings.Value.Url ?? "";

    public T GetRequiredService<T>()
        where T : notnull
    {
        return _provider.GetRequiredService<T>();
    }

    public void SelectSetupParent(object aggregate)
    {
        foreach (var setter in _provider.GetRequiredService<IEnumerable<IKrudNodeSetter>>())
        {
            setter.SetParent(aggregate);
        }
    }

    public async Task<IReadOnlyList<Participation>> ReadParticipations()
    {
        return (await _participationRepository.ReadMany()).ToArray();
    }

    public Task Start()
    {
        EnsureRpcClientsInitialized();
        return _provider.Startup();
    }

    public async Task Connect(EventInformation eventInformation)
    {
        await _socketService.Connect(eventInformation);
        if (!_socketService.IsConnected)
        {
            throw new InvalidOperationException($"Judge did not connect to event {eventInformation.Id}.");
        }
    }

    public Task Disconnect()
    {
        return _socketService.IsConnected ? _socketService.Disconnect() : Task.CompletedTask;
    }

    public Task Record(Snapshot snapshot)
    {
        return _snapshotService.Record(snapshot);
    }

    public async ValueTask DisposeAsync()
    {
        if (_socketService.IsConnected)
        {
            await _socketService.Disconnect();
        }

        await _provider.DisposeAsync();
    }

    void EnsureRpcClientsInitialized()
    {
        foreach (var rpcClient in _provider.GetServices<IRpcClient>())
        {
            rpcClient.RunAtStartup();
        }
    }

}

internal static class IntegrationServiceCollectionExtensions
{
    public static IServiceCollection AddNotifier(this IServiceCollection services)
    {
        services.TryAddSingleton<Notifier>();
        services.TryAddSingleton<INotifier>(provider => provider.GetRequiredService<Notifier>());
        services.TryAddSingleton<INotificationStream>(provider => provider.GetRequiredService<Notifier>());
        return services;
    }
}
