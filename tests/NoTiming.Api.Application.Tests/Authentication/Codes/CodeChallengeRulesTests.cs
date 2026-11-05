using NoTiming.Api.Application.Authentication.Codes;

namespace NoTiming.Api.Application.Tests.Authentication.Codes;

public sealed class CodeChallengeRulesTests
{
    static readonly DateTimeOffset Issued = new(2026, 11, 5, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Issue_WhenCalled_ShouldExpireTenMinutesLater()
    {
        var state = CodeChallengeRules.Issue(Issued);

        Assert.Equal(Issued.AddMinutes(10), state.ExpiresAtUtc);
    }

    [Fact]
    public void Issue_WhenCalled_ShouldAllowFiveAttempts()
    {
        var state = CodeChallengeRules.Issue(Issued);

        Assert.Equal(5, state.AttemptsLeft);
    }

    [Fact]
    public void Verify_WhenCodeMatches_ShouldAccept()
    {
        var state = CodeChallengeRules.Issue(Issued);

        var result = CodeChallengeRules.Verify(state, codeMatches: true, at: Issued.AddMinutes(1));

        Assert.True(result.Accepted);
    }

    [Fact]
    public void Verify_WhenCodeDoesNotMatch_ShouldReject()
    {
        var state = CodeChallengeRules.Issue(Issued);

        var result = CodeChallengeRules.Verify(state, codeMatches: false, at: Issued.AddMinutes(1));

        Assert.False(result.Accepted);
    }

    [Fact]
    public void Verify_WhenCodeDoesNotMatch_ShouldUseOneAttempt()
    {
        var state = CodeChallengeRules.Issue(Issued);

        var result = CodeChallengeRules.Verify(state, codeMatches: false, at: Issued.AddMinutes(1));

        Assert.Equal(4, result.State.AttemptsLeft);
    }

    [Fact]
    public void Verify_WhenFifthAttemptMatches_ShouldAccept()
    {
        var state = Wrong(CodeChallengeRules.Issue(Issued), times: 4);

        var result = CodeChallengeRules.Verify(state, codeMatches: true, at: Issued.AddMinutes(1));

        Assert.True(result.Accepted);
    }

    [Fact]
    public void Verify_WhenSixthAttemptMatches_ShouldReject()
    {
        var state = Wrong(CodeChallengeRules.Issue(Issued), times: 5);

        var result = CodeChallengeRules.Verify(state, codeMatches: true, at: Issued.AddMinutes(1));

        Assert.False(result.Accepted);
    }

    [Fact]
    public void Verify_WhenExpired_ShouldRejectEvenACorrectCode()
    {
        var state = CodeChallengeRules.Issue(Issued);

        var result = CodeChallengeRules.Verify(state, codeMatches: true, at: Issued.AddMinutes(10));

        Assert.False(result.Accepted);
    }

    [Fact]
    public void Verify_WhenAlreadyAccepted_ShouldRejectTheSameCodeAgain()
    {
        var state = CodeChallengeRules.Issue(Issued);
        var first = CodeChallengeRules.Verify(state, codeMatches: true, at: Issued.AddMinutes(1));

        var second = CodeChallengeRules.Verify(first.State, codeMatches: true, at: Issued.AddMinutes(2));

        Assert.False(second.Accepted);
    }

    static CodeChallengeState Wrong(CodeChallengeState state, int times)
    {
        for (var i = 0; i < times; i++)
        {
            state = CodeChallengeRules.Verify(state, codeMatches: false, at: Issued.AddMinutes(1)).State;
        }

        return state;
    }
}
