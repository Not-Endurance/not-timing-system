using NTS.Tests.Unit.Harness;
using Not.Async;

namespace NTS.Tests.Unit.Not.Async;

/// <summary>
/// The retry loop through its public surface: it waits, doubling up to the longest wait, tries the work again after each wait,
/// and stops when the work reports nothing is left, when it can no longer be retried, or when it is cancelled. It catches no
/// failure of the work: a failure ends the loop.
/// </summary>
public sealed class RetryUntilDoneTests
{
    static readonly TimeSpan FIRST = TimeSpan.FromSeconds(5);
    static readonly TimeSpan LONGEST = TimeSpan.FromSeconds(20);

    [Fact]
    public async Task It_waits_first_then_doubles_the_wait_up_to_the_longest_between_tries()
    {
        var timer = new StepTimer();
        var work = new CountingWork(answerAfter: 99);
        using var stop = new CancellationTokenSource();

        var run = RetryUntilDone.Run(timer, work, FIRST, LONGEST, stop.Token);
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            await Until(() => timer.IsWaiting);
            timer.Elapse();
            await Until(() => work.Tries == attempt);
        }

        stop.Cancel();
        await run;
        // The wait after the fourth try was begun before the cancellation ended it: the four before it are the ones under test.
        Assert.Equal([5, 10, 20, 20], timer.Spans.Take(4).Select(x => (int)x.TotalSeconds));
    }

    [Fact]
    public async Task It_stops_when_the_work_reports_that_nothing_is_left()
    {
        var timer = new StepTimer();
        var work = new CountingWork(answerAfter: 2);

        var run = RetryUntilDone.Run(timer, work, FIRST, LONGEST, CancellationToken.None);
        await Until(() => timer.IsWaiting);
        timer.Elapse();
        await Until(() => work.Tries == 1);
        await Until(() => timer.IsWaiting);
        timer.Elapse();

        await run;
        Assert.Equal(2, work.Tries);
        Assert.Equal(2, timer.Spans.Count);
    }

    [Fact]
    public async Task It_stops_when_the_work_can_no_longer_be_retried()
    {
        var timer = new StepTimer();
        var work = new CountingWork(answerAfter: 99) { CanRetry = false };

        await RetryUntilDone.Run(timer, work, FIRST, LONGEST, CancellationToken.None);

        Assert.Empty(timer.Spans);
        Assert.Equal(0, work.Tries);
    }

    [Fact]
    public async Task A_cancellation_during_a_wait_ends_the_loop_without_another_try()
    {
        var timer = new StepTimer();
        var work = new CountingWork(answerAfter: 99);
        using var stop = new CancellationTokenSource();

        var run = RetryUntilDone.Run(timer, work, FIRST, LONGEST, stop.Token);
        await Until(() => timer.IsWaiting);
        stop.Cancel();

        await run;
        Assert.Equal(0, work.Tries);
    }

    [Fact]
    public async Task A_failure_of_the_work_is_not_caught_by_the_loop()
    {
        var timer = new StepTimer();
        var work = new CountingWork(answerAfter: 99) { Fails = new InvalidOperationException("The work broke.") };

        var run = RetryUntilDone.Run(timer, work, FIRST, LONGEST, CancellationToken.None);
        await Until(() => timer.IsWaiting);
        timer.Elapse();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => run);
        Assert.Equal("The work broke.", error.Message);
    }

    static async Task Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The retry loop did not reach the expected step.");
            }

            await Task.Delay(1);
        }
    }

}
