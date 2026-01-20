using Not.Blazor.Components;
using NTS.Domain.Core.Objects;
using NTS.Judge.Features.Core.Rankings;

namespace NTS.Judge.Blazor.Core.Rankings.Ranklists;

public class RanklistTableBehind : NComponent
{
    [Inject]
    IRankingContext RankingContext { get; set; } = default!;

    public Ranklist Ranklist { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(RankingContext);
    }

    protected override void OnBeforeRender()
    {
        Ranklist = new(RankingContext.Current);
    }
}
