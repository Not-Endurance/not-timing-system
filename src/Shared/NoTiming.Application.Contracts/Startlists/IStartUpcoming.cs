using NC.Application.Behinds.Adapters;
using NoTiming.Domain.Core.Objects.Startlists;

namespace NoTiming.Application.Contracts.Startlists;

public interface IStartUpcoming : IStatefulService
{
    IReadOnlyList<Starter> Upcoming { get; }
    void Tick();
}
