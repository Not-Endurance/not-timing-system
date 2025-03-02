using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Ports;

namespace NTS.Judge.Adapters;

public class AthleteCache : HttpCache<Athlete>, IAthleteCache
{
    public AthleteCache(IRepository<Athlete> repository) : base(repository)
    {
    }
}
