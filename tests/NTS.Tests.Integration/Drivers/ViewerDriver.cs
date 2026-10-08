using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
using NTS.Contracts.Features.Account;
using NTS.Contracts.Features.Snapshots;
using NTS.Contracts.Socket;
using NTS.Contracts.Watcher.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration.Drivers;

internal sealed class ViewerDriver : IAsyncDisposable
{
    /// <summary>A Witness whose person is already signed in at the Api when it starts, as one who opens the app after signing in.</summary>
    public static async Task<ViewerDriver> SignedInAsync(
        NtsIntegrationFixture fixture,
        IntegrationUser user,
        string clientName
    )
    {
        var viewer = new ViewerDriver(fixture, clientName);
        await viewer.SignIn(user);
        return viewer;
    }

    readonly ServiceProvider _provider;
    readonly INtsSocketService _socketService;
    readonly IParticipationContext _participationContext;
    readonly IWitnessAccessContext _accessContext;
    readonly SessionCookies _cookies = new();
    readonly NtsIntegrationFixture? _fixture;
    readonly string _clientName;

    /// <param name="user">
    /// Must be null: the Witness is a visitor until a person is signed in at the Api, which is done by
    /// <see cref="SignedInAsync"/> or, on a driver made with the fixture, by <see cref="SignIn"/>.
    /// </param>
    public ViewerDriver(Uri apiBaseUrl, Uri functionsBaseUrl, IntegrationUser? user, string clientName)
    {
        if (user != null)
        {
            throw new ArgumentException(
                "A person is signed in at the Api, which asks for the fixture: use ViewerDriver.SignedInAsync.",
                nameof(user)
            );
        }

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

        // The families that moved to the Api are reached at the Api, as the Ui does where its page came from.
        services.Configure<JsonApiSettings>(settings =>
        {
            settings.Url = $"{apiBaseUrl.ToString().TrimEnd('/')}/api";
            settings.WriteHeaders[ApplicationConstants.WRITE_HEADER] = ApplicationConstants.WRITE_HEADER_VALUE;
        });
        services.AddNtsWitness(
            configuration,
            functionsBaseUrl.ToString().TrimEnd('/'),
            typeof(NtsWitnessServices).Assembly
        );

        // A browser sends the session cookie of the page's own host on every request; a program has to send it itself.
        services.AddHttpClient(nameof(JsonApiClient)).AddHttpMessageHandler(() => new SessionCookieHandler(_cookies));

        _provider = services.BuildServiceProvider();
        _socketService = _provider.GetRequiredService<INtsSocketService>();
        _participationContext = _provider.GetRequiredService<IParticipationContext>();
        _accessContext = _provider.GetRequiredService<IWitnessAccessContext>();
    }

    /// <summary>A Witness of the Api of the fixture, as a visitor, who can sign a person in later.</summary>
    public ViewerDriver(NtsIntegrationFixture fixture, string clientName)
        : this(fixture.ApiBaseUrl, fixture.FunctionsBaseUrl, null, clientName)
    {
        _fixture = fixture;
    }

    public WitnessAccessLevel AccessLevel => _accessContext.AccessLevel;

    public T GetRequiredService<T>()
        where T : notnull
    {
        return _provider.GetRequiredService<T>();
    }

    /// <summary>
    /// Signs the person in at the Api and tells the Witness, the way the host's sign-in page does: the session is a cookie
    /// the Witness sends from then on, and the account is asked who is signed in. An account is made for the person.
    /// </summary>
    public async Task SignIn(IntegrationUser user)
    {
        var fixture =
            _fixture ?? throw new InvalidOperationException("A person is signed in at the Api of the fixture.");
        var seeded = await TenancySeed.AccountAsync(
            fixture.MongoConnectionString,
            home: null,
            name: user.Name,
            email: user.Email
        );
        using var client = fixture.Api.CreateClient();
        var session = await ApiSessions.SignInAsync(fixture.Api, client, seeded.Email);
        _cookies.Value = $"{session.Name}={session.Value}";
        await _provider.GetRequiredService<IAccountSession>().Refresh();
    }

    public Task Start()
    {
        EnsureRpcClientsInitialized();
        return _provider.Startup();
    }

    /// <summary>Sends the group to the Event the viewer is connected to, through the client of the Api, and says what each came to.</summary>
    public Task<IReadOnlyList<SnapshotReceipt>> Publish(SnapshotGroup snapshotGroup)
    {
        var eventId =
            _provider.GetRequiredService<INtsSocketContext>().Event?.Id
            ?? throw new InvalidOperationException($"Witness '{_clientName}' is not connected to an Event.");
        return _provider.GetRequiredService<ISnapshotPublisher>().PublishSnapshotsAsync(eventId, snapshotGroup);
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
                }
            )
            .Build();
    }

    /// <summary>The cookie the Witness has been given, which it sends to the Api as a browser would.</summary>
    sealed class SessionCookies
    {
        public string? Value { get; set; }
    }

    sealed class SessionCookieHandler : DelegatingHandler
    {
        readonly SessionCookies _cookies;

        public SessionCookieHandler(SessionCookies cookies)
        {
            _cookies = cookies;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            if (_cookies.Value != null)
            {
                request.Headers.Add("Cookie", _cookies.Value);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
