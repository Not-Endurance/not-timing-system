namespace Not.Blazor.Ports.Behinds;

public interface IListBehind<T> : IDeleteBehind<T>, IObservableBehind
{
    IReadOnlyList<T> Items { get; }
}
