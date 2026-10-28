namespace Not.Test.Tests;

public class DeadlineWaitTests
{
    [Fact]
    public async Task UntilAsync_returns_at_once_when_the_condition_holds()
    {
        await DeadlineWait.UntilAsync("the order to be paid", () => true, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task UntilAsync_polls_until_the_condition_holds()
    {
        var checks = 0;

        await DeadlineWait.UntilAsync("the third check", () => ++checks == 3, TimeSpan.FromSeconds(5));

        Assert.Equal(3, checks);
    }

    [Fact]
    public async Task UntilAsync_times_out_naming_what_it_waited_for()
    {
        var error = await Assert.ThrowsAsync<TimeoutException>(() =>
            DeadlineWait.UntilAsync("the order to be paid", () => false, TimeSpan.FromMilliseconds(100))
        );

        Assert.Contains("the order to be paid", error.Message);
    }
}
