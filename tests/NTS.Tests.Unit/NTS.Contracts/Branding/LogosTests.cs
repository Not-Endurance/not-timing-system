using NTS.Contracts.Branding;

namespace NTS.Tests.Unit.NTS.Contracts.Branding;

/// <summary>The two logos at the head of a document (#646).</summary>
public sealed class LogosTests
{
    [Fact]
    public void A_position_has_the_logo_of_its_own_and_the_ones_without_any_have_none()
    {
        var left = new Logo("image/png", [1]);
        var right = new Logo("image/jpeg", [2]);
        var both = new Logos(left, right);

        Assert.Same(left, both.At(LogoPosition.Left));
        Assert.Same(right, both.At(LogoPosition.Right));
        Assert.Null(Logos.None.At(LogoPosition.Left));
        Assert.Null(new Logos(null, right).At(LogoPosition.Left));
        Assert.Null(new Logos(left, null).At(LogoPosition.Right));
    }

    [Fact]
    public void A_logo_is_an_address_that_a_page_shows_and_prints_without_asking_for_it()
    {
        var logo = new Logo("image/png", [0x89, 0x50, 0x4E], LogoSource.Event);

        Assert.Equal("data:image/png;base64,iVBO", logo.DataUrl);
        Assert.Equal(3, logo.Size);
        Assert.Equal(LogoSource.Event, logo.Source);
        Assert.Null(new Logo("image/png", [1]).Source);
    }
}
