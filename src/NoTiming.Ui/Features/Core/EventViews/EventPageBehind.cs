using Not.Blazor.Components.Abstractions;

namespace NoTiming.Ui.Features.Core.EventViews;

/// <summary>
/// A page of the Core views (#630, ADR-0007): its Event is the one its route names. The page shows it through
/// <c>ViewedEventScope</c>, which opens it by the provider.
/// </summary>
public abstract class EventPageBehind : NStatefulComponent
{
    [Parameter]
    public Guid EventId { get; set; }
}
