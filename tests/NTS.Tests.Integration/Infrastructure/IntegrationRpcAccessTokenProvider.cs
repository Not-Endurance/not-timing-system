using Not.Application.RPC.SignalR;

namespace NTS.Tests.Integration.Infrastructure;

internal sealed class IntegrationRpcAccessTokenProvider : IRpcAccessTokenProvider
{
    public Task<string?> Get()
    {
        return Task.FromResult<string?>(null);
    }
}
