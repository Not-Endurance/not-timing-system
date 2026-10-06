using Not.Blazor.Components.Abstractions;
using NoTiming.Ui.Features.Socket;

namespace NoTiming.Ui.Features.Core.EventViews;

/// <summary>
/// A page of the Core views (#630, ADR-0007): its Event is the one its route names, or, on the address of old that names
/// none, the Live Event the app follows. The page shows it through <c>ViewedEventScope</c>, which opens it by the provider.
/// </summary>
public abstract class EventPageBehind : NStatefulComponent
{
    [Inject]
    protected BlazorSocketService BlazorSocketService { get; set; } = default!;

    [Parameter]
    public Guid? EventId { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (firstRender && EventId == null)
        {
            await BlazorSocketService.EnsureConnected();
        }
    }

    /// <summary>
    /// The router gives a parameter only to the route that has it, and the page of the address that has none is the page
    /// of the address that has one, kept: the Event of an earlier route must not be the Event of this one.
    /// </summary>
    public override Task SetParametersAsync(ParameterView parameters)
    {
        EventId = null;
        return base.SetParametersAsync(parameters);
    }
}
