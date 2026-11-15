using NC.Application.RPC.SignalR;
using NC.Injection;

namespace NoTiming.Ui.Application.Features.Socket;

/// <summary>
/// The session cookie is sent with the hub connection by the browser, so there is no bearer token to fetch. Public
/// reads connect anonymously and the hub checks the session for writes.
/// </summary>
public sealed class CookieRpcAccessTokenProvider : IRpcAccessTokenProvider, IScoped
{
    public Task<string?> Get()
    {
        return Task.FromResult<string?>(null);
    }
}
