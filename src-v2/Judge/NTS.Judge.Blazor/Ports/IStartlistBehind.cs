using Not.Blazor.Ports.Behinds;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Objects;

namespace NTS.Judge.Blazor.Ports;

public interface IStartlistBehind : IObservableBehind
{
    StartList UpcomingStarts { get; }
    StartList StartHistory { get; }
}
