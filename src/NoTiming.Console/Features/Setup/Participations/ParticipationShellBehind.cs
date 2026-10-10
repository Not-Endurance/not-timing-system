using MudBlazor;
using Not.Krud.Blazor.Components.Abstractions;
using NoTiming.Ui.Constants;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NoTiming.Console.Features.Setup.Participations;

public class ParticipationShellBehind : KrudShell<ParticipationFormModel>
{
    public ParticipationShellBehind()
    {
        TimeMask = new(Masks.MINUTES_TIME_MASK_FORMAT);
    }

    [Inject]
    IConsoleLookups Lookups { get; set; } = default!;

    protected PatternMask TimeMask { get; }

    protected async Task<IEnumerable<Combination?>> SearchCombinationsSafe(string term, CancellationToken _)
    {
        return (await Lookups.SearchCombinations(term)).Cast<Combination?>();
    }
}
