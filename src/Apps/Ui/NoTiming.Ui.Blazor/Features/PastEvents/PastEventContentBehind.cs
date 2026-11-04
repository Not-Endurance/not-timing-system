using Not.Blazor.Components.Abstractions;
using NTS.Domain.Core.Aggregates;
using static NoTiming.Ui.Blazor.Routes;

namespace NoTiming.Ui.Blazor.Features.PastEvents;

public class PastEventContentBehind : NComponent
{
    protected string CreatePastEventRoute(EventInformation eventInformation)
    {
        return $"{PAST_EVENTS_PAGE}/{eventInformation.Id}";
    }
}
