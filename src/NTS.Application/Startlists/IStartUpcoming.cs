using Not.Application.Behinds.Adapters;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Application.Startlists;

public interface IStartUpcoming : IStatefulService
{
    IReadOnlyList<StartlistEntry> Upcoming { get; }
    void Refresh();
}
