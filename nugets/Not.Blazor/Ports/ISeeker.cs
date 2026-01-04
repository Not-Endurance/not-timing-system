using Not.Injection;

namespace Not.Blazor.Ports;

public interface ISeeker<T> : ITransient
{
    Task<IEnumerable<T>> Search(string term);
}
