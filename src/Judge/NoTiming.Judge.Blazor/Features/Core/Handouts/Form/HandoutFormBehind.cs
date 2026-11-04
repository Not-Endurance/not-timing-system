using NC.Blazor.Components.Abstractions;
using NC.Notify;
using NoTiming.Domain.Core.Aggregates.Participations.Entities;
using NoTiming.Judge.Contracts.Features.Core.Handouts;

namespace NoTiming.Judge.Blazor.Features.Core.Handouts.Form;

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
                Notifier.Warn(Please_select_the_combination_string);
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
