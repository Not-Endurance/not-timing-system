namespace NTS.Domain.Objects;

public record TimeInterval : IComparable<TimeInterval>
{
    public static implicit operator TimeSpan?(TimeInterval? interval)
    {
        return interval?.ToTimeSpan();
    }

    public static TimeInterval? operator +(TimeInterval? one, TimeInterval? two)
    {
        if (one == null || two == null)
        {
            return null;
        }
        return new TimeInterval(one!._interval + two!._interval);
    }

    public static TimeInterval? operator -(TimeInterval? one, TimeInterval? two)
    {
        if (one == null || two == null)
        {
            return null;
        }
        return new TimeInterval(one!._interval - two!._interval);
    }

    public static Speed? operator /(double num, TimeInterval? interval)
    {
        if (interval == null)
        {
            return null;
        }
        return new Speed(num, interval.ToTotalHours());
    }

    TimeInterval() { }

    public TimeInterval(TimeSpan timeSpan)
    {
        _interval = timeSpan;
    }
#pragma warning disable IDE1006 // TODO: serialize internal propety using custom JsonConverter<TimeInterval>
    TimeSpan _interval { get; set; }
#pragma warning restore IDE1006 // Naming Styles

    public static TimeInterval Zero => new(TimeSpan.Zero);

    public double ToTotalHours()
    {
        return _interval.TotalHours;
    }

    public TimeSpan ToTimeSpan()
    {
        return _interval;
    }

    public override string ToString()
    {
        // TODO: use FormattingHelper from Startlist PR
        return _interval.ToString(_interval.TotalDays > 1 ? @"dd\.hh\:mm\:ss" : @"hh\:mm\:ss");
    }

    public int CompareTo(TimeInterval? other)
    {
        return _interval.CompareTo(other?._interval);
    }
}
