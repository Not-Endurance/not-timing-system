using System.Net;
using System.Net.Sockets;

namespace NoTiming.Api.Features.Account;

/// <summary>
/// Who a request comes from, for the limits (#601). It is the address of the connection, which behind the platform's
/// front end is the address that front end forwards (the forwarded-headers middleware of the host puts it there: see
/// the notes on the deployment). A person on IPv6 has a whole <c>/64</c> to themselves and can pick another address of
/// it for every request, so what counts is the network and not the address.
/// </summary>
internal static class ClientAddress
{
    public const string UNKNOWN = "unknown";

    public static string Of(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address is null)
        {
            return UNKNOWN;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            Array.Clear(bytes, 8, 8);
            address = new IPAddress(bytes);
        }

        return address.ToString();
    }
}
