using Not.Blazor.Components;
using Not.Safe;
using Not.Structures;
using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Blazor.Core.Rankings.Menu;

public class RankingMenuBehind : NComponent
{
    [Inject]
    IRankingMenuService Service { get; set; } = default!;

    public Ranking? SelectedRanking => Service.SelectedRanking;
    public ObservableList<Ranking> Rankings => Service.Rankings;
    
    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }

    public async Task Select(Ranking? ranking)
    {
        if (ranking == null)
        {
            return;
        }
        await SafeHelper.Run(() => Service.Select(ranking)); 
    }
}
