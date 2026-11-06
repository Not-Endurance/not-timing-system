using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using NC.Application.HTTP;
using NC.Application.RPC;
using NC.Application.RPC.Clients;
using NC.Blazor.Client.Authentication.Services;
using NC.Startup;
using NoTiming.Application.Contracts;
using NoTiming.Application.Contracts.Core;
using NoTiming.Application.Contracts.Socket;
using NoTiming.Application.Contracts.Watcher.Models;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Watcher;
using NoTiming.IntegrationTests.Infrastructure;
using NoTiming.Storage;
using NoTiming.Ui.Application;
using NoTiming.Ui.Application.Features.Core.Dashboard;
using NoTiming.Ui.Contracts.Features.Access;

namespace NoTiming.IntegrationTests.Drivers;

internal sealed class UiDriver : IAsyncDisposable
{
    readonly ServiceProvider _provider;
    readonly INoTimingSocketService _socketService;
    readonly IParticipationContext _participationContext;
    readonly IUiAccessContext _accessContext;
    readonly string _clientName;

    /// <param name="sessionCookie">
    /// The signed-in person's session cookie, as <see cref="NoTimingIntegrationFixture.SignInAsync"/> returns it. Null
    /// makes the driver an anonymous visitor.
    /// </param>
    public UiDriver(Uri noTimingBaseUrl, string? sessionCookie, string clientName)
    {
        _clientName = clientName;
        var configuration = DriverConfiguration.Create(noTimingBaseUrl, ApplicationConstants.WITNESS_HUB, clientName);
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.ConfigureNoTimingStorage(configuration).AddRestApiStorage();
        services.AddNoTimingUiApplication(
            configuration,
            noTimingBaseUrl.ToString().TrimEnd('/'),
            typeof(NoTimingUiApplicationServices).Assembly
        );
        // The browser's signed-in state, read from the server's profile route with the same provider the Ui uses.
        services.AddScoped<AuthenticationStateProvider, SessionAuthenticationStateProvider>();
        if (sessionCookie != null)
        {
            services
                .AddHttpClient(nameof(NHttpClient))
                .AddHttpMessageHandler(() => new SessionCookieHandler(sessionCookie));
            services.Replace(
                ServiceDescriptor.Scoped<IRpcSocket>(provider =>
                    ActivatorUtilities.CreateInstance<SessionCookieSocket>(provider, noTimingBaseUrl, sessionCookie)
                )
            );
        }

        _provider = services.BuildServiceProvider();
        _socketService = _provider.GetRequiredService<INoTimingSocketService>();
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
            throw new InvalidOperationException($"Ui '{_clientName}' did not connect to event {eventInformation.Id}.");
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
