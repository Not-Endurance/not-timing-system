using MudBlazor;
using Not.Notify;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Judge.Features.Core.Handouts;

namespace NTS.Judge.Blazor.Features.Core.Handouts.Form;

public partial class HandoutForm
{
    Combination? _combination;

    [Inject]
    ICreateHandout Behind { get; set; } = default!;

    [Inject]
    INotifier Notifier { get; set; } = default!;

    async Task Create()
    {
        if (_combination == null)
        {
            Notifier.Warn("Please select the combination");
            return;
        }
        await Behind.Create(_combination.Number);
    }

    async Task<IEnumerable<Combination?>> Search(string term, CancellationToken _)
    {
        var combinations = await Behind.GetCombinations();
        if (string.IsNullOrEmpty(term))
        {
            return combinations;
        }
        return combinations.Where(x => x.ToString().Contains(term, StringComparison.InvariantCultureIgnoreCase));
    }
}
