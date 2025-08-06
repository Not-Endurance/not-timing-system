using Not.Blazor.Components;
using NTS.Domain.Core.Objects;

namespace NTS.Judge.Blazor.Core.Rankings.Ranklists;

public class RanklistTableBehind : NComponent
{
    [Inject]
    IRanklistDocumentService Service { get; set; } = default!;

    public Ranklist? Ranklist => Service.Document?.Ranklist;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }
}
