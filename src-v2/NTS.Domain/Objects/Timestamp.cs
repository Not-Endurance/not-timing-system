namespace NTS.Domain;

public record Timestamp : DomainObject, IComparable<Timestamp>
{
    public static Timestamp Now()
    {
        return new Timestamp(DateTimeOffset.Now);
    }

    public static Timestamp? Create(DateTimeOffset? dateTime)
    {
        if (dateTime == null)
        {
            return null;
        }
        return new Timestamp(dateTime.Value);
    }

    public Timestamp(DateTime dateTime)
    {
        DateTime = dateTime;
    }

    public Timestamp(Timestamp timestamp) : base(timestamp)
    {
        DateTime = timestamp.DateTime;
    }
    private Timestamp()
    {
    }
    private Timestamp(DateTimeOffset dateTime)
    {
        DateTime = dateTime;
    }

    public DateTimeOffset DateTime { get; private set; }

    public Timestamp Add(TimeSpan span)
    {
        return new Timestamp(DateTime.Add(span));
    }

    public string ToString(string format, IFormatProvider formatProvider)
    {
        return DateTime.LocalDateTime.ToString(format, formatProvider);
    }
    public override string ToString()
    {
        return DateTime.LocalDateTime.ToString("HH:mm:ss");
    }

    public int CompareTo(Timestamp? other)
    {
        return DateTime.CompareTo(other?.DateTime ?? DateTimeOffset.MinValue);
    }

    public static bool operator <(Timestamp? left, Timestamp? right)
    {
        return left?.DateTime < right?.DateTime;
    }
    public static bool operator >(Timestamp? left, Timestamp? right)
    {
        return left?.DateTime > right?.DateTime;
    }

    public static bool operator <=(Timestamp? left, Timestamp? right)
    {
        return left?.DateTime <= right?.DateTime;
    }
    public static bool operator >=(Timestamp? left, Timestamp? right)
    {
        return left?.DateTime > right?.DateTime;
    }

    public static bool operator <(Timestamp? left, DateTimeOffset? right)
    {
        return left?.DateTime < right;
    }
    public static bool operator >(Timestamp? left, DateTimeOffset? right)
    {
        return left?.DateTime > right;
    }
    public static TimeInterval? operator -(Timestamp? left, Timestamp? right)
    {
        if (left == null || right == null)
        {
            return null;
        }
        return new TimeInterval(left!.DateTime - right!.DateTime);
    }
    public static Timestamp? operator +(Timestamp? left, TimeSpan? right)
    {
        return left == null
            ? null
            : new Timestamp(left!.DateTime + (right ?? TimeSpan.Zero));
    }
}
