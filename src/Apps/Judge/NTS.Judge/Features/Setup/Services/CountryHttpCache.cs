using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using NTS.Domain.Aggregates;

namespace NTS.Judge.Features.Setup.Services;

public class CountryHttpCache : HttpCache<Country>
{
    public CountryHttpCache(IRepository<Country> repository)
        : base(repository) { }
}
