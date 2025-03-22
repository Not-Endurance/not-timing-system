using NTS.Domain.Core.Aggregates.Participations;

namespace NTS.Storage.Documents.Archive.Models;

public class TotalDocumentModel
{
    public static TotalDocumentModel Create(Total total)
    {
        return new TotalDocumentModel
        {
            LastArriveTime = total.LastArriveTime.ToDateTimeOffset(),
            AverageSpeed = total.AverageSpeed.ToDouble(),
            Interval = total.Interval.ToTimeSpan(),
            RideInterval = total.RideInterval.ToTimeSpan(),
            RecoveryInterval = total.RecoveryInterval.ToTimeSpan(),
            RecoveryIntervalWithoutFinal = total.RecoveryIntervalWithoutFinal.ToTimeSpan(),
        };
    }

    public DateTimeOffset LastArriveTime { get; init; }
    public double AverageSpeed { get; init; }
    public TimeSpan Interval { get; init; }
    public TimeSpan RideInterval { get; init; }
    public TimeSpan RecoveryInterval { get; init; }
    public TimeSpan RecoveryIntervalWithoutFinal { get; init; }
}
