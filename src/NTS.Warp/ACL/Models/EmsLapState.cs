using NTS.Warp.ACL.Entities.Laps;

namespace NTS.Warp.ACL.Models;

public class EmsLapState : IEmsLapState
{
    public double LengthInKm { get; set; }
    public bool IsFinal { get; set; }
    public int OrderBy { get; set; }
    public int MaxRecoveryTimeInMins { get; set; }
    public int RestTimeInMins { get; set; }
    public bool IsCompulsoryInspectionRequired { get; set; }
    public int Id { get; set; }
}
