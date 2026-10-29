using Not.Application.RPC;
using NTS.Contracts.Socket;
using NTS.Domain.Core.Aggregates;
using NTS.Tests.Integration.Drivers;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>The Event the Ui has selected, as the repositories and the contexts of the Ui ask for it: the one the app is connected to, or none.</summary>
internal sealed class SelectedEvent : INtsSocketContext
{
    public SelectedEvent(Guid? eventId)
    {
        Event = eventId == null ? null : IntegrationPayloadFactory.EventInformation(eventId.Value);
    }

    public bool IsConnected => Event != null;
    public SocketConnectionStatus Status =>
        IsConnected ? SocketConnectionStatus.Connected : SocketConnectionStatus.Disconnected;
    public EventInformation? Event { get; }
}
