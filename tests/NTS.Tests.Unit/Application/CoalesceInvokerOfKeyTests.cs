using Not.Async;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// The keyed coalescing invoker through its public surface: one run at a time for a key, exactly one more run for the
/// calls that arrive during it, every caller waits for it, and no call is lost.
/// </summary>
public sealed class CoalesceInvokerOfKeyTests
{
    [Fact]
    public async Task A_call_with_nothing_in_flight_runs_the_action_once_and_completes()
    {
        var invoker = new CoalesceInvoker<int>();
        var runs = 0;

        await invoker.Invoke(
            1,
            () =>
            {
                runs++;
                return Task.CompletedTask;
            }
        );

        Assert.Equal(1, runs);
    }

    [Fact]
    public async Task Calls_that_arrive_during_a_run_cause_exactly_one_more_run_and_every_caller_waits_for_it()
    {
        var invoker = new CoalesceInvoker<int>();
        var held = new Held();

        var first = invoker.Invoke(1, held.Run);
        var second = invoker.Invoke(1, held.Run);
        var third = invoker.Invoke(1, held.Run);

        Assert.Equal(1, held.Started); // one run, however many calls came during it
        Assert.False(first.IsCompleted);

        held.Release(0);
        await Until(() => held.Started == 2); // the trailing run
        Assert.False(first.IsCompleted); // the callers wait for the trailing run
        Assert.False(third.IsCompleted);

        held.Release(1);
        await Task.WhenAll(first, second, third);

        Assert.Equal(2, held.Started); // exactly one more, not one per call
    }

    [Fact]
    public async Task Calls_for_different_keys_run_at_once_and_do_not_wait_for_each_other()
    {
        var invoker = new CoalesceInvoker<int>();
        var one = new Held();
        var two = new Held();

        var first = invoker.Invoke(1, one.Run);
        var second = invoker.Invoke(2, two.Run);

        Assert.Equal(1, one.Started);
        Assert.Equal(1, two.Started);
        two.Release(0);
        await second;
        Assert.False(first.IsCompleted);
        one.Release(0);
        await first;
    }

    [Fact]
    public async Task The_trailing_run_uses_the_action_of_the_latest_call()
    {
        var invoker = new CoalesceInvoker<int>();
        var ran = new List<string>();
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = invoker.Invoke(
            1,
            () =>
            {
                ran.Add("first");
                return held.Task;
            }
        );
        var second = invoker.Invoke(
            1,
            () =>
            {
                ran.Add("second");
                return Task.CompletedTask;
            }
        );
        var third = invoker.Invoke(
            1,
            () =>
            {
                ran.Add("third");
                return Task.CompletedTask;
            }
        );
        held.SetResult();
        await Task.WhenAll(first, second, third);

        Assert.Equal(["first", "third"], ran);
    }

    [Fact]
    public async Task A_failure_reaches_every_caller_that_waited_and_the_key_can_run_again()
    {
        var invoker = new CoalesceInvoker<int>();
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = invoker.Invoke(1, () => held.Task);
        var second = invoker.Invoke(1, () => held.Task);
        held.SetException(new InvalidOperationException("The read failed."));

        await Assert.ThrowsAsync<InvalidOperationException>(() => first);
        await Assert.ThrowsAsync<InvalidOperationException>(() => second);
        var again = 0;
        await invoker.Invoke(
            1,
            () =>
            {
                again++;
                return Task.CompletedTask;
            }
        );
        Assert.Equal(1, again);
    }

    [Fact]
    public async Task A_call_is_never_lost_when_calls_race_with_the_end_of_a_run()
    {
        var invoker = new CoalesceInvoker<int>();
        long clock = 0;
        long lastRunStartedAt = 0;
        var lost = 0;

        async Task Run()
        {
            Interlocked.Exchange(ref lastRunStartedAt, Interlocked.Increment(ref clock));
            await Task.Yield();
        }

        await Task.WhenAll(
            Enumerable
                .Range(0, 8)
                .Select(_ =>
                    Task.Run(async () =>
                    {
                        for (var call = 0; call < 1000; call++)
                        {
                            var calledAt = Interlocked.Increment(ref clock);
                            await invoker.Invoke(1, Run);

                            // A call is served by a run that started after it: when it returns, one has.
                            if (Interlocked.Read(ref lastRunStartedAt) < calledAt)
                            {
                                Interlocked.Increment(ref lost);
                            }
                        }
                    })
                )
        );

        Assert.Equal(0, lost);
    }

    static async Task Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition did not hold in time.");
            await Task.Delay(5);
        }
    }

    /// <summary>An action whose runs stay open until a test releases them, one by one in the order they started.</summary>
    sealed class Held
    {
        readonly List<TaskCompletionSource> _runs = [];
        int _started;

        public int Started => Volatile.Read(ref _started);

        public Task Run()
        {
            var run = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_runs)
            {
                _runs.Add(run);
            }

            Interlocked.Increment(ref _started);
            return run.Task;
        }

        public void Release(int index)
        {
            lock (_runs)
            {
                _runs[index].SetResult();
            }
        }
    }
}
