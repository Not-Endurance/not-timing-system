using Not.Blazor.Components.Abstractions;
using Not.Notify;
using NTS.Contracts.Features.Console;
using NTS.Domain.Core.Aggregates.Participations.Entities;

namespace NoTiming.Console.Features.Handouts;

/// <summary>The Handout of a Combination, made by hand: the sheet of a Participation whose Phase the Console did not see complete.</summary>
public class HandoutFormBehind : NComponent
{
    [Inject]
    IConsoleHandouts Handouts { get; set; } = default!;

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

            await Handouts.Create(Combination.Number);
            Combination = null;
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected Task<IEnumerable<Combination?>> Search(string term, CancellationToken _)
    {
        var found = string.IsNullOrEmpty(term)
            ? Handouts.Combinations
            :
            [
                .. Handouts.Combinations.Where(x =>
                    x.ToString().Contains(term, StringComparison.CurrentCultureIgnoreCase)
                ),
            ];
        return Task.FromResult(found.Cast<Combination?>());
    }
}
