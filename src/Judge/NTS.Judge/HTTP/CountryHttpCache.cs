using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using NTS.Domain.Aggregates;

namespace NTS.Judge.HTTP;

public class CountryHttpCache : HttpCache<Country>
{
    public CountryHttpCache(IRepository<Country> repository) : base(repository)
    {
    }
}
