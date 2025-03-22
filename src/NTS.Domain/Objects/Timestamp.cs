using Newtonsoft.Json;
using Not.Domain.Base;

namespace NTS.Domain.Objects;

public sealed record Timestamp : DomainObject, IComparable<Timestamp>
{
    public static Timestamp Now()
    {
        return new Timestamp(DateTimeOffset.UtcNow);
    }

    public static Timestamp? Create(DateTimeOffset? dateTimeOffset)
    {
        if (dateTimeOffset == null)
        {
            return null;
        }
        return new Timestamp(dateTimeOffset.Value);
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
        return left?._stamp.TimeOfDay < right?._stamp.TimeOfDay;
    }

    public static bool operator >(Timestamp? left, Timestamp? right)
    {
        return left?._stamp.TimeOfDay > right?._stamp.TimeOfDay;
    }

    public static bool operator <=(Timestamp? left, Timestamp? right)
    {
        return left?._stamp.TimeOfDay <= right?._stamp.TimeOfDay;
    }

    public static bool operator >=(Timestamp? left, Timestamp? right)
    {
        return left?._stamp.TimeOfDay > right?._stamp.TimeOfDay;
    }

    public static TimeInterval? operator -(Timestamp? left, Timestamp? right)
    {
        if (left == null || right == null)
        {
            return null;
        }
        return new TimeInterval(left!._stamp.TimeOfDay - right!._stamp.TimeOfDay);
    }

    public static Timestamp? operator +(Timestamp? left, TimeSpan? right)
    {
        return left == null ? null : new Timestamp(left!._stamp.TimeOfDay + (right ?? TimeSpan.Zero));
    }

    Timestamp() { }

    Timestamp(Timestamp timestamp)
        : base(timestamp)
    {
        _stamp = timestamp._stamp;
    }

    Timestamp(TimeSpan timeSpan) : this(DateTimeOffset.Now.Add(timeSpan))
    {
    }

    public Timestamp(DateTimeOffset dateTimeOffset)
    {
        _stamp = dateTimeOffset.ToUniversalTime();
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
        return _stamp;
    }

    public DateTime ToDateTime()
    {
        return _stamp.DateTime;
    }
}
