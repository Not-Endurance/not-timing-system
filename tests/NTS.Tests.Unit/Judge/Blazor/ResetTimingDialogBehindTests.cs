using NTS.Judge.Blazor.Layout.Drawer.Reset;

namespace NTS.Judge.Tests.Blazor;

public class ResetTimingDialogBehindTests
{
    [Theory]
    [InlineData("1337", true)]
    [InlineData(" 1337 ", true)]
    [InlineData("01337", false)]
    [InlineData("1338", false)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    public void CanConfirm_WhenInputMatchesRequiredSequence(string input, bool expected)
    {
        var dialog = new TestResetTimingDialogBehind();

        dialog.SetConfirmationCode(input);

        Assert.Equal(expected, dialog.ReadCanConfirm());
    }

    sealed class TestResetTimingDialogBehind : ResetEventDialogBehind
    {
        public void SetConfirmationCode(string value)
        {
            ConfirmationCode = value;
        }

        public bool ReadCanConfirm()
        {
            return CanConfirm;
        }
    }
}
