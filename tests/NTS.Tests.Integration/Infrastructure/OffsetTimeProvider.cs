namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// The clock of a host that keeps its own time and can be moved forward: what a scenario does to make an Event Historic,
/// which is to let the end of its last day pass (ADR-0007, #628), without freezing the host's clock for what else it does.
/// </summary>
public sealed class OffsetTimeProvider : TimeProvider
{
    long _offsetTicks;

    public TimeSpan Offset => TimeSpan.FromTicks(Interlocked.Read(ref _offsetTicks));

    public void Advance(TimeSpan by)
    {
        Interlocked.Add(ref _offsetTicks, by.Ticks);
    }

    public override DateTimeOffset GetUtcNow()
    {
        return base.GetUtcNow() + Offset;
    }
}
