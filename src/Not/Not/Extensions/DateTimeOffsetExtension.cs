namespace Not.Extensions;

public static class DateTimeOffsetExtension
{
    public static DateTimeOffset ToDateTimeOffset(this DateTime time)
    {
        var timeWithSpecifiedKind = DateTime.SpecifyKind(time, DateTimeKind.Local);
        var offsetTime = timeWithSpecifiedKind;
        return offsetTime;
    }

    public static DateTimeOffset ToDateTimeOffset(this TimeSpan setTime, DateTime? setDate = null)
    {
        if (setDate == null)
        {
            var today = DateTime.Today;
            today.Add(setTime);
            return today.ToDateTimeOffset();
        }
        else
        {
            var date = (DateTime)setDate;
            var dateTime = date.Date.Add(setTime);
            return dateTime.ToDateTimeOffset();
        }
    }
}
