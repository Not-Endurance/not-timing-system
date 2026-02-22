using Not.Blazor.Components.Abstractions;
using Not.Notify;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Judge.Features.Core.Handouts;

namespace NTS.Judge.Blazor.Features.Core.Handouts.Form;

public class HandoutFormBehind : NComponent
{
    [Inject]
    ICreateHandout Behind { get; set; } = default!;

    [Inject]
    INotifier Notifier { get; set; } = default!;
    protected Combination? Combination { get; set; }

    protected async Task Create()
    {
        try
        {
            if (Combination == null)
            {
                Notifier.Warn("Please select the combination");
                return;
            }

            await Behind.Create(Combination.Number);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task<IEnumerable<Combination?>> SearchSafe(string term, CancellationToken _)
    {
        var combinations = await Behind.GetCombinations();
        if (string.IsNullOrEmpty(term))
        {
            return combinations;
        }

        return combinations.Where(x => x.ToString().Contains(term, StringComparison.InvariantCultureIgnoreCase));
    }
}
