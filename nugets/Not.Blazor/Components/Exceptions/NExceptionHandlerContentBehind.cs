using MudBlazor;
using Not.Blazor.Components.Abstractions;
using Not.Localization;

namespace Not.Blazor.Components.Exceptions;

public class NExceptionHandlerContentBehind : NComponent
{
    public const string IMAGE_URL_PARAMETER = "NExceptionHandlerImageUrl";
    public const string ERROR_TEXT_PARAMETER = "NExceptionHandlerErrorText";

    [Inject]
    NavigationManager Navigator { get; set; } = default!;

    [CascadingParameter]
    protected MudDialogInstance? CurrentDialog { get; set; }

    protected string? BackdropImageUrl => CascadedBackdropImageUrl;
    protected string ErrorText =>
        string.IsNullOrWhiteSpace(CascadedErrorText)
            ? string.Empty
            : LocalizationHelper.LocalizeString(CascadedErrorText);

    protected bool HasBackdropImage => !string.IsNullOrWhiteSpace(BackdropImageUrl);

    [CascadingParameter(Name = IMAGE_URL_PARAMETER)]
    public string? CascadedBackdropImageUrl { get; set; }

    [CascadingParameter(Name = ERROR_TEXT_PARAMETER)]
    public string? CascadedErrorText { get; set; }

    protected void GoHome()
    {
        try
        {
            Navigator.NavigateTo("/", forceLoad: true);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected void Reload()
    {
        try
        {
            if (CurrentDialog != null)
            {
                CurrentDialog.Cancel();
                return;
            }

            Navigator.NavigateTo(Navigator.Uri, forceLoad: true);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
