using System.Net;
using System.Net.Sockets;
using System.Text;
using Not.Injection;

namespace Not.Application.UdpHandshake;

public class JudgeHandshakeService : INetworkBroadcastService, IHandshakeService
{
    const int PORT = 11337;
    const string JUDGE = "Judge";
    const string WITNESS = "Witness";

    readonly IHandshakeValidatorService _handshakeValidatorService;

    public JudgeHandshakeService(IHandshakeValidatorService handshakeValidatorService)
    {
        _handshakeValidatorService = handshakeValidatorService;
    }

    public async Task StartBroadcasting(CancellationToken token)
    {
        var serverPayload = _handshakeValidatorService.CreatePayload(JUDGE);
        do
        {
            using var server = new UdpClient(11337);
            var requestTask = server.ReceiveAsync();
            var delay = TimeSpan.FromSeconds(3);
            var timeout = Task.Delay(delay);
            var first = await Task.WhenAny(requestTask, timeout);
            if (first == requestTask)
            {
                var request = await requestTask;
                if (_handshakeValidatorService.ValidatePayload(request.Buffer, WITNESS))
                {
                    Console.WriteLine($"Handshake with '{WITNESS}' on '{request.RemoteEndPoint.Address}'");
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
            if (_handshakeValidatorService.ValidatePayload(response.Buffer, JUDGE))
            {
                Console.WriteLine($"Handshake completed with '{JUDGE}' on '{response.RemoteEndPoint.Address}'");
                return response.RemoteEndPoint.Address;
            }
        }
        catch (Exception) { }

        return (IPAddress)null!;
    }

    async Task<HandshakeResult> AttemptHandshake(byte[] payload)
    {
        using var socket = new UdpClient();
        socket.EnableBroadcast = true;
        socket.Send(payload, payload.Length, new IPEndPoint(IPAddress.Broadcast, PORT));

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
