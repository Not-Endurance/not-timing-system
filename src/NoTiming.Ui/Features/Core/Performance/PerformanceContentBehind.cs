using NoTiming.Ui.Features.Core.EventViews;
using NTS.Contracts.Core;
using NTS.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Ui.Features.Core.Performance;

public class PerformanceContentBehind : EventPageBehind
{
    [Inject]
    IParticipationContext Context { get; set; } = default!;

    [Inject]
    IParticipationStore Store { get; set; } = default!;

    protected IReadOnlyList<int> Recent => Context.RecentlyTimed;

    protected Participation? Selected
    {
        get => Context.Selected;
        set => Context.Selected = value;
    }

    protected IReadOnlyList<Participation> Participations => Store.Participations;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Context);
        await Observe(Store);
    }

    protected Task<IEnumerable<Participation?>> Search(string term, CancellationToken _)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return Task.FromResult(Participations.Cast<Participation?>());
            }

            var result = Participations
                .Where(x => x.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
                .Cast<Participation?>();
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            Handle(ex);
            return Task.FromResult(Enumerable.Empty<Participation?>());
        }
    }
}
