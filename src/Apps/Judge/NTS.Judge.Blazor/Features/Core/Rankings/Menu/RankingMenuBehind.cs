using Not.Blazor.Components.Abstractions;
using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Blazor.Features.Core.Rankings.Menu;

public class RankingMenuBehind : NComponent
{
    [Parameter]
    public IReadOnlyList<Ranking> Rankings { get; set; } = [];

    [Parameter]
    public Ranking? Selected { get; set; }

    [Parameter]
    public EventCallback<Ranking> OnSelectedSafe { get; set; }

    [Parameter]
    public EventCallback<Ranking> OnDeleteSafe { get; set; }

    protected async Task Select(Ranking ranking)
    {
        try
        {
            if (OnSelectedSafe.HasDelegate)
            {
                await OnSelectedSafe.InvokeAsync(ranking);
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task RequestDelete(Ranking ranking)
    {
        try
        {
            if (OnDeleteSafe.HasDelegate)
            {
                await OnDeleteSafe.InvokeAsync(ranking);
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
