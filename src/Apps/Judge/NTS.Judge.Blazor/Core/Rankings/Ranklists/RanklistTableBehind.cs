using Not.Blazor.Components;
using NTS.Domain.Core.Objects;
using NTS.Judge.Features.Core.Rankings;

namespace NTS.Judge.Blazor.Core.Rankings.Ranklists;

public class RanklistTableBehind : NStatefulComponent<IRankingContext>
{
    public Ranklist Ranklist { get; set; } = default!;

    protected override void OnBeforeRender()
    {
        Ranklist = new(Service.Current);
    }
}
