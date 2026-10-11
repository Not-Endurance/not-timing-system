using Not.Blazor.Dialogs.Abstractions;
using NTS.Contracts.Features.Console;
using NTS.Domain.Core.Objects.Documents;

namespace NoTiming.Console.Features.Handouts;

/// <summary>
/// Asks whether the Handouts that were handed to the browser were printed, because the Console cannot know: the browser says
/// nothing of what happened to the page it printed. A person who says so has them removed, which cannot be undone.
/// </summary>
public class HandoutsPrintConfirmationDialogBehind : NDialog
{
    [Inject]
    IConsoleHandouts Handouts { get; set; } = default!;

    [Parameter]
    public IReadOnlyList<ResultsDocument> Documents { get; set; } = [];

    protected async Task ConfirmAndDelete()
    {
        try
        {
            await Handouts.Delete(Documents);
            await ConfirmDialog();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
