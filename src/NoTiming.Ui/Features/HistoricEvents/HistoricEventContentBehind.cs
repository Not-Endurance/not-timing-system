using Not.Blazor.Components.Abstractions;
using NTS.Domain.Core.Aggregates;
using static NoTiming.Ui.Routes;

namespace NoTiming.Ui.Features.HistoricEvents;

public class HistoricEventContentBehind : NComponent
{
    protected string CreateHistoricEventRoute(EventInformation eventInformation)
    {
        return $"{HISTORIC_EVENTS_PAGE}/{eventInformation.Id}";
    }
}
