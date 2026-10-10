using Not.Blazor.Components.Abstractions;
using NTS.Contracts.Core;
using NTS.Contracts.Socket;
using NTS.Domain.Access;

namespace NoTiming.Ui.Components.EventViews;

/// <summary>
/// The Event of a route for everything under it (#630, ADR-0007): it opens the Event through the provider, cascades it as
/// the viewed Event, and shows in its place what there is to say when there is none or when the Event does not show the
/// Core view the page is. The Event is the route's, or, when the address names none, the Live Event the app follows, and
/// it is opened again when either changes. The view it opened is disposed when it is replaced and when the page leaves.
/// </summary>
public class ViewedEventScopeBehind : NStatefulComponent
{
    readonly SemaphoreSlim _opening = new(1, 1);
    bool _disposed;
    bool _opened;
    Guid? _openedFor;

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

    [Parameter]
    public Guid? EventId { get; set; }

    /// <summary>The Core view the page is: an Event that does not show it is not reachable through the page.</summary>
    [Parameter]
    public CoreView? Shows { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await Observe(SocketService);
    }

    protected override async Task OnParametersSetAsync()
    {
        await Open();
    }

    protected override async Task OnBeforeRenderAsync()
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
        View?.Dispose();
        View = null;
        base.Dispose();
    }

    /// <summary>Opens the Event the route names, or the one the app follows, unless it is the one that is open.</summary>
    async Task Open()
    {
        if (_opened && TargetEvent() == _openedFor)
        {
            return;
        }

        await _opening.WaitAsync();
        try
        {
            var target = TargetEvent();
            if (_disposed || (_opened && target == _openedFor))
            {
                return;
            }

            _opened = true;
            _openedFor = target;
            IsNotFound = false;
            View?.Dispose();
            View = null;
            if (target is not { } eventId)
            {
                return;
            }

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

    Guid? TargetEvent()
    {
        return EventId ?? SocketService.Event?.Id;
    }
}
