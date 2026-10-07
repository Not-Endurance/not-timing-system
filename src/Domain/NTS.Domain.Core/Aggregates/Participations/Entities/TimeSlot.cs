namespace NTS.Domain.Core.Aggregates.Participations.Entities;

/// <summary>A time a Phase shows, each the latest accepted event that feeds it (ADR-0005).</summary>
public enum TimeSlot
{
    Arrive = 1,
    Present = 2,
    Represent = 3,
}
