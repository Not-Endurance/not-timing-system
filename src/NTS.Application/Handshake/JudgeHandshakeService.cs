using System.Net;
using System.Net.Sockets;
using System.Text;
using Not.Injection;
using static NTS.Application.ApplicationConstants;

namespace NTS.Application.Handshake;

public class JudgeHandshakeService : INetworkBroadcastService, IHandshakeService
{
    readonly IHandshakeValidatorService _handshakeValidatorService;

    public JudgeHandshakeService(IHandshakeValidatorService handshakeValidatorService)
    {
        _handshakeValidatorService = handshakeValidatorService;
    }

    public async Task StartBroadcasting(CancellationToken token)
    {
        var serverPayload = _handshakeValidatorService.CreatePayload(Apps.JUDGE);
        do
        {
            using var server = new UdpClient(NETWORK_BROADCAST_PORT);
            var requestTask = server.ReceiveAsync();
            var delay = TimeSpan.FromSeconds(3);
            var timeout = Task.Delay(delay);
            var first = await Task.WhenAny(requestTask, timeout);
            if (first == requestTask)
            {
                var request = await requestTask;
                if (_handshakeValidatorService.ValidatePayload(request.Buffer, Apps.WITNESS))
                {
                    Console.WriteLine($"Handshake with '{Apps.WITNESS}' on '{request.RemoteEndPoint.Address}'");
                    server.Send(serverPayload, serverPayload.Length, request.RemoteEndPoint);
                }
                else
                {
                    var contents = Encoding.UTF8.GetString(request.Buffer);
                    Console.WriteLine(
                        $"Invalid handshake attempt '{contents}' from '{request.RemoteEndPoint.Address}'"
                    );
                }
            }
            server.Close();
        } while (!token.IsCancellationRequested);
    }

    public async Task<IPAddress> Handshake(string app, CancellationToken token)
    {
        try
        {
            var payload = _handshakeValidatorService.CreatePayload(app);
            HandshakeResult handshake;
            do
            {
                handshake = await AttemptHandshake(payload);
            } while (handshake.IsTimeout && !token.IsCancellationRequested);

            var response = handshake.Result!.Value;
            if (_handshakeValidatorService.ValidatePayload(response.Buffer, Apps.JUDGE))
            {
                Console.WriteLine($"Handshake completed with '{Apps.JUDGE}' on '{response.RemoteEndPoint.Address}'");
                return response.RemoteEndPoint.Address;
            }
        }
        catch (Exception)
        {
            // TODO: logging/error handling
        }

        return (IPAddress)null!;
    }

    async Task<HandshakeResult> AttemptHandshake(byte[] payload)
    {
        using var socket = new UdpClient();
        socket.EnableBroadcast = true;
        socket.Send(payload, payload.Length, new IPEndPoint(IPAddress.Broadcast, NETWORK_BROADCAST_PORT));

        var delay = TimeSpan.FromSeconds(3); // can be a constant
        var timeout = Task.Delay(delay);
        var result = socket.ReceiveAsync();
        var first = await Task.WhenAny(new List<Task> { timeout, result });
        socket.Close();

        if (first is Task<UdpReceiveResult> success)
        {
            return new HandshakeResult(success.Result);
        }
        else
        {
            return new HandshakeResult();
        }
    }
}

public interface INetworkBroadcastService : ITransient
{
    Task StartBroadcasting(CancellationToken token);
}

public interface IHandshakeService : ITransient
{
    Task<IPAddress> Handshake(string app, CancellationToken token);
}

internal class HandshakeResult
{
    public HandshakeResult(UdpReceiveResult? result = null)
    {
        Result = result;
    }

    public bool IsTimeout => Result == null;
    public UdpReceiveResult? Result { get; }
}
