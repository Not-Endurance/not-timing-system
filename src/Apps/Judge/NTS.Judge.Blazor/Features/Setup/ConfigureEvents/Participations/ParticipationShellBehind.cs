using MudBlazor;
using Not.Krud.Blazor.Components.Abstractions;
using NTS.Blazor.Constants;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;
using NTS.Judge.Contracts.Features.Setup.ConfigureEvents.Participations;

namespace NTS.Judge.Blazor.Features.Setup.ConfigureEvents.Participations;

public class ParticipationShellBehind : KrudShell<ParticipationFormModel>
{
    public ParticipationShellBehind()
    {
        TimeMask = new(Masks.MINUTES_TIME_MASK_FORMAT);
    }

    [Inject]
    IJudgeSetupLookupService Lookups { get; set; } = default!;

    protected PatternMask TimeMask { get; }

    protected async Task<IEnumerable<Combination>> SearchCombinationsSafe(string term, CancellationToken _)
    {
        return await Lookups.SearchCombinations(term, CancellationToken.None);
    }
}
