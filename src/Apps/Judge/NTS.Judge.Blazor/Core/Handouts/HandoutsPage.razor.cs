using MudBlazor;
using Not.Blazor.Dialogs;

namespace NTS.Judge.Blazor.Core.Handouts;

public partial class HandoutsPage
{
    [Inject]
    IHandoutsBehind Behind { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Behind);
    }

    async Task OpenPrintPreview()
    {
        var handouts = Behind.Documents.ToList();
        await OpenPrintDialog();
        var dialog = await DialogService.ShowAsync<HandoutsPrintConfirmationDialog>();
        if (await dialog.IsCanceled())
        {
            return;
        }
        await Behind.Delete(handouts);
    }
}
