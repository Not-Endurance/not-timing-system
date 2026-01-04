using NTS.Warp.ACL.Abstractions;

namespace NTS.Warp.ACL.Entities.Laps;

public class EmsLap : EmsDomainBase<EmsLapException>
{
    [Newtonsoft.Json.JsonConstructor]
    EmsLap() { }

    public EmsLap(IEmsLapState state)
        : base(GENERATE_ID)
    {
        IsFinal = state.IsFinal;
        OrderBy = state.OrderBy;
        LengthInKm = state.LengthInKm;
        MaxRecoveryTimeInMins = state.MaxRecoveryTimeInMins;
        RestTimeInMins = state.RestTimeInMins;
        IsCompulsoryInspectionRequired = state.IsCompulsoryInspectionRequired;
    }

    public bool IsFinal { get; internal set; }
    public int OrderBy { get; internal set; }
    public double LengthInKm { get; internal set; }
    public int MaxRecoveryTimeInMins { get; internal set; }
    public int RestTimeInMins { get; internal set; }
    public bool IsCompulsoryInspectionRequired { get; internal set; }
}
