using Not.Injection;

namespace Not.Application.Services;

public interface ISeeker<T> : ITransient
{
    Task<IEnumerable<T>> Search(string term);
}
