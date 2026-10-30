using MudBlazor;
using Not.Application.RPC;
using NTS.Contracts.Socket;

namespace NoTiming.Console.Features.Navigation;

/// <summary>
/// Whether the page is in touch with the Event it runs (#646): Judge said so beside its menu, and a Main Operator who is not told
/// that the connection is down works on what the Event was a moment ago. It is connected when the app follows this Event and the
/// connection is up, not when it follows another, and it is disconnected when it follows this one and the connection is down.
/// </summary>
public sealed class EventConnection
{
    public static EventConnection Of(INtsSocketContext socket, Guid eventId, string eventName)
    {
        var isThisOne = socket.Event?.Id == eventId;
        if (isThisOne && socket.IsConnected)
        {
            return new EventConnection(string.Format(Console_connected_string, eventName), Color.Success);
        }

        return isThisOne && socket.Status == SocketConnectionStatus.Disconnected
            ? new EventConnection(string.Format(Console_disconnected_string, eventName), Color.Error)
            : new EventConnection(string.Format(Console_connecting_string, eventName), Color.Warning);
    }

    EventConnection(string text, Color color)
    {
        Text = text;
        Color = color;
    }

    public string Text { get; }
    public Color Color { get; }
}
