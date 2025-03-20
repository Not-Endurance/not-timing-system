using Newtonsoft.Json;
using Not.Domain.Base;

namespace NTS.Domain.Objects;

public record Timestamp : DomainObject, IComparable<Timestamp>
{
    public static Timestamp Now()
    {
        return new Timestamp(DateTimeOffset.UtcNow);
    }

    public static Timestamp? Create(DateTimeOffset? dateTime)
    {
        if (dateTime == null)
        {
            return null;
        }
        return new Timestamp(dateTime.Value.ToUniversalTime());
    }

    public static Timestamp Copy(Timestamp timestamp)
    {
        return new Timestamp(timestamp);
    }

    public static implicit operator Timestamp?(DateTimeOffset? dateTimeOffset)
    {
        return dateTimeOffset == null ? null : new Timestamp(dateTimeOffset.Value);
    }

    public static implicit operator DateTimeOffset?(Timestamp? timestamp)
    {
        return timestamp?.ToDateTimeOffset();
    }

    public static bool operator <(Timestamp? left, Timestamp? right)
    {
        return left?._stamp < right?._stamp;
    }

    public static bool operator >(Timestamp? left, Timestamp? right)
    {
        return left?._stamp > right?._stamp;
    }

    public static bool operator <=(Timestamp? left, Timestamp? right)
    {
        return left?._stamp <= right?._stamp;
    }

    public static bool operator >=(Timestamp? left, Timestamp? right)
    {
        return left?._stamp > right?._stamp;
    }

    public static TimeInterval? operator -(Timestamp? left, Timestamp? right)
    {
        if (left == null || right == null)
        {
            return null;
        }
        return new TimeInterval(left!._stamp - right!._stamp);
    }

    public static Timestamp? operator +(Timestamp? left, TimeSpan? right)
    {
        return left == null ? null : new Timestamp(left!._stamp + (right ?? TimeSpan.Zero));
    }

    Timestamp() { }

    protected Timestamp(Timestamp timestamp)
        : base(timestamp)
    {
        _stamp = timestamp._stamp;
    }

    public Timestamp(DateTimeOffset dateTime)
    {
        _stamp = new DateTimeOffset(
            1337,
            1,
            3,
            dateTime.Hour,
            dateTime.Minute,
            dateTime.Second,
            dateTime.Offset
        ).ToUniversalTime();
    }

    [JsonProperty]
#pragma warning disable IDE1006 // TODO: serialize private setter using custom JsonConverter<Timestamp>
    public DateTimeOffset _stamp { get; set; }
#pragma warning restore IDE1006 // Naming Styles

    public Timestamp Add(TimeSpan span)
    {
        return new Timestamp(_stamp.Add(span));
    }

    public string ToString(string format, IFormatProvider formatProvider)
    {
        return _stamp.LocalDateTime.ToString(format, formatProvider);
    }

    public override string ToString()
    {
        return _stamp.LocalDateTime.ToString("HH:mm:ss");
    }

    public int CompareTo(Timestamp? other)
    {
        return _stamp.CompareTo(other?._stamp ?? DateTimeOffset.MinValue);
    }

    public DateTimeOffset ToDateTimeOffset()
    {
        return _stamp.LocalDateTime;
    }

    public DateTime ToDateTime()
    {
        return _stamp.DateTime;
    }
}
