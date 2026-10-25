using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Not.Application.HTTP;
using Not.Application.RPC;
using NoTiming.Ui;
using NTS.Contracts;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// The Ui of a person as the app builds it (#646): the container of <c>AddNoTimingUi</c>, with its scanned services, its
/// repositories and the Krud graphs of the Setup, reaching the Api in this process with the session cookie of the person.
/// What a Console page asks of a service is asked here of the same service, built the way the browser builds it, so that a
/// registration that is missing or doubled is found here and not in a browser.
/// </summary>
internal sealed class ConsoleUi : IAsyncDisposable
{
    const string ORIGIN = "https://localhost";

    public static ConsoleUi Open(ApiFactory api, TenancySeed.Person person)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"{nameof(RpcSettings)}:{nameof(RpcSettings.Host)}"] = ORIGIN,
                    [$"{nameof(RpcSettings)}:{nameof(RpcSettings.HubPattern)}"] = ApplicationConstants.LIVE_HUB,
                    [$"{nameof(RpcSettings)}:{nameof(RpcSettings.ClientName)}"] = "console-test",
                    [$"{nameof(RpcSettings)}:{nameof(RpcSettings.AppVersion)}"] = "integration-test",
                    [$"{nameof(RpcSettings)}:{nameof(RpcSettings.ConnectTimeoutSeconds)}"] = "10",
                }
            )
            .Build();
        var cookie = new JsonApiClients.Cookie(
            $"{ApiSessions.COOKIE_NAME}={person.Page.Cookie(ApiSessions.COOKIE_NAME)}"
        );
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddNoTimingUi(configuration, ORIGIN, typeof(NoTimingUiServices).Assembly);

        // The page's own origin is the Api in this process, and the cookie is what a browser sends to it.
        var requests = new JsonApiClients.Requests();
        services
            .AddHttpClient(nameof(JsonApiClient))
            .ConfigurePrimaryHttpMessageHandler(() => api.Server.CreateHandler())
            .AddHttpMessageHandler(() => cookie)
            .AddHttpMessageHandler(() => requests);
        return new ConsoleUi(services.BuildServiceProvider(), requests);
    }

    readonly ServiceProvider _provider;

    ConsoleUi(ServiceProvider provider, JsonApiClients.Requests requests)
    {
        _provider = provider;
        Requests = requests;
    }

    /// <summary>What the services of the Ui asked of the Api, in order, and a way to answer in its place.</summary>
    public JsonApiClients.Requests Requests { get; }

    public T Get<T>()
        where T : notnull
    {
        return _provider.GetRequiredService<T>();
    }

    public ValueTask DisposeAsync()
    {
        return _provider.DisposeAsync();
    }
}
