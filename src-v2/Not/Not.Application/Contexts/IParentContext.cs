using Not.Blazor.Ports.Behinds;
using Not.Injection;
using Not.Structures;

namespace Not.Application.Contexts;

public interface IParentContext<T> : ISingletonService
    where T : IIdentifiable
{
    bool HasLoaded();
    Task Load(int parentId);
    Task Persist();
    ObservableList<T> Children { get; }
    void Add(T child);
    void Update(T child);
    void Remove(T child);
}
