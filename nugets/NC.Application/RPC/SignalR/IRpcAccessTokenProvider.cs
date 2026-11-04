namespace Not.Application.RPC.SignalR;

public interface IRpcAccessTokenProvider
{
    Task<string?> Get();
}
