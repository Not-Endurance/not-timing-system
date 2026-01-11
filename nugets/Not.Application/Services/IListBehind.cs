using Not.Application.Behinds.Adapters;

namespace Not.Application.Services;

public interface IListBehind<T> : IDeleteBehind<T>, IStatefulService
{
    IReadOnlyList<T> Items { get; }
}
