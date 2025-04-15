using Microsoft.Extensions.Hosting;

namespace NTS.Application.Handshake;

public class NetworkBroadcastService : BackgroundService
{
    readonly INetworkBroadcastService _networkService;

    public NetworkBroadcastService(INetworkBroadcastService networkBroadcastService)
    {
        _networkService = networkBroadcastService;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("Broadcasting");
        try
        {
            return _networkService.StartBroadcasting(stoppingToken);
        }
        catch (Exception exception)
        {
            Console.WriteLine("Error while broadcasting!");
            Console.WriteLine(exception.Message);
            Console.WriteLine(exception.StackTrace);
        }
        return Task.CompletedTask;
    }
}
