using Not.Blazor.Ports;
using Not.Injection;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Blazor.Components.Startlist.History;

public interface IStartHistory : INObservable, ISingleton
{
    IReadOnlyList<StartlistEntry> History { get; }
}
