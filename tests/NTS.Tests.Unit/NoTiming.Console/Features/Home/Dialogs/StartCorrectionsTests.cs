using NoTiming.Console.Features.Home.Dialogs;
using NTS.Domain.Setup.Services.StartValidation;

namespace NTS.Tests.Unit.NoTiming.Console.Features.Home.Dialogs;

/// <summary>What the person settles about a start number that two Competitions hold at once (#646): the Competition it is taken out of.</summary>
public sealed class StartCorrectionsTests
{
    static readonly Guid FIRST = TestId.Of(1);
    static readonly Guid SECOND = TestId.Of(2);
    static readonly Guid THIRD = TestId.Of(3);

    [Fact]
    public void A_Participation_is_taken_out_of_the_Competition_that_was_chosen_for_it_and_of_no_other()
    {
        var issues = new[] { Issue(7, FIRST, SECOND), Issue(9, FIRST, SECOND, THIRD) };
        var chosen = new Dictionary<int, Guid> { [7] = FIRST, [9] = SECOND };

        var removals = StartCorrections.Removals(issues, chosen).ToList();

        Assert.Equal([(7, FIRST), (9, SECOND)], removals);
    }

    [Fact]
    public void The_corrections_are_applied_when_there_is_something_to_settle_and_every_number_has_its_choice()
    {
        var issues = new[] { Issue(7, FIRST, SECOND), Issue(9, FIRST, SECOND) };

        Assert.False(StartCorrections.CanApply([], new Dictionary<int, Guid>())); // nothing to settle
        Assert.False(StartCorrections.CanApply(issues, new Dictionary<int, Guid> { [7] = FIRST })); // one is left to choose
        Assert.True(StartCorrections.CanApply(issues, new Dictionary<int, Guid> { [7] = FIRST, [9] = SECOND }));
    }

    [Fact]
    public void An_issue_that_no_choice_settles_stops_the_corrections()
    {
        var chosen = new Dictionary<int, Guid> { [7] = FIRST };
        var issues = new[] { Issue(7, FIRST, SECOND), new StartValidationIssue("A Competition has no Phases.") };
        var oneCompetition = new[] { Issue(7, FIRST) }; // a number that only one Competition holds is no conflict

        Assert.False(StartCorrections.CanApply(issues, chosen));
        Assert.False(StartCorrections.CanApply(oneCompetition, chosen));
    }

    static StartValidationIssue Issue(int number, params Guid[] competitions)
    {
        return new StartValidationIssue(
            number,
            $"Athlete {number}",
            $"Horse {number}",
            [.. competitions.Select((x, index) => new StartValidationCompetition(x, $"Competition {index}", "20/20"))]
        );
    }
}
