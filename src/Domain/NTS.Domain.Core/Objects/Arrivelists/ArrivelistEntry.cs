namespace NTS.Domain.Core.Objects.Arrivelists;

public record ArrivelistEntry : ValueObject
{
    internal ArrivelistEntry(
        int number,
        string athleteName,
        string horseName,
        Timestamp? fastest,
        Timestamp? average,
        Timestamp? slowest
    )
    {
        Number = number;
        AthleteName = athleteName;
        HorseName = horseName;
        Fast = fastest;
        Average = average;
        Slow = slowest;
    }

    internal Timestamp? SortEstimate => Fast ?? Average ?? Slow;

    public int Number { get; }
    public string AthleteName { get; }
    public string HorseName { get; }
    public Timestamp? Fast { get; }
    public Timestamp? Average { get; }
    public Timestamp? Slow { get; }
}
