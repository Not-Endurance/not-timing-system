using Not.Blazor.Ports;
using Not.Injection;
using NTS.Domain.Core.Objects;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Blazor.Components.Startlist.Upcoming;

public interface IStartlistUpcoming : IObservableBehind, ISingleton
{
    IReadOnlyList<StartlistEntry> Upcoming { get; }
}
