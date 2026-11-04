using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using NTS.Domain.Aggregates;

namespace NTS.Judge.Features.Setup;

public class CountryCache : HttpCache<Country>
{
    public CountryCache(IRepository<Country> repository)
        : base(repository) { }
}
