using NC.Blazor.Dialogs.Abstractions;
using NoTiming.Domain.Core.Objects.Documents;
using NoTiming.Judge.Contracts.Features.Core.Handouts;

namespace NoTiming.Judge.Blazor.Features.Core.Handouts;

public class HandoutsPrintConfirmationDialogBehind : NDialog
{
    [Inject]
    IHandoutsService Handouts { get; set; } = default!;

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
