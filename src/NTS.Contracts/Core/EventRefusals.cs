namespace NTS.Contracts.Core;

/// <summary>The codes of the refusals of the Api that a view of an Event acts on (#631, ADR-0007).</summary>
public static class EventRefusals
{
    /// <summary>A write to an Event whose last day is over: the Event is Historic from then on.</summary>
    public const string ENDED = "event-ended";
}
