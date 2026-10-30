using NoTiming.Ui.Features.Participants;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// What the Ui asks the Api to search for (#639, ADR-0010): the Api takes a text with a word of two characters at least and
/// answers 400 to another, so a text it would refuse is not sent, and it takes six words, so no more are.
/// </summary>
public sealed class ParticipantSearchTextTests
{
    [Theory]
    [InlineData("ab")]
    [InlineData("  ab  ")]
    [InlineData("a bc")]
    [InlineData("Ли")]
    [InlineData("Мария")]
    [InlineData("a b c d e ab")] // the sixth word is the last that the Api is asked for, and it is long enough
    public void A_text_with_a_word_of_two_characters_is_searched_for(string text)
    {
        Assert.True(ParticipantService.IsSearchable(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")]
    [InlineData("a b c")]
    [InlineData("Я")]
    [InlineData("a b c d e f ab")] // the long word is the seventh, and the Api is asked for the first six
    public void A_text_without_one_is_not(string? text)
    {
        Assert.False(ParticipantService.IsSearchable(text));
    }
}
