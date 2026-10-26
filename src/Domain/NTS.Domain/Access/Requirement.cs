namespace NTS.Domain.Access;

/// <summary>One row of the matrix of ADR-0012: where it is done, the lowest Level that holds it, and the stages that take it.</summary>
public sealed class Requirement
{
    public Requirement(
        Place place,
        AccessLevel level,
        IEnumerable<EventStage> stages,
        bool mustRecord = false,
        bool needsOperationalTenant = false
    )
    {
        Place = place;
        Level = level;
        Stages = [.. stages];
        MustRecord = mustRecord;
        NeedsOperationalTenant = needsOperationalTenant;
    }

    public Place Place { get; }
    public AccessLevel Level { get; }

    /// <summary>The stages of an Event that take the row; none for a row that is not about an Event.</summary>
    public IReadOnlyList<EventStage> Stages { get; }

    /// <summary>Whether the Level has to be one whose grant lets it record, as sending a Snapshot has.</summary>
    public bool MustRecord { get; }

    /// <summary>Whether the Tenant has to be able to hold Events, as making an Event has.</summary>
    public bool NeedsOperationalTenant { get; }
}
