using NTS.Judge.Blazor.Layout.Drawer.Deactivate;

namespace NTS.Judge.Tests.Blazor;

public class DeactivateEventDialogBehindTests
{
    [Theory]
    [InlineData("1338", true)]
    [InlineData(" 1338 ", true)]
    [InlineData("01338", false)]
    [InlineData("1337", false)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    public void CanConfirm_WhenInputMatchesRequiredSequence(string input, bool expected)
    {
        var dialog = new TestDeactivateEventDialogBehind();

        dialog.SetConfirmationCode(input);

        Assert.Equal(expected, dialog.ReadCanConfirm());
    }

    sealed class TestDeactivateEventDialogBehind : DeactivateEventDialogBehind
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
