using Not.Application.Environments;
using NTS.Application.Contracts;
using NTS.Localization;

namespace NTS.Witness.Blazor.Layout;

public class MainLayoutBehind : LayoutComponentBase
{
    const string EXCEPTION_HANDLER_IMAGE_URL = "appicon.svg";
    const string EXCEPTION_HANDLER_ERROR_TEXT =
        nameof(NtsStrings.Sorry_we_seem_to_have_fallen_off_the_horseback_string);

    [Inject]
    IEnvironmentContext Environment { get; set; } = default!;

    protected string ExceptionHandlerErrorText => EXCEPTION_HANDLER_ERROR_TEXT;
    protected string ExceptionHandlerImageUrl => EXCEPTION_HANDLER_IMAGE_URL;

    protected string LayoutWatermark =>
        NtsClientDisplayFormatter.FormatTitle(ApplicationConstants.NO_TIMING_SYSTEM, Environment);
}
