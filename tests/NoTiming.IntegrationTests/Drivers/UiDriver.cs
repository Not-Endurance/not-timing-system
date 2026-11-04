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
using NTS.Domain.Watcher;
using NTS.Storage;
using NoTiming.IntegrationTests.Infrastructure;
using NoTiming.Ui.Application;
using NoTiming.Ui.Contracts.Features.Access;
using NoTiming.Ui.Application.Features.Core.Dashboard;

namespace NoTiming.IntegrationTests.Drivers;

internal sealed class UiDriver : IAsyncDisposable
{
    readonly ServiceProvider _provider;
    readonly INtsSocketService _socketService;
    readonly IParticipationContext _participationContext;
    readonly IUiAccessContext _accessContext;
    readonly string _clientName;

    public UiDriver(Uri noTimingBaseUrl, IntegrationUser user, string clientName)
    {
        _clientName = clientName;
        var configuration = DriverConfiguration.Create(
            noTimingBaseUrl,
            ApplicationConstants.WITNESS_HUB,
            clientName,
            [
                new("NClientAuthenticationSettings:ClientId", "integration-client"),
                new("NClientAuthenticationSettings:Instance", "https://login.microsoftonline.com"),
                new("NClientAuthenticationSettings:TenantId", "integration-tenant"),
            ]
        );
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.ConfigureNtsStorage(configuration).AddRestApiStorage();
        services.AddNtsUiApplication(configuration, noTimingBaseUrl.ToString().TrimEnd('/'), typeof(NtsUiApplicationServices).Assembly);
        services.Replace(ServiceDescriptor.Scoped<IRpcAccessTokenProvider, IntegrationRpcAccessTokenProvider>());
        services.AddScoped<AuthenticationStateProvider>(_ => new IntegrationAuthenticationStateProvider(user));

        _provider = services.BuildServiceProvider();
        _socketService = _provider.GetRequiredService<INtsSocketService>();
        _participationContext = _provider.GetRequiredService<IParticipationContext>();
        _accessContext = _provider.GetRequiredService<IUiAccessContext>();
    }

    public UiAccessLevel AccessLevel => _accessContext.AccessLevel;

    public T GetRequiredService<T>()
        where T : notnull
    {
        return _provider.GetRequiredService<T>();
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
                $"Ui '{_clientName}' did not connect to event {eventInformation.Id}."
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

        throw new TimeoutException($"Ui did not observe participation #{participationNumber} before timeout.");
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

        if (_provider.GetService<ISnapshotPublisher>() is IRpcClient uiRpcClient)
        {
            uiRpcClient.RunAtStartup();
        }
    }

}
