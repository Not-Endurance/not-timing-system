using MudBlazor;
using Not.Application.RPC;
using NoTiming.Console.Features.Navigation;
using NTS.Contracts.Socket;
using NTS.Domain.Core.Aggregates;
using static NTS.Localization.NtsStrings;

namespace NTS.Tests.Unit.Harness;

internal sealed class StubSocket : INtsSocketContext
{
    public StubSocket(Guid? eventId, SocketConnectionStatus status)
    {
        Status = status;
        Event = eventId == null ? null : EventOf(eventId.Value);
    }

    public SocketConnectionStatus Status { get; }
    public bool IsConnected => Status == SocketConnectionStatus.Connected && Event != null;
    public EventInformation? Event { get; }

    static EventInformation EventOf(Guid id)
    {
        var country = new global::NTS.Domain.Aggregates.Country(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG");
        var span = new global::NTS.Domain.Core.Objects.EventSpan(
            DateTimeOffset.Now.Date,
            DateTimeOffset.Now.Date.AddDays(1)
        );
        return new EventInformation(country, "Event", "Location", span, null, id: id);
    }
}
