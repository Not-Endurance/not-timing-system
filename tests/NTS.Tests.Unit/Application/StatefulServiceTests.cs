using Not.Application.Behinds.Adapters;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// A service that keeps state loads it once, and loads it again when it is reset. A reset that comes while the state is
/// being made says that what is being made is out of date, so it is made again: a service that took it for loaded would
/// show the state of before the reset until the next one (#645: a person who signs out as the profile is still being read).
/// </summary>
public sealed class StatefulServiceTests
{
    [Fact]
    public async Task The_state_is_made_once_until_it_is_reset()
    {
        var service = new Counting();

        await service.Load();
        await service.Load();
        service.ResetHasLoaded();
        service.Current = "changed";
        await service.Load();
        await service.Load();

        Assert.Equal(2, service.Runs);
        Assert.Equal("changed", service.State);
    }

    [Fact]
    public async Task A_reload_that_comes_while_the_state_is_being_made_makes_it_again_from_what_is_there_now()
    {
        var service = new Counting { HoldsFirstRun = true };
        var firstLoad = service.Load();
        await service.FirstRunStarted;
        service.Current = "after";

        var reload = service.Reload();
        service.ReleaseFirstRun();
        await Task.WhenAll(firstLoad, reload);

        Assert.Equal("after", service.State);
        Assert.Equal(2, service.Runs);
        await service.Load();
        Assert.Equal(2, service.Runs); // and then it is loaded
    }

    [Fact]
    public async Task A_service_that_says_it_could_not_make_its_state_makes_it_at_the_next_load()
    {
        var service = new Counting { Succeeds = false };

        await service.Load();
        service.Succeeds = true;
        await service.Load();
        await service.Load();

        Assert.Equal(2, service.Runs);
    }

    sealed class Counting : NStatefulService
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

        public Task Reload()
        {
            return ReloadState();
        }

        public void ReleaseFirstRun()
        {
            _firstRunMayEnd.SetResult();
        }

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
    }
}
