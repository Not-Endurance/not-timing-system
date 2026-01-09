using Not.Blazor.Ports;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Blazor.Components.Startlist.Upcoming;

public interface IStartUpcoming : INObservable
{
    IReadOnlyList<StartlistEntry> Upcoming { get; }
    void Refresh();
}
