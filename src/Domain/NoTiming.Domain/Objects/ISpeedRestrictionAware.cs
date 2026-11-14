namespace NoTiming.Domain.Objects;

// An entity that has speed limits. The limits are read through Restrictions.
public interface ISpeedRestrictionAware
{
    SpeedRestrictions Restrictions { get; }
}
