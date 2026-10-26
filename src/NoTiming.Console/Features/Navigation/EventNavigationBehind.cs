using Not.Blazor.Components.Abstractions;
using NTS.Contracts.Core;
using NTS.Contracts.Features.Console;
using NTS.Contracts.Socket;
using NTS.Domain.Access;

namespace NoTiming.Console.Features.Navigation;

/// <summary>
/// The way from one page of an Event to the others (#646): a row of buttons above the page, one for each page that the Event
/// at its stage has for the person (<see cref="EventLinks"/>), the one they are on shown as the one they are on. A person that the
/// Console does not list the Event to has no row.
/// </summary>
public class EventNavigationBehind : NStatefulComponent
{
    Guid _endedSubscription;

    [Inject]
    IConsoleEvents Events { get; set; } = default!;

    [Inject]
    NavigationManager Navigator { get; set; } = default!;

    [Inject]
    INtsSocketService Socket { get; set; } = default!;

    [Inject]
    IViewedEventProvider Provider { get; set; } = default!;

    protected IReadOnlyList<EventLink> Links =>
        Events.Events.FirstOrDefault(x => x.Id == EventId) is { } row ? EventLinks.For(row, Current) : [];

    /// <summary>Whether the connection to the Event is up, said to the person who runs it while it is Live.</summary>
    protected EventConnection? Connection =>
        Events.Events.FirstOrDefault(x => x.Id == EventId) is { Stage: EventStage.Live, RunsTheConsole: true } row
            ? EventConnection.Of(Socket, row.Id, row.Name)
            : null;

    [Parameter, EditorRequired]
    public Guid EventId { get; set; }

    /// <summary>The address template of the page the person is on, which is the constant that the page is routed by.</summary>
    [Parameter, EditorRequired]
    public string Current { get; set; } = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        _endedSubscription = Provider.EventEnded.Subscribe(EventHasEnded);
        await Observe(Events);
        await Observe(Socket);
    }

    protected void Open(EventLink link)
    {
        Navigator.NavigateTo(link.Route);
    }

    public override void Dispose()
    {
        Provider.EventEnded.Unsubscribe(_endedSubscription);
        base.Dispose();
    }

    /// <summary>The Event of the page ended while it was open (#631): what the Console lists of it, and so what it offers, is read again.</summary>
    async Task EventHasEnded(Guid eventId)
    {
        if (eventId == EventId)
        {
            await Events.Refresh();
        }
    }
}
