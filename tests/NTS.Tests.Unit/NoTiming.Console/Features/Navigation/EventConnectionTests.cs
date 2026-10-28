using MudBlazor;
using Not.Application.RPC;
using NoTiming.Console.Features.Navigation;
using NTS.Contracts.Socket;
using NTS.Domain.Core.Aggregates;
using static NTS.Localization.NtsStrings;

namespace NTS.Tests.Unit.NoTiming.Console.Features.Navigation;

/// <summary>
/// Whether the page of an Event is in touch with it (#646): what the Main Operator is told by the row of its pages, which is that
/// it is connected only when the app follows this Event and the connection is up.
/// </summary>
public sealed class EventConnectionTests
{
    static readonly Guid EVENT = TestId.Of(7);

    [Fact]
    public void The_Event_the_app_follows_with_the_connection_up_is_connected()
    {
        var connection = EventConnection.Of(Socket(EVENT, SocketConnectionStatus.Connected), EVENT, "The Cup");

        Assert.Equal(string.Format(Console_connected_string, "The Cup"), connection.Text);
        Assert.Equal(Color.Success, connection.Color);
    }

    [Fact]
    public void The_Event_the_app_follows_with_the_connection_down_is_not()
    {
        var connection = EventConnection.Of(Socket(EVENT, SocketConnectionStatus.Disconnected), EVENT, "The Cup");

        Assert.Equal(string.Format(Console_disconnected_string, "The Cup"), connection.Text);
        Assert.Equal(Color.Error, connection.Color);
    }

    [Fact]
    public void The_Event_the_app_is_connecting_to_is_said_to_be_connecting()
    {
        var connection = EventConnection.Of(Socket(EVENT, SocketConnectionStatus.Connecting), EVENT, "The Cup");

        Assert.Equal(string.Format(Console_connecting_string, "The Cup"), connection.Text);
        Assert.Equal(Color.Warning, connection.Color);
    }

    [Theory]
    [InlineData(SocketConnectionStatus.Connected)]
    [InlineData(SocketConnectionStatus.Disconnected)]
    public void An_Event_the_app_does_not_follow_is_not_connected_whatever_the_connection_to_another_is(
        SocketConnectionStatus status
    )
    {
        var connection = EventConnection.Of(Socket(TestId.Of(8), status), EVENT, "The Cup");

        Assert.Equal(string.Format(Console_connecting_string, "The Cup"), connection.Text);
        Assert.Equal(Color.Warning, connection.Color);
    }

    [Fact]
    public void No_Event_followed_is_connecting_to_this_one()
    {
        var connection = EventConnection.Of(Socket(null, SocketConnectionStatus.Disconnected), EVENT, "The Cup");

        Assert.Equal(Color.Warning, connection.Color);
    }

    static StubSocket Socket(Guid? eventId, SocketConnectionStatus status)
    {
        return new StubSocket(eventId, status);
    }

    sealed class StubSocket : INtsSocketContext
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
}
