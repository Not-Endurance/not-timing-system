using NoTiming.Ui.Features.Core;

namespace NTS.Tests.Unit.Application;

/// <summary>The outcome of a time as the Main Operator is told it (#646).</summary>
public sealed class OutcomeWordsTests
{
    [Theory]
    [InlineData("RejectedDuplicateArrive", "Rejected Duplicate Arrive")]
    [InlineData("RejectedSeparateFinishLine", "Rejected Separate Finish Line")]
    [InlineData("Accepted", "Accepted")]
    [InlineData("", "")]
    public void An_outcome_is_its_name_in_words_with_no_space_before_the_first(string name, string expected)
    {
        Assert.Equal(expected, OutcomeWords.Of(name));
    }
}
