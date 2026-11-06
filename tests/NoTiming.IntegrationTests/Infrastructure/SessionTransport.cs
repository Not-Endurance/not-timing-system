using System.Net;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NC.Application.RPC;
using NC.Application.RPC.SignalR;
using NC.Notify;

namespace NoTiming.IntegrationTests.Infrastructure;

/// <summary>Sends one person's session cookie with every HTTP request a driver makes, as the browser would.</summary>
internal sealed class SessionCookieHandler : DelegatingHandler
{
    readonly string _sessionCookie;

    public SessionCookieHandler(string sessionCookie)
    {
        _sessionCookie = sessionCookie;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        request.Headers.Add("Cookie", _sessionCookie);
        return base.SendAsync(request, cancellationToken);
    }
}

/// <summary>The hub socket of a driver, carrying the session cookie on its connection as a browser does.</summary>
internal sealed class SessionCookieSocket : SignalRSocket
{
    readonly Uri _origin;
    readonly string _sessionCookie;

    public SessionCookieSocket(
        IOptions<RpcSettings> options,
        INotifier notifier,
        IRpcAccessTokenProvider? accessTokenProvider,
        Uri origin,
        string sessionCookie,
        ILogger<SignalRSocket>? logger = null,
        ILoggerFactory? loggerFactory = null
    )
        : base(options, notifier, accessTokenProvider, logger, loggerFactory)
    {
        _origin = origin;
        _sessionCookie = sessionCookie;
    }

    protected override void ConfigureHttpConnection(HttpConnectionOptions options)
    {
        var separator = _sessionCookie.IndexOf('=');
        options.Cookies.Add(_origin, new Cookie(_sessionCookie[..separator], _sessionCookie[(separator + 1)..]));
    }
}
