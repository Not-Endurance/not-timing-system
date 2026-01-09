using MudBlazor;
using Not.Notify;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Judge.Features.Core.Handouts;

namespace NTS.Judge.Blazor.Core.Handouts.Form;

public partial class HandoutForm
{
    Combination? _combination;

    [Inject]
    ICreateHandout Behind { get; set; } = default!;

    async Task Create()
    {
        if (_combination == null)
        {
            NotifyHelper.Warn("Please select the combination");
            return;
        }
        await Behind.Create(_combination.Number);
    }

    async Task<IEnumerable<Combination?>> Search(string term)
    {
        var combinations = await Behind.GetCombinations();
        if (string.IsNullOrEmpty(term))
        {
            return combinations;
        }
        return combinations.Where(x => x.ToString().Contains(term, StringComparison.InvariantCultureIgnoreCase));
    }
}
