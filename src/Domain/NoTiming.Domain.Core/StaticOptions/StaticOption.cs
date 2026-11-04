using NoTiming.Domain.Aggregates;
using NoTiming.Domain.Core.StaticOptions.Regions;
using NoTiming.Domain.Settings;

namespace NoTiming.Domain.Core.StaticOptions;

public class StaticOption
{
    public static bool IsVisionDetectionEnabled()
    {
        return false;
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

    public static IRegionOption? Regional { get; private set; } = new BulgariaOption();
    public static Country? SelectedCountry => StaticSettings.SelectedCountry;
}
