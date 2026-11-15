namespace NC.Server.Tests.Authentication.Fakes;

/// <summary>A clock that always answers the same instant.</summary>
internal sealed class FixedTime : TimeProvider
{
    readonly DateTimeOffset _now;

    public FixedTime(DateTimeOffset now)
    {
        _now = now;
    }

    public override DateTimeOffset GetUtcNow()
    {
        return _now;
    }
}
