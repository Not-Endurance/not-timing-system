using Not.Blazor.Components.Abstractions;
using NTS.Contracts.Core;
using NTS.Contracts.Socket;
using NTS.Domain.Access;

namespace NoTiming.Ui.Components.EventViews;

/// <summary>
/// The Event of a route for everything under it (#630, ADR-0007): it opens the Event through the provider, cascades it as
/// the viewed Event, and shows in its place what there is to say when there is none or when the Event does not show the
/// Core view the page is. The Event is the route's, and it is opened again when that changes. The view it opened is
/// disposed when it is replaced and when the page leaves. When a write to the Event it shows is refused because the Event has
/// ended (#631), the provider says so and the Event is opened again on the spot, which gives the historic view in place of the
/// live one: the connection closes, the controls go with the live view, and a banner says why. Nothing else interrupts a
/// person who looks at an Event that ended.
/// </summary>
public class ViewedEventScopeBehind : NStatefulComponent
{
    readonly SemaphoreSlim _opening = new(1, 1);
    Guid _endedSubscription;
    bool _disposed;
    bool _opened;
    Guid _openedFor;

    [Inject]
    IViewedEventProvider Provider { get; set; } = default!;

    [Inject]
    INtsSocketService SocketService { get; set; } = default!;

    [Inject]
    NavigationManager Navigator { get; set; } = default!;

    protected IViewedEvent? View { get; private set; }
    protected bool IsOpening { get; private set; } = true;
    protected bool IsNotFound { get; private set; }
    protected bool IsHidden => View != null && Shows is { } view && !View.Shows(view);

    /// <summary>Whether the Event ended while the person was looking at it, which the banner says.</summary>
    protected bool HasEnded { get; private set; }

    [Parameter]
    public Guid EventId { get; set; }

    /// <summary>The Core view the page is: an Event that does not show it is not reachable through the page.</summary>
    [Parameter]
    public CoreView? Shows { get; set; }

    protected override async Task OnInitializedAsync()
    {
        _endedSubscription = Provider.EventEnded.Subscribe(EventHasEnded);
        await Observe(SocketService);
    }

    protected override async Task OnParametersSetAsync()
    {
        await Open();
    }

    /// <summary>Where an Event goes when it does not show the page: to its record, which every Event shows.</summary>
    protected void OpenResults()
    {
        try
        {
            if (View != null)
            {
                Navigator.NavigateTo(Routes.Of(Routes.HISTORIC_EVENT_DETAILS_PAGE, View.Event.Id));
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    public override void Dispose()
    {
        _disposed = true;
        Provider.EventEnded.Unsubscribe(_endedSubscription);
        View?.Dispose();
        View = null;
        base.Dispose();
    }

    /// <summary>
    /// A write to the Event that the page shows was refused because it has ended, which is the Api's word for it: the connection
    /// that followed the Event is closed, and the Event is opened again, and the Api's list says it is Historic now.
    /// </summary>
    async Task EventHasEnded(Guid eventId)
    {
        if (_disposed || View is not { IsLive: true } || View.Event.Id != eventId)
        {
            return;
        }

        HasEnded = true;
        _opened = false;
        if (SocketService.Event?.Id == eventId)
        {
            await Disconnect();
        }

        await Open();
        await InvokeRender();
    }

    /// <summary>Closes the connection to the Event, and goes on when it cannot be closed: the Event is opened again all the same.</summary>
    async Task Disconnect()
    {
        try
        {
            await SocketService.Disconnect();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    /// <summary>Opens the Event the route names, unless it is the one that is open.</summary>
    async Task Open()
    {
        if (_opened && EventId == _openedFor)
        {
            return;
        }

        await _opening.WaitAsync();
        try
        {
            var eventId = EventId;
            if (_disposed || (_opened && eventId == _openedFor))
            {
                return;
            }

            _opened = true;
            _openedFor = eventId;
            IsNotFound = false;
            View?.Dispose();
            View = null;
            IsOpening = true;
            var view = await Provider.Open(eventId);
            if (_disposed)
            {
                view?.Dispose(); // the page left while it was being opened
                return;
            }

            View = view;
            IsNotFound = view == null;
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
        finally
        {
            IsOpening = false;
            _opening.Release();
        }
    }
}
