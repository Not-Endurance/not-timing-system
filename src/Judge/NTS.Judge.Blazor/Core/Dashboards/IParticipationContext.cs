using Not.Blazor.Ports;
using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Blazor.Core.Dashboards;

public interface IParticipationContext : INObservable
{
    Participation? SelectedParticipation { get; set; }
}
