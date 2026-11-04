using Not.Observables;

namespace Not.Application.Behinds.Adapters;

public interface IStatefulService : IObservable
{
    Task Load();
}
