using MudBlazor;
using Not.Application.Services;
using Not.Krud.Blazor.Components.Abstractions;
using Not.Strings;
using NTS.Blazor.Constants;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;
using NTS.Judge.Features.Setup.UpcomingEvents.Participations;

namespace NTS.Judge.Blazor.Features.Setup.UpcomingEvents.Participations;

public class ParticipationShellBehind : KrudShell<ParticipationFormModel>
{
    public ParticipationShellBehind()
    {
        TimeMask = new(Masks.MINUTES_TIME_MASK_FORMAT);
    }

    [Inject]
    ISettService<Combination> Behind { get; set; } = default!;

    protected PatternMask TimeMask { get; }

    protected async Task<IEnumerable<Combination>> SearchCombinationsSafe(string term, CancellationToken _)
    {
        return Search(await Behind.ReadMany(), term);
    }

    // TODO: extract search functionality somehow, because ToString() should be identical (maybe ToString should be configurable)
    IEnumerable<T> Search<T>(IEnumerable<T> values, string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return values;
        }
        return values.Where(x => x != null && StringExtensions.NContains(x.ToString()!, term));
    }
}
