namespace NTS.Domain.Core.Objects.Arrivelists;

public record ArrivelistEntry : ValueObject
{
    internal ArrivelistEntry(
        int number,
        string athleteName,
        string horseName,
        ArrivalEstimates fei,
        ArrivalEstimates regional
    )
    {
        Number = number;
        AthleteName = athleteName;
        HorseName = horseName;
        Fei = fei;
        Regional = regional;
    }

    internal Timestamp? SortEstimate => Fei.SortEstimate;

    public int Number { get; }
    public string AthleteName { get; }
    public string HorseName { get; }

    /// <summary>
    /// Estimates on the FEI model - cumulative distance over cumulative interval, recovery included.
    /// </summary>
    public ArrivalEstimates Fei { get; }

    /// <summary>
    /// Estimates on the Regional model - the active loop alone, recovery and ride history excluded.
    /// </summary>
    public ArrivalEstimates Regional { get; }
}
