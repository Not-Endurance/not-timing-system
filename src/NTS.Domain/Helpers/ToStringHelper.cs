namespace NTS.Domain.Helpers;

public static class ToStringHelper
{
    public static string FormatSpeedRestrictions(object? minRestriction, object? maxRestriction)
    {
        if (minRestriction != null && maxRestriction != null)
        {
            return $"({minRestriction}-{maxRestriction} {km_per_hour_string})";
        }
        if (minRestriction != null && maxRestriction == null)
        {
            return $"({min_string}:{minRestriction} {km_per_hour_string})";
        }
        if (minRestriction == null && maxRestriction != null)
        {
            return $"({max_string}:{maxRestriction} {km_per_hour_string})";
        }
        return "";
    }
}
