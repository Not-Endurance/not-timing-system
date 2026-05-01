using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using Not.Application.RPC;
using Not.Application.RPC.Clients;
using Not.Notify;
using Not.Startup;
using NTS.Application.Contracts;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Socket;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Judge;
using NTS.Judge.Contracts.Features.Core.Dashboard;
using NTS.Storage;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration.Drivers;

internal sealed class JudgeDriver : IAsyncDisposable
{
    readonly ServiceProvider _provider;
    readonly INtsSocketService _socketService;
    readonly ISnapshotService _snapshotService;
    readonly IParticipationContext _participationContext;
    readonly IRepository<Participation> _participationRepository;
    readonly IOptions<NHttpSettings> _httpSettings;

    public JudgeDriver(Uri warpBaseUrl, Uri nexusBaseUrl)
    {
        var configuration = CreateConfiguration(
            warpBaseUrl,
            nexusBaseUrl,
            ApplicationConstants.JUDGE_HUB,
            "IntegrationJudge"
        );
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.ConfigureNtsStorage(configuration).AddRestApiStorage();
        services.AddNtsJudge(configuration, typeof(NtsJudgeServices).Assembly);
        services.AddNotifier();

        _provider = services.BuildServiceProvider();
        _socketService = _provider.GetRequiredService<INtsSocketService>();
        _snapshotService = _provider.GetRequiredService<ISnapshotService>();
        _participationContext = _provider.GetRequiredService<IParticipationContext>();
        _participationRepository = _provider.GetRequiredService<IRepository<Participation>>();
        _httpSettings = _provider.GetRequiredService<IOptions<NHttpSettings>>();
    }

    public IReadOnlyList<Participation> Participations => _participationContext.Participations;
    public IReadOnlyList<int> RecentlyTimed => _participationContext.RecentlyTimed;
    public string ParticipationRepositoryType => _participationRepository.GetType().FullName ?? "unknown";
    public string HttpBaseUrl => _httpSettings.Value.Url ?? "";

    public async Task<IReadOnlyList<Participation>> ReadParticipations()
    {
        return (await _participationRepository.ReadMany()).ToArray();
    }

    public Task Start()
    {
        EnsureRpcClientsInitialized();
        return _provider.Startup();
    }

    public async Task Connect(EnduranceEvent enduranceEvent)
    {
        await _socketService.Connect(enduranceEvent);
        if (!_socketService.IsConnected)
        {
            throw new InvalidOperationException($"Judge did not connect to event {enduranceEvent.Id}.");
        }
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

    static IConfiguration CreateConfiguration(Uri warpBaseUrl, Uri nexusBaseUrl, string hub, string clientName)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"{nameof(RpcSettings)}:{nameof(RpcSettings.Host)}"] = warpBaseUrl.ToString().TrimEnd('/'),
                    [$"{nameof(RpcSettings)}:{nameof(RpcSettings.HubPattern)}"] = hub,
                    [$"{nameof(RpcSettings)}:{nameof(RpcSettings.ClientName)}"] = clientName,
                    [$"{nameof(RpcSettings)}:{nameof(RpcSettings.AppVersion)}"] = "integration-test",
                    [$"{nameof(RpcSettings)}:{nameof(RpcSettings.ConnectTimeoutSeconds)}"] = "10",
                    [$"{nameof(NHttpSettings)}:{nameof(NHttpSettings.Host)}"] = nexusBaseUrl.ToString().TrimEnd('/'),
                    [$"{nameof(NHttpSettings)}:{nameof(NHttpSettings.EndpointPrefix)}"] = "api",
                }
            )
            .Build();
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
