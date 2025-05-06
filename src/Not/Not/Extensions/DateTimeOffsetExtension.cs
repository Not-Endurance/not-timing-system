namespace Not.Extensions;

public static class DateTimeOffsetExtension
{
    public static DateTimeOffset ToDateTimeOffset(this DateTime time)
    {
        var timeWithSpecifiedKind = DateTime.SpecifyKind(time, DateTimeKind.Local);
        var offsetTime = timeWithSpecifiedKind;
        return offsetTime;
    }
}
