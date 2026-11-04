using NC.Observables;

namespace NC.Application.Behinds.Adapters;

public interface IStatefulService : IObservable
{
    Task Load();
}
