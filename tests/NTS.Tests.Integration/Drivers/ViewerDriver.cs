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
using NoTiming.Ui;
using NoTiming.Ui.Features.Core.Dashboard;
using NoTiming.Ui.Storage;
using NTS.Contracts;
using NTS.Contracts.Core;
using NTS.Contracts.Features.Access;
using NTS.Contracts.Socket;
using NTS.Contracts.Watcher.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration.Drivers;

internal sealed class ViewerDriver : IAsyncDisposable
{
    readonly ServiceProvider _provider;
    readonly INtsSocketService _socketService;
    readonly IParticipationContext _participationContext;
    readonly IWitnessAccessContext _accessContext;
    readonly IntegrationAuthenticationStateProvider _authenticationStateProvider;
    readonly string _clientName;

    /// <param name="user">A null user drives the Witness as an anonymous, read-only visitor.</param>
    public ViewerDriver(Uri apiBaseUrl, Uri functionsBaseUrl, IntegrationUser? user, string clientName)
    {
        _clientName = clientName;
        var configuration = CreateConfiguration(
            apiBaseUrl,
            functionsBaseUrl,
            ApplicationConstants.LIVE_HUB,
            clientName
        );
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.ConfigureNtsStorage(configuration).AddRestApiStorage();
        services.AddNtsWitness(
            configuration,
            functionsBaseUrl.ToString().TrimEnd('/'),
            typeof(NtsWitnessServices).Assembly
        );
        services.Replace(ServiceDescriptor.Scoped<IRpcAccessTokenProvider, AnonymousRpcAccessTokenProvider>());
        _authenticationStateProvider = new IntegrationAuthenticationStateProvider(user);
        services.AddScoped<AuthenticationStateProvider>(_ => _authenticationStateProvider);

        _provider = services.BuildServiceProvider();
        _socketService = _provider.GetRequiredService<INtsSocketService>();
        _participationContext = _provider.GetRequiredService<IParticipationContext>();
        _accessContext = _provider.GetRequiredService<IWitnessAccessContext>();
    }

    public WitnessAccessLevel AccessLevel => _accessContext.AccessLevel;

    public T GetRequiredService<T>()
        where T : notnull
    {
        return _provider.GetRequiredService<T>();
    }

    /// <summary>
    /// Completes a sign-in on an already running Witness, the way the login callback does.
    /// </summary>
    public void SignIn(IntegrationUser user)
    {
        _authenticationStateProvider.SignIn(user);
    }

    public Task Start()
    {
        EnsureRpcClientsInitialized();
        return _provider.Startup();
    }

    public Task Publish(SnapshotGroup snapshotGroup)
    {
        return _provider.GetRequiredService<ISnapshotPublisher>().PublishSnapshotsAsync(snapshotGroup);
    }

    public async Task Connect(EventInformation eventInformation)
    {
        await _socketService.Connect(eventInformation);
        if (!_socketService.IsConnected)
        {
            throw new InvalidOperationException(
                $"Witness '{_clientName}' did not connect to event {eventInformation.Id}."
            );
        }
    }

    public Task Disconnect()
    {
        return _socketService.IsConnected ? _socketService.Disconnect() : Task.CompletedTask;
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

    static IConfiguration CreateConfiguration(Uri apiBaseUrl, Uri functionsBaseUrl, string hub, string clientName)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"{nameof(RpcSettings)}:{nameof(RpcSettings.Host)}"] = apiBaseUrl.ToString().TrimEnd('/'),
                    [$"{nameof(RpcSettings)}:{nameof(RpcSettings.HubPattern)}"] = hub,
                    [$"{nameof(RpcSettings)}:{nameof(RpcSettings.ClientName)}"] = clientName,
                    [$"{nameof(RpcSettings)}:{nameof(RpcSettings.AppVersion)}"] = "integration-test",
                    [$"{nameof(RpcSettings)}:{nameof(RpcSettings.ConnectTimeoutSeconds)}"] = "10",
                    [$"{nameof(NHttpSettings)}:{nameof(NHttpSettings.Host)}"] = functionsBaseUrl
                        .ToString()
                        .TrimEnd('/'),
                    [$"{nameof(NHttpSettings)}:{nameof(NHttpSettings.EndpointPrefix)}"] = "api",
                    ["NClientAuthenticationSettings:ClientId"] = "integration-client",
                    ["NClientAuthenticationSettings:Instance"] = "https://login.microsoftonline.com",
                    ["NClientAuthenticationSettings:TenantId"] = "integration-tenant",
                }
            )
            .Build();
    }
}

/// <summary>The live connection is anonymous (ADR-0001) and the hub reads no token, so the driver sends none.</summary>
internal sealed class AnonymousRpcAccessTokenProvider : IRpcAccessTokenProvider
{
    public Task<string?> Get()
    {
        return Task.FromResult<string?>(null);
    }
}
