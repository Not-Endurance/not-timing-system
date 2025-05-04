namespace NTS.Judge.Blazor.Core.Rankings.Ranklists;

public partial class RanklistTable
{
    [Inject]
    IRankingBehind Behind { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Behind);
    }
}
