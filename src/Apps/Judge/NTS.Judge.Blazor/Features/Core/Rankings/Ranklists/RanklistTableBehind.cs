using Not.Blazor.Components.Abstractions;
using NTS.Domain.Core.Objects;
using NTS.Judge.Contracts.Features.Core.Rankings;

namespace NTS.Judge.Blazor.Features.Core.Rankings.Ranklists;

public class RanklistTableBehind : NStatefulComponent
{
    [Inject]
    protected IRankingContext Service { get; set; } = default!;
    public Ranklist Ranklist { get; set; } = default!;

    protected override void OnBeforeRender()
    {
        Ranklist = new(Service.Current);
    }

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }
}
