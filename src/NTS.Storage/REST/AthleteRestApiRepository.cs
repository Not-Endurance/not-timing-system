using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.Setup;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Storage.REST;

public class AthleteRestApiRepository : RestApiRepository<Athlete, AthleteModel>, ITransient
{
    public AthleteRestApiRepository(NHttpClient client)
        : base("athletes", client) { }
}
