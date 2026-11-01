using Not.Application.Behinds.Adapters;

namespace NTS.Tests.Unit.Harness;

internal sealed class Counting : NStatefulService
{
    readonly TaskCompletionSource _firstRunStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly TaskCompletionSource _firstRunMayEnd = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task FirstRunStarted => _firstRunStarted.Task;

    /// <summary>The first run does not end until it is let to, so that a test can reset the service meanwhile.</summary>
    public bool HoldsFirstRun { get; set; }

    public string Current { get; set; } = "before";
    public string? State { get; private set; }
    public int Runs { get; private set; }
    public bool Succeeds { get; set; } = true;

    protected override async Task<bool> InitializeState()
    {
        var run = ++Runs;
        var seen = Current;
        if (run == 1 && HoldsFirstRun)
        {
            _firstRunStarted.SetResult();
            await _firstRunMayEnd.Task;
        }

        State = seen;
        return Succeeds;
    }

    public Task Reload()
    {
        return ReloadState();
    }

    public void ReleaseFirstRun()
    {
        _firstRunMayEnd.SetResult();
    }
}
