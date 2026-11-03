using NoTiming.Ui.Features.Participants;
using NTS.Contracts.Features.Participants;

namespace NTS.Tests.Unit.Harness;

internal sealed class Clock : TimeProvider
{
    public DateTimeOffset Now { get; set; }

    public override DateTimeOffset GetUtcNow()
    {
        return Now;
    }
}
