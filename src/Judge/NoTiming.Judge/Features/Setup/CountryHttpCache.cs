using NC.Application.CRUD.Ports;
using NC.Application.HTTP;
using NoTiming.Domain.Aggregates;

namespace NoTiming.Judge.Features.Setup;

public class CountryCache : HttpCache<Country>
{
    public CountryCache(IRepository<Country> repository)
        : base(repository) { }
}
