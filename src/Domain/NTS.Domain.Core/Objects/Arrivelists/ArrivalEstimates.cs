namespace NTS.Domain.Core.Objects.Arrivelists;

/// <summary>
/// The three arrival estimates an Arrivelist entry shows for one speed model. Each entry carries
/// one instance per model - see <see cref="ArrivelistEntry"/>.
/// </summary>
public record ArrivalEstimates : ValueObject
{
    internal ArrivalEstimates(Timestamp? fast, Timestamp? average, Timestamp? slow)
    {
        Fast = fast;
        Average = average;
        Slow = slow;
    }

    internal Timestamp? SortEstimate => Fast ?? Average ?? Slow;

    public Timestamp? Fast { get; }
    public Timestamp? Average { get; }
    public Timestamp? Slow { get; }
}
