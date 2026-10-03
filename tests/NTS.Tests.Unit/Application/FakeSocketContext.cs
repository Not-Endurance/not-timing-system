using Not.Application.RPC;
using NTS.Contracts.Socket;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using Country = NTS.Domain.Aggregates.Country;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// The socket of the app as the services see it: connected to an Event until the test leaves it, as the socket
/// service does (it clears the Event before it announces that it was left).
/// </summary>
internal sealed class FakeSocketContext : INtsSocketContext
{
    public static readonly Guid EVENT_ID = TestId.Of(100);

    public bool IsConnected => Event != null;
    public SocketConnectionStatus Status =>
        IsConnected ? SocketConnectionStatus.Connected : SocketConnectionStatus.Disconnected;

    public EventInformation? Event { get; private set; } =
        new(
            new Country(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG"),
            "Event",
            "Location",
            new EventSpan(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(1)),
            null,
            EVENT_ID
        );

    public void Leave()
    {
        Event = null;
    }
}
