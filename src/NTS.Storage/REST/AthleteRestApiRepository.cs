using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.Contracts.Setup;
using NTS.Application.Contracts.Setup.Models;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Storage.REST;

public class AthleteRestApiRepository : ApiRepository<Athlete, AthleteModel>, ITransient
{
    public AthleteRestApiRepository(NHttpClient client)
        : base("athletes", client) { }
}
