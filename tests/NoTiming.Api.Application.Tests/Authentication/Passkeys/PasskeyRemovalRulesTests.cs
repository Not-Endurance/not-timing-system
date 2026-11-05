using NoTiming.Api.Application.Authentication.Passkeys;

namespace NoTiming.Api.Application.Tests.Authentication.Passkeys;

public sealed class PasskeyRemovalRulesTests
{
    [Fact]
    public void CanRemove_WhenOnlyOnePasskeyRemains_ShouldRefuse()
    {
        Assert.False(PasskeyRemovalRules.CanRemove(passkeyCount: 1));
    }

    [Fact]
    public void CanRemove_WhenTwoPasskeysRemain_ShouldAllow()
    {
        Assert.True(PasskeyRemovalRules.CanRemove(passkeyCount: 2));
    }
}
