using Not.Application.Behinds.Adapters;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Application.Startlists;

public interface IStartUpcoming : IStatefulService
{
    IReadOnlyList<Starter> Upcoming { get; }
    IReadOnlyDictionary<int, IReadOnlyList<Starter>> UpcomingByStage { get; }
    void Refresh();
}
