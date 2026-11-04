using NC.Injection;

namespace NC.Application.Services;

public interface ISeeker<T> : ITransient
{
    Task<IEnumerable<T>> Search(string term, CancellationToken ct);
}
