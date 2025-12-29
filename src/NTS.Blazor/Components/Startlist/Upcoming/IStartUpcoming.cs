using Not.Blazor.Ports;
using Not.Injection;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Blazor.Components.Startlist.Upcoming;

public interface IStartUpcoming : INObservable, ISingleton
{
    IReadOnlyList<StartlistEntry> Upcoming { get; }
    void Refresh();
}
