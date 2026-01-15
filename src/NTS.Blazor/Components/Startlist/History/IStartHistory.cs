using Not.Application.Behinds.Adapters;
using Not.Injection;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Blazor.Components.Startlist.History;

public interface IStartHistory : IStatefulService
{
    IReadOnlyList<StartlistEntry> History { get; }
}
