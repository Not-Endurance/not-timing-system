using NoTiming.Domain.Aggregates;
using NoTiming.Domain.Settings;

namespace NoTiming.Domain.Core.StaticOptions;

public class StaticOption
{
    public static bool IsVisionDetectionEnabled()
    {
        return false;
    }

    public static Country? SelectedCountry => StaticSettings.SelectedCountry;
}
