using Not.Blazor.Components.Abstractions;
using NTS.Domain.Core.Aggregates;
using static NTS.Judge.Blazor.Routes;

namespace NTS.Judge.Blazor.Features.PastEvents;

public class PastEventContentBehind : NComponent
{
    protected string CreatePastEventRoute(EventInformation eventInformation)
    {
        return $"{PAST_EVENTS_PAGE}/{eventInformation.Id}";
    }
}
