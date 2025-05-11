using MudBlazor;

namespace Not.Blazor.Dialogs;

public static class MudDialogExtensions
{
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
