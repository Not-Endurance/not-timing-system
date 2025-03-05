using Not.Injection;

namespace Not.Blazor.Ports;

public interface ISearchable<T> : ITransient
{
    Task<IEnumerable<T>> Search(string term);
}
