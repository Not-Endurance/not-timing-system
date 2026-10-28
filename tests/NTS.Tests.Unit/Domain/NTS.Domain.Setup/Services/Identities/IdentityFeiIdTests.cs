using NTS.Domain.Setup.Services.Identities;

namespace NTS.Tests.Unit.Domain.NTS.Domain.Setup.Services.Identities;

/// <summary>
/// The FEI ID that two copies of an Athlete or a Horse are compared by (#635, ADR-0010): an Athlete's as a number, a Horse's as
/// the text it is, trimmed and case-folded; and no FEI ID at all for what is empty, zero or a placeholder.
/// </summary>
public sealed class IdentityFeiIdTests
{
    [Theory]
    [InlineData("10012345", "10012345")]
    [InlineData("0010012345", "10012345")]
    [InlineData("  00012 ", "12")]
    public void An_Athletes_FEI_ID_is_a_number_without_the_zeros_it_was_written_with(string feiId, string expected)
    {
        Assert.Equal(expected, IdentityFeiIds.Of(IdentityKind.Athlete, feiId));
    }

    [Theory]
    [InlineData("10312BK", "10312BK")]
    [InlineData("  103bk17 ", "103BK17")]
    [InlineData("0103BK17", "0103BK17")]
    public void A_Horses_FEI_ID_is_the_text_it_is_trimmed_and_case_folded(string feiId, string expected)
    {
        Assert.Equal(expected, IdentityFeiIds.Of(IdentityKind.Horse, feiId));
    }

    [Fact]
    public void An_Athletes_FEI_ID_that_is_not_a_number_is_compared_as_text()
    {
        Assert.Equal("BUL12345", IdentityFeiIds.Of(IdentityKind.Athlete, " bul12345"));
    }

    [Theory]
    [InlineData(IdentityKind.Athlete)]
    [InlineData(IdentityKind.Horse)]
    public void Nothing_zero_and_a_placeholder_are_no_FEI_ID(IdentityKind kind)
    {
        Assert.Null(IdentityFeiIds.Of(kind, null));
        Assert.Null(IdentityFeiIds.Of(kind, string.Empty));
        Assert.Null(IdentityFeiIds.Of(kind, "   "));
        Assert.Null(IdentityFeiIds.Of(kind, "0"));
        Assert.Null(IdentityFeiIds.Of(kind, "0000"));
        Assert.Null(IdentityFeiIds.Of(kind, "-----"));
        Assert.Null(IdentityFeiIds.Of(kind, "....."));
        Assert.Null(IdentityFeiIds.Of(kind, "?"));
    }

    [Fact]
    public void A_text_that_starts_with_a_sign_but_is_more_than_a_placeholder_is_a_FEI_ID()
    {
        Assert.Equal("-12", IdentityFeiIds.Of(IdentityKind.Horse, " -12"));
        Assert.Equal("#103BK17", IdentityFeiIds.Of(IdentityKind.Horse, "#103bk17"));
        Assert.Equal("--A", IdentityFeiIds.Of(IdentityKind.Horse, "--a"));
    }

    [Fact]
    public void A_text_with_a_letter_among_its_zeros_is_a_FEI_ID()
    {
        Assert.Equal("00A", IdentityFeiIds.Of(IdentityKind.Horse, "00a"));
    }
}
