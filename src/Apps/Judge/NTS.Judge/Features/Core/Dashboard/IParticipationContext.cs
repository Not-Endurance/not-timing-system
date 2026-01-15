using Not.Blazor.Ports;
using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Features.Core.Dashboard;

public interface IParticipationContext : INObservable
{
    Participation? SelectedParticipation { get; set; }
}
