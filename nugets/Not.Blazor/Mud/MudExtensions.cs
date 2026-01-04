using MudBlazor;

namespace Not.Blazor.Mud;

public static class MudExtensions
{
    public static void SetVisibleDuration(this CommonSnackbarOptions options, TimeSpan duration)
    {
        options.VisibleStateDuration = (int)duration.TotalMilliseconds;
    }
}
