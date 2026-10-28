using NoTiming.Api.Features.Tenancy;

namespace NTS.Tests.Integration;

/// <summary>An email that leaves partly masked (ADR-0012): the first character and the domain, never the rest of the name.</summary>
public sealed class EmailMaskTests
{
    [Theory]
    [InlineData("anna.petrova@example.test", "a***@example.test")]
    [InlineData("A@x.test", "A***@x.test")]
    [InlineData("first.last+tag@mail.example.test", "f***@mail.example.test")]
    [InlineData("odd@name@example.test", "o***@example.test")]
    public void A_mask_keeps_the_first_character_of_the_name_and_the_domain(string email, string expected)
    {
        Assert.Equal(expected, EmailMask.Of(email));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-at-sign")]
    [InlineData("@example.test")]
    public void What_is_not_an_email_is_masked_altogether(string? text)
    {
        Assert.Equal("***", EmailMask.Of(text));
    }

    [Fact]
    public void A_mask_never_holds_the_rest_of_the_name()
    {
        var masked = EmailMask.Of("confidential.person@example.test");

        Assert.DoesNotContain("onfidential", masked);
        Assert.DoesNotContain("person", masked);
    }
}
