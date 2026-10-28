using NTS.Domain.Setup.Services.Identities;

namespace NTS.Tests.Unit.Domain.NTS.Domain.Setup.Services.Identities;

/// <summary>The legacy id that two copies are compared by (#635, ADR-0009): the text of the number, and no id for 0 (ADR-0010).</summary>
public sealed class IdentityLegacyIdTests
{
    [Theory]
    [InlineData("17", "17")]
    [InlineData("  17 ", "17")]
    [InlineData("0017", "17")]
    [InlineData("A-17", "A-17")]
    [InlineData("0A7", "0A7")]
    public void An_id_is_the_text_of_the_number_without_the_zeros_it_was_written_with(string legacyId, string expected)
    {
        Assert.Equal(expected, IdentityLegacyIds.Of(legacyId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")]
    [InlineData("000")]
    [InlineData(" 0 ")]
    public void Nothing_and_zero_are_no_id(string? legacyId)
    {
        Assert.Null(IdentityLegacyIds.Of(legacyId));
    }

    [Fact]
    public void An_id_with_a_zero_among_other_characters_is_an_id()
    {
        Assert.Equal("A0", IdentityLegacyIds.Of("A0"));
        Assert.Equal("10", IdentityLegacyIds.Of("010"));
    }
}
