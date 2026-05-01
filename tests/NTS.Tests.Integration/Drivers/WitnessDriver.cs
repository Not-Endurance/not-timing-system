using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Not.Application.HTTP;
using Not.Application.RPC;
using Not.Application.RPC.Clients;
using Not.Application.RPC.SignalR;
using Not.Startup;
using NTS.Application.Contracts;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Socket;
using NTS.Application.Contracts.Watcher.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Storage;
using NTS.Witness;
using NTS.Witness.Contracts.Features.Access;
using NTS.Witness.Features.Core.Dashboard;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration.Drivers;

internal sealed class WitnessDriver : IAsyncDisposable
{
    readonly ServiceProvider _provider;
    readonly INtsSocketService _socketService;
    readonly IParticipationContext _participationContext;
    readonly IWitnessAccessContext _accessContext;
    readonly string _clientName;

    public WitnessDriver(Uri warpBaseUrl, Uri nexusBaseUrl, IntegrationUser user, string clientName)
    {
        _clientName = clientName;
        var configuration = CreateConfiguration(warpBaseUrl, nexusBaseUrl, ApplicationConstants.WITNESS_HUB, clientName);
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.ConfigureNtsStorage(configuration).AddRestApiStorage();
        services.AddNtsWitness(configuration, nexusBaseUrl.ToString().TrimEnd('/'), typeof(NtsWitnessServices).Assembly);
        services.Replace(ServiceDescriptor.Scoped<IRpcAccessTokenProvider, IntegrationRpcAccessTokenProvider>());
        services.AddScoped<AuthenticationStateProvider>(_ => new IntegrationAuthenticationStateProvider(user));

        _provider = services.BuildServiceProvider();
        _socketService = _provider.GetRequiredService<INtsSocketService>();
        _participationContext = _provider.GetRequiredService<IParticipationContext>();
        _accessContext = _provider.GetRequiredService<IWitnessAccessContext>();
    }

    public WitnessAccessLevel AccessLevel => _accessContext.AccessLevel;

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
            throw new InvalidOperationException($"Witness '{_clientName}' did not connect to event {enduranceEvent.Id}.");
        }
    }

    public async Task<Participation> WaitForParticipation(
        int participationNumber,
        Func<Participation, bool> predicate,
        TimeSpan timeout
    )
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var participation = _participationContext.Participations.FirstOrDefault(x =>
                x.Combination.Number == participationNumber
            );
            if (participation != null && predicate(participation))
            {
                return participation;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Witness did not observe participation #{participationNumber} before timeout.");
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

        if (_provider.GetService<ISnapshotPublisher>() is IRpcClient witnessRpcClient)
        {
            witnessRpcClient.RunAtStartup();
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
                    ["NClientAuthenticationSettings:ClientId"] = "integration-client",
                    ["NClientAuthenticationSettings:Instance"] = "https://login.microsoftonline.com",
                    ["NClientAuthenticationSettings:TenantId"] = "integration-tenant",
                }
            )
            .Build();
    }
}
