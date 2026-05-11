using MudBlazor;
using Not.Blazor.Components.Abstractions;
using Not.Blazor.Helpers;
using NTS.Judge.Contracts.Features.Core.Handouts;

namespace NTS.Judge.Blazor.Features.Core.Handouts;

public class HandoutsPageBehind : PrintableComponent
{
    [Inject]
    IDialogService DialogService { get; set; } = default!;

    [Inject]
    protected IHandoutsService Service { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }

    protected async Task OpenPrintPreview()
    {
        try
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
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
