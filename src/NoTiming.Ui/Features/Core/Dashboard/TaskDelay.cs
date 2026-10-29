using Not.Async;
using Not.Injection;

namespace NoTiming.Ui.Features.Core.Dashboard;

/// <summary>The wait of the app, in time: a test replaces it with one it steps through.</summary>
public sealed class TaskDelay : IDelay, IScoped
{
    public Task Wait(TimeSpan span, CancellationToken cancellationToken)
    {
        return Task.Delay(span, cancellationToken);
    }
}
