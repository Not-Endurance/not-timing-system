using System.Net;
using System.Net.Sockets;

namespace NTS.Tests.Integration.Infrastructure;

internal static class PortAllocator
{
    const int ATTEMPTS = 3;

    public static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    /// <summary>
    /// Starts something on a free port, and starts it again on another one when it fails because the port was taken.
    /// The port is closed again before the host binds it, which takes seconds, and in that time another fixture, a
    /// container or a browser may be given the same one.
    /// </summary>
    public static async Task<T> StartOnAFreePort<T>(Func<int, Task<T>> start)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await start(GetFreeTcpPort());
            }
            catch (InvalidOperationException ex)
                when (attempt < ATTEMPTS && ex.Message.Contains("address already in use", StringComparison.Ordinal)) { }
        }
    }
}
