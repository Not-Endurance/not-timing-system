using MudBlazor;
using Not.Blazor.Components;
using Not.Blazor.Dialogs;
using NTS.Judge.Features.Core.Handouts;

namespace NTS.Judge.Blazor.Core.Handouts;

public class HandoutsPageBehind : PrintableComponent
{
    [Inject]
    IDialogService DialogService { get; set; } = default!;

    [Inject]
    protected IHandoutsBehind Service { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }

    protected async Task OpenPrintPreview()
    {
        var handouts = Service.Documents.ToList();
        await OpenPrintDialog();
        var dialog = await DialogService.ShowAsync<HandoutsPrintConfirmationDialog>();
        if (await dialog.IsCanceled())
        {
            return;
        }
        await Service.Delete(handouts);
    }
}
