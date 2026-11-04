using MudBlazor;
using NC.Krud.Blazor.Components.Abstractions;
using NoTiming.Blazor.Constants;
using NoTiming.Domain.Setup.Aggregates.ConfigureEvents;
using NoTiming.Judge.Contracts.Features.Setup.ConfigureEvents.Participations;

namespace NoTiming.Judge.Blazor.Features.Setup.ConfigureEvents.Participations;

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
