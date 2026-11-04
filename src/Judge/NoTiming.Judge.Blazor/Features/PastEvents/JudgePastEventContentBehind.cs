using NC.Blazor.Components.Abstractions;
using NoTiming.Domain.Core.Aggregates;
using static NoTiming.Judge.Blazor.JudgeRoutes;

namespace NoTiming.Judge.Blazor.Features.PastEvents;

public class JudgePastEventContentBehind : NComponent
{
    protected string CreatePastEventRoute(EventInformation eventInformation)
    {
        return $"{PAST_EVENTS_PAGE}/{eventInformation.Id}";
    }
}
