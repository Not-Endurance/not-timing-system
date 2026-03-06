using MudBlazor;

namespace Not.Blazor.Helpers;

public static class MudBlazorExtensions
{
    public static void SetVisibleDuration(this CommonSnackbarOptions options, TimeSpan duration)
    {
        options.VisibleStateDuration = (int)duration.TotalMilliseconds;
    }

    public static async Task<bool> IsCanceled(this IDialogReference? dialog)
    {
        if (dialog is null)
        {
            return true;
        }
        var result = await dialog.Result;
        return result?.Canceled ?? false;
    }
}
