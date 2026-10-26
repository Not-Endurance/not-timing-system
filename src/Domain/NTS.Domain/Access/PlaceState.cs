namespace NTS.Domain.Access;

/// <summary>What the policy needs to know about the place besides who stands there: the stage of an Event, or whether a Tenant can hold Events.</summary>
public sealed class PlaceState
{
    public static PlaceState OfEvent(EventStage stage)
    {
        return new PlaceState(Place.Event, stage, true);
    }

    public static PlaceState OfTenant(bool isOperational)
    {
        return new PlaceState(Place.Tenant, EventStage.Unstarted, isOperational);
    }

    PlaceState(Place place, EventStage stage, bool isOperational)
    {
        Place = place;
        Stage = stage;
        IsOperational = isOperational;
    }

    public static PlaceState Platform { get; } = new(Place.Platform, EventStage.Unstarted, true);

    public Place Place { get; }
    public EventStage Stage { get; }

    /// <summary>Whether the Tenant can hold Events, which it can once it has a Tenant Root.</summary>
    public bool IsOperational { get; }
}
