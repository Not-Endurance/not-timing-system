using NTS.Domain.Aggregates;
using NTS.Domain.Settings;

namespace NTS.Domain.Core.StaticOptions;

public class StaticOption
{
    public static bool IsRfidDetectionEnabled()
    {
        return Detection != default && Detection == DetectionMode.Rfid;
    }

    public static SnapshotType GetRfidSnapshotType()
    {
        return SnapshotType.Stage;
    }

    public static bool IsVisionDetectionEnabled()
    {
        return Detection != default && Detection == DetectionMode.ComputerVision;
    }

    public static bool ShouldOnlyUseAverageLoopSpeed(CompetitionRuleset ruleset)
    {
        if (ruleset == CompetitionRuleset.Regional && Regional != null)
        {
            return Regional.ShouldOnlyUseAverageLoopSpeed;
        }
        return false;
    }

    public static bool ShouldUseRegionalRanker(CompetitionRuleset ruleset)
    {
        if (ruleset == CompetitionRuleset.Regional)
        {
            return true;
        }
        return false;
    }

    public static IRegionOption? Regional { get; private set; } // TODO: fix regional options
    public static DetectionMode? Detection => StaticSettings.DetectionMode;
    public static Country? SelectedCountry => StaticSettings.SelectedCountry;
}
