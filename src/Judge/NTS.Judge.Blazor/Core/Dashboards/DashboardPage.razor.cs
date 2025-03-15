using Microsoft.AspNetCore.Components.Forms;
using Not.Blazor.Components;
using NTS.Judge.Blazor.Shared.Components.SidePanels;

namespace NTS.Judge.Blazor.Core.Dashboards;

public partial class DashboardPage : NComponent
{
    [Inject]
    ICoreBehind CoreBehind { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(CoreBehind);
    }

    async Task ImportEmsData(InputFileChangeEventArgs args)
    {
        using var stream = args.File.OpenReadStream();
        using var stringReader = new StreamReader(stream);
        var contents = await stringReader.ReadToEndAsync();
        if (contents == null)
        {
            return;
        }
        await CoreBehind.Import(contents);
    }
}
