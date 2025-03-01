using Not.Domain.Base;

namespace NTS.Domain.Objects;

public record Speed : DomainObject
{
    public static Speed? Create(double? speed)
    {
        return speed.HasValue ? new Speed(speed.Value) : null;
    }

    public static implicit operator double?(Speed? speed)
    {
        return speed?._speed;
    }

    public static implicit operator Speed?(double? speed)
    {
        return speed == null ? null : new Speed(speed.Value);
    }

    public static bool operator >(Speed? a, Speed? b)
    {
        return a?._speed > b?._speed;
    }

    public static bool operator <(Speed? a, Speed? b)
    {
        return a?._speed < b?._speed;
    }

    Speed() { }

    Speed(double value)
    {
        _speed = value;
    }

    public Speed(double distance, double totalHours)
    {
        _speed = distance / totalHours;
    }

    public Speed(double distance, TimeInterval interval)
    {
        _speed = distance / interval.ToTotalHours();
    }

#pragma warning disable IDE1006 // TODO: serialize internal property using a custom JsonConverter<Speed>
    public double _speed { get; private set; }
#pragma warning restore IDE1006 // Naming Styles

    public override string ToString()
    {
        return $"{_speed:0.00}";
    }
}
