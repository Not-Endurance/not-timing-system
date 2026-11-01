namespace Not.Test.Tests;

public class JourneyRunnerTests
{
    const string FEATURE = "Sign in";
    const string JOURNEY = "Sign in with an emailed code";
    const string SCENARIO = "the code is entered";

    [Fact]
    public async Task RunAsync_runs_every_stage_in_order_when_all_pass()
    {
        var ran = new List<int>();

        await JourneyRunner.RunAsync(FEATURE, JOURNEY, SCENARIO, [Passing(ran, 1), Passing(ran, 2), Passing(ran, 3)]);

        Assert.Equal(new[] { 1, 2, 3 }, ran);
    }

    [Fact]
    public async Task RunAsync_stops_at_the_first_stage_that_fails()
    {
        var ran = new List<int>();

        await Assert.ThrowsAsync<JourneyFailedException>(() =>
            JourneyRunner.RunAsync(FEATURE, JOURNEY, SCENARIO, [Passing(ran, 1), Failing(ran, 2, new InvalidOperationException("no code")), Passing(ran, 3)])
        );

        Assert.Equal(new[] { 1, 2 }, ran);
    }

    [Fact]
    public async Task RunAsync_names_the_feature_journey_scenario_and_stage_it_stopped_at()
    {
        var ran = new List<int>();

        var error = await Assert.ThrowsAsync<JourneyFailedException>(() =>
            JourneyRunner.RunAsync(FEATURE, JOURNEY, SCENARIO, [Passing(ran, 1), Failing(ran, 2, new InvalidOperationException("no code"))])
        );

        Assert.Equal(FEATURE, error.Feature);
        Assert.Equal(JOURNEY, error.Journey);
        Assert.Equal(SCENARIO, error.Scenario);
        Assert.Equal(2, error.Stage.Number);
        Assert.Equal(
            "Sign in > Sign in with an emailed code > the code is entered stopped at stage 2 (Stage 2): no code",
            error.Message
        );
    }

    [Fact]
    public async Task RunAsync_keeps_the_failure_of_the_stage_as_the_inner_exception()
    {
        var failure = new InvalidOperationException("no code");
        var ran = new List<int>();

        var error = await Assert.ThrowsAsync<JourneyFailedException>(() =>
            JourneyRunner.RunAsync(FEATURE, JOURNEY, SCENARIO, [Failing(ran, 1, failure)])
        );

        Assert.Same(failure, error.InnerException);
    }

    static JourneyStage Passing(List<int> ran, int number)
    {
        return new JourneyStage(
            number,
            $"Stage {number}",
            () =>
            {
                ran.Add(number);
                return Task.CompletedTask;
            }
        );
    }

    static JourneyStage Failing(List<int> ran, int number, Exception failure)
    {
        return new JourneyStage(
            number,
            $"Stage {number}",
            () =>
            {
                ran.Add(number);
                throw failure;
            }
        );
    }
}
