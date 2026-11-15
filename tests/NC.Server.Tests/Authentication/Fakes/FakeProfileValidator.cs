using NC.Server.Authentication.Interface;

namespace NC.Server.Tests.Authentication.Fakes;

/// <summary>A validator the test defines: it accepts a profile when the rule does.</summary>
internal sealed class FakeProfileValidator : IProfileValidator<TestProfile>
{
    readonly Func<TestProfile, bool> _rule;

    public FakeProfileValidator(Func<TestProfile, bool> rule)
    {
        _rule = rule;
    }

    public bool Accepts(TestProfile profile)
    {
        return _rule(profile);
    }
}
