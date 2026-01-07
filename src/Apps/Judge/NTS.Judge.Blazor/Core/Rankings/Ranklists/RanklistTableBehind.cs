using Not.Blazor.Components;
using NTS.Domain.Core.Objects;
using NTS.Judge.Features.Core.Rankings;

namespace NTS.Judge.Blazor.Core.Rankings.Ranklists;

public class RanklistTableBehind : NBehind
{
    [Inject]
    IRanklistDocumentService Service { get; set; } = default!;

    public Ranklist? Ranklist => Service.Document?.Ranklist;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }
}
