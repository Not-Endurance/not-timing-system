namespace Not.Extensions;

public static class DateTimeOffsetExtension
{
    public static DateTimeOffset ToDateTimeOffset(this DateTime time)
    {
        var timeWithSpecifiedKind = DateTime.SpecifyKind(time, DateTimeKind.Local);
        var offsetTime = timeWithSpecifiedKind;
        return offsetTime;
    }


    /// <summary>
    /// ToDateTimeOffset creates a new DateTimeOffset from a TimeSpan. 
    /// The DateTimeOffset's day defaults to the one following the current date to ensure scheduling events doesn't happen in the past.
    /// To set the date to the current day pass 'true' as the second argument.
    /// </summary>
    public static DateTimeOffset ToDateTimeOffset(this TimeSpan timeToBeAdded, bool enforceDateToCurrentDay = false)
    {
        var daysToAdd = 1;
        if (enforceDateToCurrentDay)
        {
            daysToAdd = 0;
        }
        var today = DateTime.Today.AddDays(daysToAdd);
        var time = today.Add(timeToBeAdded);
        var timeWithSpecifiedKind = DateTime.SpecifyKind(time, DateTimeKind.Local);
        var offsetTime = timeWithSpecifiedKind;
        return offsetTime;
    }
}
