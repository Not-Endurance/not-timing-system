using System.Net;
using System.Net.Sockets;

namespace NoTiming.IntegrationTests.Infrastructure;

internal static class PortAllocator
{
    public static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
