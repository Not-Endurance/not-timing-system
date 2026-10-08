using Not.Injection;

namespace NoTiming.Ui.Features.Core.Dashboard;

/// <summary>How long the app waits before it sends a group that was not answered again; a test steps through it.</summary>
public interface ISnapshotResendTimer
{
    Task Wait(TimeSpan span, CancellationToken cancellationToken);
}

public sealed class SnapshotResendTimer : ISnapshotResendTimer, IScoped
{
    public Task Wait(TimeSpan span, CancellationToken cancellationToken)
    {
        return Task.Delay(span, cancellationToken);
    }
}
